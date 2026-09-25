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

using System.Runtime.InteropServices;

namespace OpenRA.Quest.Probe
{
	/// <summary>
	/// Checks that Android can resolve the GLES functions bound by the existing
	/// OpenRA OpenGL wrapper, independently of SDL_GL_GetProcAddress. The two
	/// desktop-only functions are excluded from the GLES profile.
	/// </summary>
	sealed class AndroidGlesFunctionProbe : IDisposable
	{
		// Keep this list aligned with OpenRA.Platforms.Default/OpenGL.cs Bind calls.
		const string Functions = """
			glActiveTexture glAttachShader glBindAttribLocation glBindBuffer glBindFramebuffer
			glBindRenderbuffer glBindTexture glBindVertexArray glBlendEquation
			glBlendEquationSeparate glBlendFunc glBufferData glBufferSubData
			glCheckFramebufferStatus glClear glClearColor glCompileShader
			glCopyTexImage2D glCreateProgram glCreateShader glDeleteBuffers
			glDeleteFramebuffers glDeleteRenderbuffers glDeleteTextures glDepthFunc
			glDisable glDisableVertexAttribArray glDrawArrays glDrawElements glEnable
			glEnableVertexAttribArray glFinish glFlush glFramebufferRenderbuffer
			glFramebufferTexture2D glGenBuffers glGenFramebuffers glGenRenderbuffers
			glGenTextures glGenVertexArrays glGetActiveUniform glGetError
			glGetIntegerv glGetProgramInfoLog glGetProgramiv glGetShaderInfoLog
			glGetShaderiv glGetString glGetStringi glGetUniformLocation glIsTexture
			glLinkProgram glPixelStorei glReadPixels glRenderbufferStorage glScissor
			glShaderSource glTexImage2D glTexParameterf glTexParameteri
			glTexSubImage2D glUniform1f glUniform1fv glUniform1i glUniform2f
			glUniform2fv glUniform3f glUniform3fv glUniform4fv glUniformMatrix4fv
			glUseProgram glVertexAttribIPointer glVertexAttribPointer glViewport
			""";

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		delegate IntPtr EglGetProcAddress([MarshalAs(UnmanagedType.LPStr)] string name);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		delegate IntPtr GlGetString(int name);

		readonly IntPtr glesLibrary;
		readonly IntPtr eglLibrary;
		readonly EglGetProcAddress eglGetProcAddress;

		public AndroidGlesFunctionProbe()
		{
			glesLibrary = NativeLibrary.Load("libGLESv3.so");
			try
			{
				eglLibrary = NativeLibrary.Load("libEGL.so");
				eglGetProcAddress = Marshal.GetDelegateForFunctionPointer<EglGetProcAddress>(
					NativeLibrary.GetExport(eglLibrary, "eglGetProcAddress"));
			}
			catch
			{
				if (eglLibrary != IntPtr.Zero)
					NativeLibrary.Free(eglLibrary);

				NativeLibrary.Free(glesLibrary);
				throw;
			}
		}

		public (int Resolved, string[] Missing, string Version) CheckFunctions()
		{
			var names = Functions.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
			var missing = names.Where(name => Resolve(name) == IntPtr.Zero).ToArray();
			var glGetString = Marshal.GetDelegateForFunctionPointer<GlGetString>(Resolve("glGetString"));
			var version = Marshal.PtrToStringAnsi(glGetString(Android.Opengl.GLES30.GlVersion)) ?? "";
			return (names.Length - missing.Length, missing, version);
		}

		public IntPtr Resolve(string name)
		{
			if (NativeLibrary.TryGetExport(glesLibrary, name, out var function))
				return function;

			return eglGetProcAddress(name);
		}

		public void Dispose()
		{
			NativeLibrary.Free(eglLibrary);
			NativeLibrary.Free(glesLibrary);
		}
	}
}
