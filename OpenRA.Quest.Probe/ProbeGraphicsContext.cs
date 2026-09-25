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

using Android.Opengl;
using OpenRA.Platforms.Default;
using OpenRA.Primitives;

namespace OpenRA.Quest.Probe
{
	/// <summary>
	/// Minimal OpenRA graphics context for an Android-owned GLES surface. EGL
	/// context creation and presentation remain the GLSurfaceView's responsibility.
	/// </summary>
	sealed class ProbeGraphicsContext : IGraphicsContext
	{
		uint vertexArray;

		public string GLVersion => OpenGL.Version;

		public ProbeGraphicsContext()
		{
			OpenGL.glGenVertexArrays(1, out vertexArray);
			OpenGL.glBindVertexArray(vertexArray);
			OpenGL.CheckGLError();
		}

		public IVertexBuffer<T> CreateEmptyVertexBuffer<T>(int size) where T : struct => new VertexBuffer<T>(size);
		public IVertexBuffer<T> CreateVertexBuffer<T>(T[] data, bool dynamic = true) where T : struct => new VertexBuffer<T>(data, dynamic);
		public T[] CreateVertices<T>(int size) where T : struct => new T[size];
		public IIndexBuffer CreateIndexBuffer(uint[] indices) => new StaticIndexBuffer(indices);
		public ITexture CreateTexture() => new Texture();
		public IFrameBuffer CreateFrameBuffer(Size size) => new FrameBuffer(size, new Texture(), Color.FromArgb(0));
		public IFrameBuffer CreateFrameBuffer(Size size, Color clearColor) => new FrameBuffer(size, new Texture(), clearColor);
		public IShader CreateShader(IShaderBindings bindings) => new Shader(bindings);

		public void EnableScissor(int x, int y, int width, int height)
		{
			OpenGL.glScissor(x, y, Math.Max(0, width), Math.Max(0, height));
			OpenGL.glEnable(OpenGL.GL_SCISSOR_TEST);
			OpenGL.CheckGLError();
		}

		public void DisableScissor()
		{
			OpenGL.glDisable(OpenGL.GL_SCISSOR_TEST);
			OpenGL.CheckGLError();
		}

		public void Present() { }

		public void DrawPrimitives(PrimitiveType type, int firstVertex, int numVertices)
		{
			var mode = type switch
			{
				PrimitiveType.PointList => OpenGL.GL_POINTS,
				PrimitiveType.LineList => OpenGL.GL_LINES,
				PrimitiveType.TriangleList => OpenGL.GL_TRIANGLES,
				_ => throw new ArgumentOutOfRangeException(nameof(type))
			};

			OpenGL.glDrawArrays(mode, firstVertex, numVertices);
			OpenGL.CheckGLError();
		}

		public void DrawElements(int numIndices, int offset)
		{
			OpenGL.glDrawElements(OpenGL.GL_TRIANGLES, numIndices, OpenGL.GL_UNSIGNED_INT, new IntPtr(offset));
			OpenGL.CheckGLError();
		}

		public void Clear()
		{
			OpenGL.glClearColor(0, 0, 0, 1);
			OpenGL.glClear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);
			OpenGL.CheckGLError();
		}

		public void EnableDepthBuffer()
		{
			OpenGL.glClear(OpenGL.GL_DEPTH_BUFFER_BIT);
			OpenGL.glEnable(OpenGL.GL_DEPTH_TEST);
			OpenGL.glDepthFunc(OpenGL.GL_LEQUAL);
			OpenGL.CheckGLError();
		}

		public void DisableDepthBuffer()
		{
			OpenGL.glDisable(OpenGL.GL_DEPTH_TEST);
			OpenGL.CheckGLError();
		}

		public void ClearDepthBuffer()
		{
			OpenGL.glClear(OpenGL.GL_DEPTH_BUFFER_BIT);
			OpenGL.CheckGLError();
		}

		public void SetBlendMode(BlendMode mode)
		{
			OpenGL.glBlendEquation(OpenGL.GL_FUNC_ADD);
			switch (mode)
			{
				case BlendMode.None:
					OpenGL.glDisable(OpenGL.GL_BLEND);
					break;
				case BlendMode.Alpha:
					OpenGL.glEnable(OpenGL.GL_BLEND);
					OpenGL.glBlendFunc(OpenGL.GL_ONE, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
					break;
				case BlendMode.Additive:
				case BlendMode.Subtractive:
					OpenGL.glEnable(OpenGL.GL_BLEND);
					OpenGL.glBlendFunc(OpenGL.GL_ONE, OpenGL.GL_ONE);
					if (mode == BlendMode.Subtractive)
						OpenGL.glBlendEquationSeparate(OpenGL.GL_FUNC_REVERSE_SUBTRACT, OpenGL.GL_FUNC_ADD);
					break;
				case BlendMode.Multiply:
					OpenGL.glEnable(OpenGL.GL_BLEND);
					OpenGL.glBlendFunc(OpenGL.GL_DST_COLOR, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
					break;
				case BlendMode.Multiplicative:
					OpenGL.glEnable(OpenGL.GL_BLEND);
					OpenGL.glBlendFunc(OpenGL.GL_ZERO, OpenGL.GL_SRC_COLOR);
					break;
				case BlendMode.DoubleMultiplicative:
					OpenGL.glEnable(OpenGL.GL_BLEND);
					OpenGL.glBlendFunc(OpenGL.GL_DST_COLOR, OpenGL.GL_SRC_COLOR);
					break;
				case BlendMode.LowAdditive:
					OpenGL.glEnable(OpenGL.GL_BLEND);
					OpenGL.glBlendFunc(OpenGL.GL_DST_COLOR, OpenGL.GL_ONE);
					break;
				case BlendMode.Screen:
					OpenGL.glEnable(OpenGL.GL_BLEND);
					OpenGL.glBlendFunc(OpenGL.GL_SRC_COLOR, OpenGL.GL_ONE_MINUS_SRC_COLOR);
					break;
				case BlendMode.Translucent:
					OpenGL.glEnable(OpenGL.GL_BLEND);
					OpenGL.glBlendFunc(OpenGL.GL_DST_COLOR, OpenGL.GL_ONE_MINUS_DST_COLOR);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(mode));
			}

			OpenGL.CheckGLError();
		}

		public void SetVSyncEnabled(bool enabled) { }

		public void Dispose()
		{
			if (vertexArray == 0)
				return;

			OpenGL.glBindVertexArray(0);
			GLES30.GlDeleteVertexArrays(1, [(int)vertexArray], 0);
			vertexArray = 0;
			OpenGL.CheckGLError();
		}
	}
}
