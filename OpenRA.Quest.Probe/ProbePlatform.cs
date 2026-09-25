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

using OpenRA.Platforms.Default;
using OpenRA.Primitives;

namespace OpenRA.Quest.Probe
{
	/// <summary>
	/// Connects OpenRA.Renderer to an Android-owned GLES surface for diagnostics.
	/// Input, font and sound integration are still separate porting tasks.
	/// </summary>
	sealed class ProbePlatform : IPlatform
	{
		readonly Size surfaceSize;

		public ProbePlatform(Size surfaceSize) => this.surfaceSize = surfaceSize;

		public IPlatformWindow CreateWindow(Size size, WindowMode windowMode, float scaleModifier,
			int vertexBatchSize, int indexBatchSize, int videoDisplay, GLProfile profile)
			=> new ProbePlatformWindow(surfaceSize);

		public ISoundEngine CreateSound(string device) => new DummySoundEngine();

		public IFont CreateFont(byte[] data)
			=> throw new NotSupportedException("The diagnostic Android platform has no font renderer yet.");
	}

	sealed class ProbePlatformWindow : IPlatformWindow
	{
		readonly Size size;
		readonly ProbeGraphicsContext context = new();

		public ProbePlatformWindow(Size size) => this.size = size;

		public IGraphicsContext Context => context;
		public Size NativeWindowSize => size;
		public Size EffectiveWindowSize => size;
		public float NativeWindowScale => 1;
		public float EffectiveWindowScale => 1;
		public Size SurfaceSize => size;
		public int DisplayCount => 1;
		public int CurrentDisplay => 0;
		public bool HasInputFocus => true;
		public bool IsSuspended => false;
		public GLProfile GLProfile => GLProfile.Embedded;
		public GLProfile[] SupportedGLProfiles => [GLProfile.Embedded];

		public event Action<float, float, float, float> OnWindowScaleChanged
		{
			add { }
			remove { }
		}

		public void PumpInput(IInputHandler inputHandler) { }
		public string GetClipboardText() => "";
		public bool SetClipboardText(string text) => false;
		public bool TryOpenUrl(string url) => false;
		public void GrabWindowMouseFocus() { }
		public void ReleaseWindowMouseFocus() { }
		public IHardwareCursor CreateHardwareCursor(string name, Size size, byte[] data, int2 hotspot, bool pixelDouble)
			=> throw new NotSupportedException("The diagnostic Android platform has no hardware cursor.");
		public void SetHardwareCursor(IHardwareCursor cursor) { }
		public void SetWindowTitle(string title) { }
		public void SetRelativeMouseMode(bool mode) { }
		public void SetScaleModifier(float scale) { }
		public void Dispose() => context.Dispose();
	}

	sealed class ProbeInputHandler : IInputHandler
	{
		public void ModifierKeys(Modifiers mods) { }
		public void OnKeyInput(KeyInput input) { }
		public void OnMouseInput(MouseInput input) { }
		public void OnTextInput(string text) { }
	}
}
