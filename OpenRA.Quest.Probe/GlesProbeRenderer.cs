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

using Android.Graphics;
using Android.Opengl;
using Java.Nio;
using Javax.Microedition.Khronos.Opengles;
using EGLConfig = Javax.Microedition.Khronos.Egl.EGLConfig;

namespace OpenRA.Quest.Probe
{
	/// <summary>
	/// Draws the parsed map's diagnostic terrain bitmap through the Quest's GLES
	/// driver. This intentionally does not claim to be OpenRA's sprite renderer.
	/// </summary>
	sealed class GlesProbeRenderer(Bitmap terrain, string capturePath) : Java.Lang.Object, GLSurfaceView.IRenderer
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
		int program;
		int vertexArray;
		int texture;
		int width;
		int height;
		bool captured;

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
				CaptureFrame();
			}
		}

		void CaptureFrame()
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
			using var output = File.Create(capturePath);
			if (!image.Compress(Bitmap.CompressFormat.Png!, 100, output))
				throw new IOException("Could not save the GLES frame.");

			Android.Util.Log.Info("OpenRA.Quest.Probe", $"GLES-Kartenbild: {width}x{height}, {capturePath}");
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
