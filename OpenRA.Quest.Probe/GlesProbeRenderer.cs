#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Immutable;
using System.IO;
using System.Numerics;
using Android.Graphics;
using Android.Opengl;
using Java.Nio;
using Javax.Microedition.Khronos.Opengles;
using OpenRA.FileFormats;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Terrain;
using OpenRA.Network;
using OpenRA.Primitives;
using EGLConfig = Javax.Microedition.Khronos.Egl.EGLConfig;

namespace OpenRA.Quest.Probe
{
	/// <summary>
	/// Draws the parsed map through the Quest's GLES driver and exercises
	/// OpenRA's renderer on the same surface.
	/// </summary>
	sealed class GlesProbeRenderer(Bitmap terrain, string capturePath, string openRaCapturePath,
		string rendererCapturePath, string worldCapturePath, string authenticTerrainCapturePath,
		string gameWorldCapturePath, string regularWorldCapturePath,
		QuestInputQueue input, bool contentReady, Action<bool> onSessionStateChanged,
		Action<string> onSessionMessage) : Java.Lang.Object, GLSurfaceView.IRenderer
	{
		const string VertexSource = """
			#version 300 es
			layout(location = 0) in vec2 position;
			layout(location = 1) in vec2 uv;
			uniform vec2 boardScale;
			out vec2 texCoord;
			void main() {
				gl_Position = vec4(position * boardScale, 0.0, 1.0);
				texCoord = uv;
			}
			""";

		const string FragmentSource = """
			#version 300 es
			precision mediump float;
			in vec2 texCoord;
			uniform sampler2D terrainTexture;
			out vec4 color;
			void main() {
				color = texture(terrainTexture, texCoord);
			}
			""";

		readonly Bitmap terrain = terrain;
		readonly string capturePath = capturePath;
		readonly string openRaCapturePath = openRaCapturePath;
		readonly string rendererCapturePath = rendererCapturePath;
		readonly string worldCapturePath = worldCapturePath;
		readonly string authenticTerrainCapturePath = authenticTerrainCapturePath;
		readonly string gameWorldCapturePath = gameWorldCapturePath;
		readonly string regularWorldCapturePath = regularWorldCapturePath;
		readonly QuestInputQueue input = input;
		readonly bool contentReady = contentReady;
		readonly Action<bool> onSessionStateChanged = onSessionStateChanged;
		readonly Action<string> onSessionMessage = onSessionMessage;
		AndroidGlesFunctionProbe? functionProbe;
		QuestGameSession? gameSession;
		int program;
		int vertexArray;
		int texture;
		int width;
		int height;
		bool captured;
		bool rendererProbed;
		bool showingWorldFrame;
		bool sessionAttempted;

		public void OnSurfaceCreated(IGL10? gl, EGLConfig? config)
		{
			var hadSession = gameSession != null;
			try
			{
				gameSession?.Dispose();
			}
			catch (Exception e)
			{
				Android.Util.Log.Warn("OpenRA.Quest.Probe", $"Vorige Spielsession konnte nach Kontextwechsel nicht freigegeben werden: {e}");
			}

			gameSession = null;
			input.SetEnabled(false);
			if (hadSession)
				onSessionStateChanged(false);
			rendererProbed = false;
			showingWorldFrame = false;
			sessionAttempted = false;
			captured = false;
			Android.Util.Log.Info("OpenRA.Quest.Probe", $"OpenGL-ES-Kontext: {GLES30.GlGetString(GLES30.GlVersion)}");
			int[] extensionCount = new int[1];
			GLES30.GlGetIntegerv(GLES30.GlNumExtensions, extensionCount, 0);
			string[] relevantExtensions =
			[
				"GL_EXT_texture_format_BGRA8888",
				"GL_OES_standard_derivatives",
				"GL_EXT_read_format_bgra",
				"GL_KHR_debug"
			];
			var available = new HashSet<string>();
			for (var i = 0; i < extensionCount[0]; i++)
				available.Add(GLES30.GlGetStringi(GLES30.GlExtensions, i) ?? "");

			foreach (var extension in relevantExtensions)
				Android.Util.Log.Info("OpenRA.Quest.Probe", $"GLES-Erweiterung {extension}: {available.Contains(extension)}");

			try
			{
				functionProbe?.Dispose();
				functionProbe = new AndroidGlesFunctionProbe();
				var (resolved, missing, nativeVersion) = functionProbe.CheckFunctions();
				Android.Util.Log.Info("OpenRA.Quest.Probe", $"Native-GLES-Funktionen: {resolved} aufgelöst, {missing.Length} fehlen; Version: {nativeVersion}");
				if (missing.Length > 0)
					Android.Util.Log.Warn("OpenRA.Quest.Probe", $"Fehlende GLES-Funktionen: {string.Join(", ", missing)}");

				// The diagnostic renderer uses Android's GLES30 API below. Initialize
				// OpenRA's original bindings separately to validate the integration
				// path. Disable the optional debug callback for this first ABI probe.
				OpenRA.Platforms.Default.OpenGL.Initialize(functionProbe.Resolve,
					name => name != "GL_KHR_debug" && available.Contains(name));
				OpenRA.Platforms.Default.OpenGL.CheckGLError();
				Android.Util.Log.Info("OpenRA.Quest.Probe",
					$"OpenRA-GL-Binding: {OpenRA.Platforms.Default.OpenGL.Version}, Profil {OpenRA.Platforms.Default.OpenGL.Profile}");
				ProbeOpenRaTexture();
				ProbeOpenRaShader();
				ProbeOpenRaFrameBuffer();
				ProbeOpenRaDraw();
				RenderOpenRaTerrain();
			}
			catch (Exception e)
			{
				Android.Util.Log.Error("OpenRA.Quest.Probe", $"OpenRA-Grafikprüfung fehlgeschlagen: {e}");
			}

			program = CreateProgram();

			// x, y, u, v. The image appears upright after GLUtils upload.
			float[] vertices =
			[
				-1, -1, 0, 1,
				 1, -1, 1, 1,
				-1,  1, 0, 0,
				 1,  1, 1, 0
			];
			using var vertexBytes = ByteBuffer.AllocateDirect(vertices.Length * sizeof(float));
			vertexBytes.Order(ByteOrder.NativeOrder()!);
			using var vertexFloats = vertexBytes.AsFloatBuffer();
			vertexFloats.Put(vertices);
			vertexFloats.Position(0);

			int[] ids = new int[1];
			GLES30.GlGenVertexArrays(1, ids, 0);
			vertexArray = ids[0];
			GLES30.GlBindVertexArray(vertexArray);
			GLES30.GlGenBuffers(1, ids, 0);
			GLES30.GlBindBuffer(GLES30.GlArrayBuffer, ids[0]);
			GLES30.GlBufferData(GLES30.GlArrayBuffer, vertices.Length * sizeof(float), vertexFloats, GLES30.GlStaticDraw);
			GLES30.GlEnableVertexAttribArray(0);
			GLES30.GlVertexAttribPointer(0, 2, GLES30.GlFloat, false, 4 * sizeof(float), 0);
			GLES30.GlEnableVertexAttribArray(1);
			GLES30.GlVertexAttribPointer(1, 2, GLES30.GlFloat, false, 4 * sizeof(float), 2 * sizeof(float));
			GLES30.GlBindVertexArray(0);

			GLES30.GlGenTextures(1, ids, 0);
			texture = ids[0];
			GLES30.GlBindTexture(GLES30.GlTexture2d, texture);
			GLES30.GlTexParameteri(GLES30.GlTexture2d, GLES30.GlTextureMinFilter, GLES30.GlNearest);
			GLES30.GlTexParameteri(GLES30.GlTexture2d, GLES30.GlTextureMagFilter, GLES30.GlNearest);
			GLES30.GlTexParameteri(GLES30.GlTexture2d, GLES30.GlTextureWrapS, GLES30.GlClampToEdge);
			GLES30.GlTexParameteri(GLES30.GlTexture2d, GLES30.GlTextureWrapT, GLES30.GlClampToEdge);
			GLUtils.TexImage2D(GLES30.GlTexture2d, 0, terrain, 0);
			GLES30.GlUseProgram(program);
			GLES30.GlUniform1i(GLES30.GlGetUniformLocation(program, "terrainTexture"), 0);
			CheckError("texture upload");
		}

		void ProbeOpenRaTexture()
		{
			var size = 1;
			while (size < Math.Max(terrain.Width, terrain.Height))
				size *= 2;

			var sourcePixels = new int[terrain.Width * terrain.Height];
			terrain.GetPixels(sourcePixels, 0, terrain.Width, 0, 0, terrain.Width, terrain.Height);
			var bgra = new byte[size * size * 4];
			for (var y = 0; y < terrain.Height; y++)
				for (var x = 0; x < terrain.Width; x++)
				{
					var color = sourcePixels[y * terrain.Width + x];
					var offset = (y * size + x) * 4;
					bgra[offset] = (byte)color;
					bgra[offset + 1] = (byte)(color >> 8);
					bgra[offset + 2] = (byte)(color >> 16);
					bgra[offset + 3] = (byte)(color >> 24);
				}

			using var openRaTexture = new OpenRA.Platforms.Default.Texture();
			openRaTexture.SetData(bgra, size, size);
			var returned = openRaTexture.GetData();
			var mismatches = 0;
			for (var i = 0; i < bgra.Length; i++)
				if (bgra[i] != returned[i])
					mismatches++;

			Android.Util.Log.Info("OpenRA.Quest.Probe",
				$"OpenRA-Textur-Roundtrip: {size}x{size}, {mismatches} abweichende Bytes von {bgra.Length}.");
		}

		static void ProbeOpenRaShader()
		{
			int[] vertexArray = new int[1];
			GLES30.GlGenVertexArrays(1, vertexArray, 0);
			GLES30.GlBindVertexArray(vertexArray[0]);
			try
			{
				_ = new OpenRA.Platforms.Default.Shader(new CombinedShaderBindings());
				Android.Util.Log.Info("OpenRA.Quest.Probe", "OpenRA-Combined-Shader: auf Quest 3 kompiliert und verknüpft.");
			}
			finally
			{
				GLES30.GlBindVertexArray(0);
				GLES30.GlDeleteVertexArrays(1, vertexArray, 0);
			}
		}

		static void ProbeOpenRaFrameBuffer()
		{
			using var target = new OpenRA.Platforms.Default.FrameBuffer(
				new Size(256, 256), new OpenRA.Platforms.Default.Texture(), OpenRA.Primitives.Color.FromArgb(0));
			target.Bind();
			OpenRA.Platforms.Default.OpenGL.glClearColor(1, 0, 0, 1);
			OpenRA.Platforms.Default.OpenGL.glClear(OpenRA.Platforms.Default.OpenGL.GL_COLOR_BUFFER_BIT);
			OpenRA.Platforms.Default.OpenGL.CheckGLError();
			target.Unbind();

			var pixels = target.Texture.GetData();
			var red = pixels[0] == 0 && pixels[1] == 0 && pixels[2] == 255 && pixels[3] == 255;
			Android.Util.Log.Info("OpenRA.Quest.Probe",
				$"OpenRA-Framebuffer: 256x256 vollständig, roter Testpixel korrekt: {red}.");
			if (!red)
				throw new InvalidOperationException("OpenRA framebuffer readback returned an unexpected color.");
		}

		static void ProbeOpenRaDraw()
		{
			int[] vertexArray = new int[1];
			GLES30.GlGenVertexArrays(1, vertexArray, 0);
			GLES30.GlBindVertexArray(vertexArray[0]);
			try
			{
				var shader = new OpenRA.Platforms.Default.Shader(new CombinedShaderBindings());
				shader.SetVec("Scroll", 0, 0, 0);
				shader.SetVec("p1", 2f / 256, 2f / 256, 0);
				shader.SetVec("p2", -1, -1, 0);
				shader.SetVec("PaletteRows", 1);
				shader.SetVec("DepthTextureScale", 0);
				shader.SetBool("EnableDepthPreview", false);
				shader.SetBool("EnablePixelArtScaling", false);

				var vertices = new Vertex[]
				{
					new(0, 0, 0, 1, 0, 0, 1, 0, 1, 1, 1, 1),
					new(256, 0, 0, 1, 0, 0, 1, 0, 1, 1, 1, 1),
					new(256, 256, 0, 1, 0, 0, 1, 0, 1, 1, 1, 1),
					new(0, 256, 0, 1, 0, 0, 1, 0, 1, 1, 1, 1)
				};
				using var vertexBuffer = new OpenRA.Platforms.Default.VertexBuffer<Vertex>(vertices, false);
				using var indexBuffer = new OpenRA.Platforms.Default.StaticIndexBuffer([0, 1, 2, 0, 2, 3]);
				using var target = new OpenRA.Platforms.Default.FrameBuffer(
					new Size(256, 256), new OpenRA.Platforms.Default.Texture(), OpenRA.Primitives.Color.FromArgb(0));

				target.Bind();
				shader.PrepareRender();
				vertexBuffer.Bind();
				indexBuffer.Bind();
				shader.Bind();
				OpenRA.Platforms.Default.OpenGL.glDrawElements(
					OpenRA.Platforms.Default.OpenGL.GL_TRIANGLES, 6,
					OpenRA.Platforms.Default.OpenGL.GL_UNSIGNED_INT, IntPtr.Zero);
				OpenRA.Platforms.Default.OpenGL.CheckGLError();
				target.Unbind();

				var pixels = target.Texture.GetData();
				const int center = (128 * 256 + 128) * 4;
				var red = pixels[center] == 0 && pixels[center + 1] == 0 &&
					pixels[center + 2] == 255 && pixels[center + 3] == 255;
				Android.Util.Log.Info("OpenRA.Quest.Probe", $"OpenRA-Combined-Draw: roter Testpixel korrekt: {red}.");
				if (!red)
					throw new InvalidOperationException("OpenRA combined shader did not draw the expected pixel.");
			}
			finally
			{
				GLES30.GlBindVertexArray(0);
				GLES30.GlDeleteVertexArrays(1, vertexArray, 0);
			}
		}

		void RenderOpenRaTerrain()
		{
			const int outputSize = 512;
			var cellCount = terrain.Width * terrain.Height;
			var terrainPixels = new int[cellCount];
			terrain.GetPixels(terrainPixels, 0, terrain.Width, 0, 0, terrain.Width, terrain.Height);
			var vertices = new Vertex[cellCount * 4];
			var indices = new uint[cellCount * 6];
			for (var y = 0; y < terrain.Height; y++)
				for (var x = 0; x < terrain.Width; x++)
				{
					var cell = y * terrain.Width + x;
					var argb = terrainPixels[cell];
					var red = ((argb >> 16) & 0xff) / 255f;
					var green = ((argb >> 8) & 0xff) / 255f;
					var blue = (argb & 0xff) / 255f;
					var alpha = ((argb >> 24) & 0xff) / 255f;
					var left = (float)x * outputSize / terrain.Width;
					var right = (float)(x + 1) * outputSize / terrain.Width;
					var bottom = (float)y * outputSize / terrain.Height;
					var top = (float)(y + 1) * outputSize / terrain.Height;
					var vertex = cell * 4;
					vertices[vertex] = new Vertex(left, bottom, 0, red, green, blue, alpha, 0, 1, 1, 1, 1);
					vertices[vertex + 1] = new Vertex(right, bottom, 0, red, green, blue, alpha, 0, 1, 1, 1, 1);
					vertices[vertex + 2] = new Vertex(right, top, 0, red, green, blue, alpha, 0, 1, 1, 1, 1);
					vertices[vertex + 3] = new Vertex(left, top, 0, red, green, blue, alpha, 0, 1, 1, 1, 1);
					var index = cell * 6;
					indices[index] = (uint)vertex;
					indices[index + 1] = (uint)(vertex + 1);
					indices[index + 2] = (uint)(vertex + 2);
					indices[index + 3] = (uint)vertex;
					indices[index + 4] = (uint)(vertex + 2);
					indices[index + 5] = (uint)(vertex + 3);
				}

			int[] vertexArray = new int[1];
			GLES30.GlGenVertexArrays(1, vertexArray, 0);
			GLES30.GlBindVertexArray(vertexArray[0]);
			try
			{
				var shader = new OpenRA.Platforms.Default.Shader(new CombinedShaderBindings());
				shader.SetVec("Scroll", 0, 0, 0);
				shader.SetVec("p1", 2f / outputSize, 2f / outputSize, 0);
				shader.SetVec("p2", -1, -1, 0);
				shader.SetVec("PaletteRows", 1);
				shader.SetVec("DepthTextureScale", 0);
				shader.SetBool("EnableDepthPreview", false);
				shader.SetBool("EnablePixelArtScaling", false);

				using var vertexBuffer = new OpenRA.Platforms.Default.VertexBuffer<Vertex>(vertices, false);
				using var indexBuffer = new OpenRA.Platforms.Default.StaticIndexBuffer(indices);
				using var target = new OpenRA.Platforms.Default.FrameBuffer(
					new Size(outputSize, outputSize), new OpenRA.Platforms.Default.Texture(),
					OpenRA.Primitives.Color.FromArgb(0));

				target.Bind();
				shader.PrepareRender();
				vertexBuffer.Bind();
				indexBuffer.Bind();
				shader.Bind();
				OpenRA.Platforms.Default.OpenGL.glDrawElements(
					OpenRA.Platforms.Default.OpenGL.GL_TRIANGLES, indices.Length,
					OpenRA.Platforms.Default.OpenGL.GL_UNSIGNED_INT, IntPtr.Zero);
				OpenRA.Platforms.Default.OpenGL.CheckGLError();
				target.Unbind();

				var bgra = target.Texture.GetData();
				var argb = new int[outputSize * outputSize];
				for (var i = 0; i < argb.Length; i++)
				{
					var offset = i * 4;
					argb[i] = (bgra[offset + 3] << 24) | (bgra[offset + 2] << 16) |
						(bgra[offset + 1] << 8) | bgra[offset];
				}

				using var image = Bitmap.CreateBitmap(argb, outputSize, outputSize, Bitmap.Config.Argb8888!);
				using var output = File.Create(openRaCapturePath);
				if (!image.Compress(Bitmap.CompressFormat.Png!, 100, output))
					throw new IOException("Could not save the OpenRA terrain frame.");

				Android.Util.Log.Info("OpenRA.Quest.Probe",
					$"OpenRA-Kartenbild: {terrain.Width}x{terrain.Height} Felder, {indices.Length / 3} Dreiecke, {openRaCapturePath}");
			}
			finally
			{
				GLES30.GlBindVertexArray(0);
				GLES30.GlDeleteVertexArrays(1, vertexArray, 0);
			}
		}

		public void OnSurfaceChanged(IGL10? gl, int width, int height)
		{
			if (gameSession != null && (this.width != width || this.height != height))
			{
				try { gameSession.Dispose(); }
				catch (Exception e)
				{
					Android.Util.Log.Warn("OpenRA.Quest.Probe", $"Spielsession konnte nach Größenänderung nicht freigegeben werden: {e}");
				}

				gameSession = null;
				input.SetEnabled(false);
				sessionAttempted = false;
				onSessionStateChanged(false);
			}

			this.width = width;
			this.height = height;
			GLES30.GlViewport(0, 0, width, height);
			GLES30.GlUseProgram(program);
			GLES30.GlUniform2f(GLES30.GlGetUniformLocation(program, "boardScale"),
				showingWorldFrame ? 1f : Math.Min(1f, (float)height * terrain.Width / (width * terrain.Height)),
				showingWorldFrame ? 1f : Math.Min(1f, (float)width * terrain.Height / (height * terrain.Width)));
			captured = false;
		}

		public void OnDrawFrame(IGL10? gl)
		{
			if (gameSession != null)
			{
				try
				{
					gameSession.TickAndRender();
					return;
				}
				catch (Exception e)
				{
					Android.Util.Log.Error("OpenRA.Quest.Probe", $"Fortlaufende OpenRA-Partie fehlgeschlagen: {e}");
					try { gameSession.Dispose(); }
					catch (Exception disposeError)
					{
						Android.Util.Log.Warn("OpenRA.Quest.Probe", $"Spielsession konnte nicht freigegeben werden: {disposeError}");
					}

					gameSession = null;
					input.SetEnabled(false);
					onSessionStateChanged(false);
					onSessionMessage($"Partie angehalten: {e.Message}");
				}
			}

			if (!rendererProbed && width > 0 && height > 0)
			{
				rendererProbed = true;
				try
				{
					File.Delete(gameWorldCapturePath);
					ProbeFullRenderer();
					ShowWorldFrame();
				}
				catch (Exception e)
				{
					Android.Util.Log.Error("OpenRA.Quest.Probe", $"OpenRA-Renderer-Prüfung fehlgeschlagen: {e}");
				}
			}

			GLES30.GlClearColor(0.08f, 0.18f, 0.30f, 1f);
			GLES30.GlClear(GLES30.GlColorBufferBit);
			GLES30.GlUseProgram(program);
			GLES30.GlActiveTexture(GLES30.GlTexture0);
			GLES30.GlBindTexture(GLES30.GlTexture2d, texture);
			GLES30.GlBindVertexArray(vertexArray);
			GLES30.GlDrawArrays(GLES30.GlTriangleStrip, 0, 4);
			CheckError("terrain draw");

			if (!captured && width > 0 && height > 0)
			{
				captured = true;
				CaptureFrame(capturePath, "GLES-Kartenbild");
			}

			if (!sessionAttempted && contentReady && width > 0 && height > 0)
			{
				sessionAttempted = true;
				try
				{
					var appFiles = System.IO.Path.GetDirectoryName(rendererCapturePath)!;
					gameSession = new QuestGameSession(appFiles, new Size(width, height), input);
					input.SetEnabled(true);
					onSessionStateChanged(true);
					onSessionMessage("Red-Alert-Partie läuft.");
				}
				catch (Exception e)
				{
					Android.Util.Log.Error("OpenRA.Quest.Probe", $"Fortlaufende OpenRA-Partie konnte nicht gestartet werden: {e}");
					gameSession = null;
					onSessionMessage($"Spielstart fehlgeschlagen: {e.Message}");
				}
			}
		}

		void ShowWorldFrame()
		{
			if (!File.Exists(gameWorldCapturePath))
				return;

			using var image = BitmapFactory.DecodeFile(gameWorldCapturePath);
			if (image == null)
				throw new IOException("Could not read the OpenRA world frame for display.");

			GLES30.GlBindTexture(GLES30.GlTexture2d, texture);
			GLUtils.TexImage2D(GLES30.GlTexture2d, 0, image, 0);
			GLES30.GlUseProgram(program);
			GLES30.GlUniform2f(GLES30.GlGetUniformLocation(program, "boardScale"), 1f, 1f);
			CheckError("OpenRA world frame display");
			showingWorldFrame = true;
			Android.Util.Log.Info("OpenRA.Quest.Probe", "OpenRA-Editor-Weltbild auf der Android-Oberfläche angezeigt.");
		}

		void ProbeFullRenderer()
		{
			var settings = new GraphicSettings
			{
				Mode = WindowMode.Windowed,
				WindowedSize = new int2(width, height),
				GLProfile = GLProfile.Embedded
			};
			using var renderer = new Renderer(new ProbePlatform(new Size(width, height)), settings, 4096);
			var previousRenderer = Game.Renderer;
			var previousModData = Game.ModData;
			Game.Renderer = renderer;
			try
			{
				var appFiles = System.IO.Path.GetDirectoryName(rendererCapturePath)!;
				var mods = new InstalledMods([System.IO.Path.Combine(appFiles, "mods")], []);
				if (!mods.TryGetValue("ra", out var manifest))
					throw new InvalidOperationException("The Red Alert mod is unavailable for font initialization.");

				using var modData = new ModData(manifest, mods);
				Game.ModData = modData;
				renderer.InitializeFonts(modData);
				Android.Util.Log.Info("OpenRA.Quest.Probe", $"OpenRA-Schriften: {renderer.Fonts.Count} aus dem Red-Alert-Mod geladen.");
				using var iconStream = modData.DefaultFileSystem.Open("ra|icon.png");
				using var iconSheets = new SheetBuilder(SheetType.BGRA, 64);
				var icon = iconSheets.Add(new Png(iconStream));
				renderer.BeginUI();
				renderer.RgbaColorRenderer.FillRect(Vector3.Zero, new Vector3(width / 2f, height, 0),
					OpenRA.Primitives.Color.FromArgb(255, 200, 40, 40), BlendMode.None);
				renderer.RgbaColorRenderer.FillRect(new Vector3(width / 2f, 0, 0), new Vector3(width, height, 0),
					OpenRA.Primitives.Color.FromArgb(255, 40, 80, 200), BlendMode.None);
				renderer.Fonts["Regular"].DrawText("OPENRA QUEST", new Vector2(12, 16), OpenRA.Primitives.Color.White);
				renderer.RgbaSpriteRenderer.DrawSprite(icon, new Vector3(width - 44, 12, 0));
				renderer.EndFrame(new ProbeInputHandler());
				CaptureFrame(rendererCapturePath, "OpenRA-Renderer-UI");
			}
			finally
			{
				Game.ModData = previousModData;
				Game.Renderer = previousRenderer;
			}

			renderer.SetMaximumViewportSize(new Size(width, height));
			renderer.BeginWorld(new Vector2(width / 2f, height / 2f), new Size(width, height));
			var terrainPixels = new int[terrain.Width * terrain.Height];
			terrain.GetPixels(terrainPixels, 0, terrain.Width, 0, 0, terrain.Width, terrain.Height);
			for (var y = 0; y < terrain.Height; y++)
				for (var x = 0; x < terrain.Width; x++)
				{
					var left = (float)x * width / terrain.Width;
					var right = (float)(x + 1) * width / terrain.Width;
					var top = (float)y * height / terrain.Height;
					var bottom = (float)(y + 1) * height / terrain.Height;
					renderer.WorldRgbaColorRenderer.FillRect(new Vector3(left, top, 0),
						new Vector3(right, bottom, 0),
						OpenRA.Primitives.Color.FromArgb(unchecked((uint)terrainPixels[y * terrain.Width + x])), BlendMode.None);
				}

			// A generated marker exercises Sheet, Sprite, RgbaSpriteRenderer and
			// the texture sampler without packaging any original game artwork.
			var markerPixels = new byte[16 * 16 * 4];
			for (var y = 0; y < 16; y++)
				for (var x = 0; x < 16; x++)
				{
					var inside = (x - 7.5f) * (x - 7.5f) + (y - 7.5f) * (y - 7.5f) <= 45;
					var offset = (y * 16 + x) * 4;
					markerPixels[offset] = 0;
					markerPixels[offset + 1] = 255;
					markerPixels[offset + 2] = 255;
					markerPixels[offset + 3] = inside ? (byte)255 : (byte)0;
				}

			var markerTexture = new OpenRA.Platforms.Default.Texture();
			markerTexture.SetData(markerPixels, 16, 16);
			using var markerSheet = new Sheet(SheetType.BGRA, markerTexture);
			var marker = new Sprite(markerSheet, new Rectangle(0, 0, 16, 16), TextureChannel.RGBA);
			renderer.WorldRgbaSpriteRenderer.DrawSprite(marker,
				new Vector3(width / 2f - 24, height / 2f - 24, 0), 3f);

			renderer.BeginUI();
			renderer.EndFrame(new ProbeInputHandler());
			CaptureFrame(worldCapturePath, "OpenRA-Renderer-Welt");
			ProbeAuthenticTerrain(renderer);
		}

		void ProbeAuthenticTerrain(Renderer renderer)
		{
			var appFiles = System.IO.Path.GetDirectoryName(authenticTerrainCapturePath)!;
			if (!File.Exists(System.IO.Path.Combine(appFiles, "Content/ra/v2/snow.mix")))
			{
				Android.Util.Log.Info("OpenRA.Quest.Probe", "Originale Gelände-Dateien fehlen; echte Tiles werden nicht gezeichnet.");
				return;
			}

			var previousRenderer = Game.Renderer;
			var previousModData = Game.ModData;
			Game.Renderer = renderer;
			try
			{
				var mods = new InstalledMods([System.IO.Path.Combine(appFiles, "mods")], []);
				if (!mods.TryGetValue("ra", out var manifest))
					throw new InvalidOperationException("Red Alert is unavailable for terrain initialization.");

				using var modData = new ModData(manifest, mods);
				Game.ModData = modData;
				using var mapPackage = modData.ModFiles.OpenPackage("ra|maps/blitz.oramap");
				using var map = new Map(modData, mapPackage);
				using var tileCache = new DefaultTileCache((DefaultTerrain)map.Rules.TerrainInfo);
				using var paletteStream = modData.DefaultFileSystem.Open("snow.pal");
				var terrainPalette = new ImmutablePalette(paletteStream, ImmutableArray.Create(0), ImmutableArray.Create(3, 4));
				using var playerPaletteStream = modData.DefaultFileSystem.Open("temperat.pal");
				var playerPalette = new ImmutablePalette(playerPaletteStream, ImmutableArray.Create(0), ImmutableArray.Create(4));
				using var hardwarePalette = new HardwarePalette();
				hardwarePalette.AddPalette("terrain", terrainPalette, false);
				hardwarePalette.AddPalette("player", playerPalette, false);
				hardwarePalette.Initialize();
				var paletteReference = new PaletteReference("terrain", hardwarePalette.GetPaletteIndex("terrain"),
					terrainPalette, hardwarePalette);
				var playerPaletteReference = new PaletteReference("player", hardwarePalette.GetPaletteIndex("player"),
					playerPalette, hardwarePalette);

				renderer.SetMaximumViewportSize(new Size(width, height));
				renderer.BeginWorld(new Vector2(width / 2f, height / 2f), new Size(width, height));
				renderer.SetPalette(hardwarePalette);
				var tileWidth = map.Rules.TerrainInfo.TileSize.Width;
				var tileHeight = map.Rules.TerrainInfo.TileSize.Height;
				var columns = Math.Min(width / tileWidth + 1, map.MapSize.Width - 40);
				var rows = Math.Min(height / tileHeight + 1, map.MapSize.Height - 40);
				for (var y = 0; y < rows; y++)
					for (var x = 0; x < columns; x++)
					{
						var tile = map.Tiles[new MPos(x + 40, y + 40)];
						var sprite = tileCache.TileSprite(tile, 0);
						renderer.WorldSpriteRenderer.DrawSprite(sprite, paletteReference,
							new Vector3(x * tileWidth, y * tileHeight, 0));
					}

				map.Sequences.LoadSprites();
				var tankSequence = map.Sequences.GetSequence("1tnk", "idle");
				for (var i = 0; i < 3; i++)
				{
					var tank = tankSequence.GetSprite(0, new WAngle(i * 341));
					renderer.WorldSpriteRenderer.DrawSprite(tank, playerPaletteReference,
						new Vector3(width * (i + 1) / 4f, height * 0.68f, 0));
				}

				Android.Util.Log.Info("OpenRA.Quest.Probe",
					$"Originale Red-Alert-Grafik: {columns * rows} Karten-Tiles und 3 Panzer-Sprites aus Animationssequenzen gezeichnet.");

				renderer.BeginUI();
				renderer.EndFrame(new ProbeInputHandler());
				CaptureFrame(authenticTerrainCapturePath, "OpenRA-Originalterrain");
				ProbeGameWorld(modData, map, renderer, WorldType.Editor, gameWorldCapturePath);
				using var regularMapPackage = modData.ModFiles.OpenPackage("ra|maps/blitz.oramap");
				using var regularMap = new Map(modData, regularMapPackage);
				ProbeGameWorld(modData, regularMap, renderer, WorldType.Regular, regularWorldCapturePath);
			}
			finally
			{
				Game.ModData = previousModData;
				Game.Renderer = previousRenderer;
			}
		}

		void ProbeGameWorld(ModData modData, Map map, Renderer renderer, WorldType type, string outputPath)
		{
			var previousSound = Game.Sound;
			var previousOrderManager = Game.OrderManager;
			var previousWorldRenderer = Game.worldRenderer;
			using var sound = new Sound(new ProbePlatform(renderer.NativeResolution), Game.Settings.Sound);
			using var orderManager = new OrderManager(new EchoConnection());
			if (type == WorldType.Regular)
			{
				orderManager.LobbyInfo.GlobalSettings.Map = map.Uid;
				orderManager.LobbyInfo.Slots.Add("Multi0", new Session.Slot { PlayerReference = "Multi0" });
				orderManager.LobbyInfo.Slots.Add("Multi1", new Session.Slot { PlayerReference = "Multi1" });
				orderManager.LobbyInfo.Clients.Add(new Session.Client
				{
					Index = orderManager.Connection.LocalClientId,
					Name = "Quest-Probe",
					Slot = "Multi0",
					Faction = "Random",
					Color = Game.Settings.Player.Color,
					PreferredColor = Game.Settings.Player.Color,
					SpawnPoint = 1,
					State = Session.ClientState.Ready
				});
			}

			Game.Sound = sound;
			Game.OrderManager = orderManager;
			try
			{
				if (type == WorldType.Editor)
				{
					modData.MapCache.LoadMaps(modData);
					Android.Util.Log.Info("OpenRA.Quest.Probe", "OpenRA MapCache: Karten geladen.");
				}

				modData.PrepareMap(map);
				Android.Util.Log.Info("OpenRA.Quest.Probe", "OpenRA PrepareMap: Loader und Sequenzen geladen.");
				var world = new World(map, modData, orderManager, type);
				orderManager.World = world;
				Android.Util.Log.Info("OpenRA.Quest.Probe", $"OpenRA {type}-World: {world.Players.Length} Spieler erzeugt.");
				var worldRenderer = new WorldRenderer(modData, world);
				Game.worldRenderer = worldRenderer;
				Android.Util.Log.Info("OpenRA.Quest.Probe", "OpenRA WorldRenderer: erstellt.");
				world.LoadComplete(worldRenderer);
				Android.Util.Log.Info("OpenRA.Quest.Probe", $"OpenRA {type} World.LoadComplete: abgeschlossen.");
				if (type == WorldType.Regular)
				{
					orderManager.StartGame();
					world.PostLoadComplete(worldRenderer);
					var completedTicks = 0;
					for (var frame = 0; frame < 30; frame++)
					{
						Game.Sound.Tick();
						Sync.RunUnsynced(world, orderManager.TickImmediate);
						if (!orderManager.TryTick())
							continue;

						Sync.RunUnsynced(world, () => world.OrderGenerator.Tick(world));
						world.Tick();
						Sync.RunUnsynced(world, () => world.TickRender(worldRenderer));
						completedTicks++;
					}

					if (completedTicks == 0)
						throw new InvalidOperationException("OpenRA did not advance the local simulation.");

					Android.Util.Log.Info("OpenRA.Quest.Probe",
						$"OpenRA {type}: {completedTicks} Ticks abgeschlossen, {world.Actors.Count()} Akteure, " +
						$"Startfeld {world.LocalPlayer.HomeLocation}, sichtbar: " +
						world.LocalPlayer.Shroud.IsVisible(world.LocalPlayer.HomeLocation));
				}

				var viewCell = type == WorldType.Regular ? world.LocalPlayer.HomeLocation : new CPos(48, 48);
				worldRenderer.Viewport.Center(map.CenterOfCell(viewCell));
				worldRenderer.BeginFrame();
				worldRenderer.Viewport.Tick();
				worldRenderer.PrepareRenderables();
				worldRenderer.EndFrame();
				renderer.BeginWorld(worldRenderer.Viewport.CenterLocation, worldRenderer.Viewport.ViewportSize);
				worldRenderer.Draw();
				renderer.BeginUI();
				renderer.EndFrame(new ProbeInputHandler());
				CaptureFrame(outputPath, $"OpenRA-{type}-Renderer");
				worldRenderer.Dispose();
			}
			catch (Exception e)
			{
				Android.Util.Log.Error("OpenRA.Quest.Probe", $"OpenRA-Spielweltprüfung fehlgeschlagen: {e}");
			}
			finally
			{
				Game.worldRenderer = previousWorldRenderer;
				Game.OrderManager = previousOrderManager;
				Game.Sound = previousSound;
			}
		}

		void CaptureFrame(string path, string label)
		{
			using var pixels = ByteBuffer.AllocateDirect(width * height * 4);
			GLES30.GlReadPixels(0, 0, width, height, GLES30.GlRgba, GLES30.GlUnsignedByte, pixels);
			CheckError("frame readback");
			var rgba = new byte[width * height * 4];
			pixels.Position(0);
			pixels.Get(rgba);
			var argb = new int[width * height];
			for (var y = 0; y < height; y++)
				for (var x = 0; x < width; x++)
				{
					var source = ((height - 1 - y) * width + x) * 4;
					argb[y * width + x] = (rgba[source + 3] << 24) | (rgba[source] << 16) |
						(rgba[source + 1] << 8) | rgba[source + 2];
				}

			using var image = Bitmap.CreateBitmap(argb, width, height, Bitmap.Config.Argb8888!);
			using var output = File.Create(path);
			if (!image.Compress(Bitmap.CompressFormat.Png!, 100, output))
				throw new IOException("Could not save the GLES frame.");

			Android.Util.Log.Info("OpenRA.Quest.Probe", $"{label}: {width}x{height}, {path}");
		}

		static int CreateProgram()
		{
			var vertex = Compile(GLES30.GlVertexShader, VertexSource);
			var fragment = Compile(GLES30.GlFragmentShader, FragmentSource);
			var result = GLES30.GlCreateProgram();
			GLES30.GlAttachShader(result, vertex);
			GLES30.GlAttachShader(result, fragment);
			GLES30.GlLinkProgram(result);
			int[] status = new int[1];
			GLES30.GlGetProgramiv(result, GLES30.GlLinkStatus, status, 0);
			if (status[0] == 0)
				throw new InvalidOperationException($"GLES program link failed: {GLES30.GlGetProgramInfoLog(result)}");

			GLES30.GlDeleteShader(vertex);
			GLES30.GlDeleteShader(fragment);
			return result;
		}

		static int Compile(int type, string source)
		{
			var shader = GLES30.GlCreateShader(type);
			GLES30.GlShaderSource(shader, source);
			GLES30.GlCompileShader(shader);
			int[] status = new int[1];
			GLES30.GlGetShaderiv(shader, GLES30.GlCompileStatus, status, 0);
			if (status[0] == 0)
				throw new InvalidOperationException($"GLES shader compile failed: {GLES30.GlGetShaderInfoLog(shader)}");

			return shader;
		}

		static void CheckError(string step)
		{
			var error = GLES30.GlGetError();
			if (error != GLES30.GlNoError)
				throw new InvalidOperationException($"GLES error after {step}: 0x{error:X}");
		}
	}
}
