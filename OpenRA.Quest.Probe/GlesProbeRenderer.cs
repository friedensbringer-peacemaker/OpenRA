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

using System.Numerics;
using Android.Graphics;
using Android.Opengl;
using Java.Nio;
using Javax.Microedition.Khronos.Opengles;
using OpenRA.Graphics;
using OpenRA.Primitives;
using EGLConfig = Javax.Microedition.Khronos.Egl.EGLConfig;

namespace OpenRA.Quest.Probe
{
	/// <summary>
	/// Draws the parsed map's diagnostic terrain bitmap through the Quest's GLES
	/// driver. This intentionally does not claim to be OpenRA's sprite renderer.
	/// </summary>
	sealed class GlesProbeRenderer(Bitmap terrain, string capturePath, string openRaCapturePath,
		string rendererCapturePath, string worldCapturePath) : Java.Lang.Object, GLSurfaceView.IRenderer
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
		AndroidGlesFunctionProbe? functionProbe;
		int program;
		int vertexArray;
		int texture;
		int width;
		int height;
		bool captured;
		bool rendererProbed;

		public void OnSurfaceCreated(IGL10? gl, EGLConfig? config)
		{
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
			this.width = width;
			this.height = height;
			GLES30.GlViewport(0, 0, width, height);
			GLES30.GlUseProgram(program);
			GLES30.GlUniform2f(GLES30.GlGetUniformLocation(program, "boardScale"),
				Math.Min(1f, (float)height * terrain.Width / (width * terrain.Height)),
				Math.Min(1f, (float)width * terrain.Height / (height * terrain.Width)));
			captured = false;
		}

		public void OnDrawFrame(IGL10? gl)
		{
			if (!rendererProbed && width > 0 && height > 0)
			{
				rendererProbed = true;
				try
				{
					ProbeFullRenderer();
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
			renderer.BeginUI();
			renderer.RgbaColorRenderer.FillRect(Vector3.Zero, new Vector3(width / 2f, height, 0),
				OpenRA.Primitives.Color.FromArgb(255, 200, 40, 40), BlendMode.None);
			renderer.RgbaColorRenderer.FillRect(new Vector3(width / 2f, 0, 0), new Vector3(width, height, 0),
				OpenRA.Primitives.Color.FromArgb(255, 40, 80, 200), BlendMode.None);
			renderer.EndFrame(new ProbeInputHandler());
			CaptureFrame(rendererCapturePath, "OpenRA-Renderer-UI");

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
