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

#if QUEST_XR
using System.Threading;
using Android.App;
using Com.Friedensbringer.Openra.XR;

namespace OpenRA.Quest.Probe
{
	/// <summary>
	/// Experimental bridge between OpenRA's GL thread and a native OpenXR
	/// session on a second thread. Device behavior remains unverified.
	/// </summary>
	sealed class QuestXrBridge : IDisposable
	{
		const long FrameIntervalMilliseconds = 67;
		static QuestXrBridge? current;

		readonly Activity activity;
		readonly QuestInputQueue input;
		readonly Action<string> onStatus;
		readonly PointerForwarder listener;
		readonly byte[] boardPixels = new byte[XrFrameConverter.BoardWidth * XrFrameConverter.BoardHeight * 4];
		long lastFrameTime;
		long lastFrameLogTime;
		long publishedFrames;
		long surfaceSize;
		int running;
		int disposed;
		long sessionToken;

		public static QuestXrBridge? Current => Volatile.Read(ref current);
		public bool IsRunning => Volatile.Read(ref running) != 0;

		public QuestXrBridge(Activity activity, QuestInputQueue input, Action<string> onStatus)
		{
			this.activity = activity;
			this.input = input;
			this.onStatus = onStatus;
			listener = new PointerForwarder(this);
		}

		public static bool Install(QuestXrBridge bridge)
		{
			if (Current?.IsRunning == true)
				return false;

			Interlocked.Exchange(ref current, bridge)?.Dispose();
			try
			{
				bridge.Start();
				return true;
			}
			catch
			{
				bridge.Dispose();
				throw;
			}
		}

		void Start()
		{
			if (Interlocked.CompareExchange(ref running, 1, 0) != 0)
				return;

			try
			{
				QuestDiagnostics.Write("Native OpenXR-Session wird angelegt.");
				sessionToken = XrProbe.BeginSession();
				XrProbe.SetPointerListener(listener);
				var thread = new Thread(() =>
				{
					string result;
					try
					{
						result = Volatile.Read(ref disposed) != 0
							? "OpenXR-Start abgebrochen."
							: XrProbe.ShowQuad(activity, sessionToken) ?? "OpenXR-Session beendet.";
					}
					catch (Exception e)
					{
						QuestDiagnostics.Error("OpenXR-Session fehlgeschlagen", e);
						result = $"OpenXR-Session fehlgeschlagen: {e.Message}";
					}
					finally
					{
						listener.ReleaseInput();
						Volatile.Write(ref running, 0);
						if (ReferenceEquals(Current, this))
							XrProbe.SetPointerListener(null);
					}

					QuestDiagnostics.Write(result);
					activity.RunOnUiThread(() =>
					{
						if (!activity.IsDestroyed)
							onStatus(result);
					});
				}) { IsBackground = true, Name = "openra-quest-xr" };
				thread.Start();
				onStatus("OpenXR-Fläche startet …");
			}
			catch
			{
				Volatile.Write(ref running, 0);
				if (sessionToken != 0)
					XrProbe.RequestStop(sessionToken);
				XrProbe.SetPointerListener(null);
				throw;
			}
		}

		/// <summary>Called only after a completed OpenRA frame on its GL thread.</summary>
		public void PublishFrame(QuestGameSession session)
		{
			if (Volatile.Read(ref running) == 0 || Volatile.Read(ref disposed) != 0)
				return;

			var now = Environment.TickCount64;
			if (now - lastFrameTime < FrameIntervalMilliseconds)
				return;

			lastFrameTime = now;
			var (pixels, backingWidth, width, height) = session.ReadScreenPixelsBgra();
			XrFrameConverter.ConvertInto(pixels, backingWidth, width, height, boardPixels);
			if (!XrProbe.SubmitFrame(boardPixels))
				throw new InvalidOperationException("OpenXR rejected the OpenRA frame.");
			Volatile.Write(ref surfaceSize, ((long)width << 32) | (uint)height);

			publishedFrames++;
			if (publishedFrames == 1 || now - lastFrameLogTime >= 5000)
			{
				var message = $"XR-Bildübergabe: {publishedFrames} Frames, letzte Quelle {width}x{height}.";
				if (publishedFrames == 1)
					QuestDiagnostics.Write(message);
				else
					Android.Util.Log.Info("OpenRA.Quest.Probe", message);
				lastFrameLogTime = now;
			}
		}

		/// <summary>Reject controller positions until a frame at the new surface size is published.</summary>
		public void InvalidateFrameMapping()
		{
			Volatile.Write(ref surfaceSize, 0);
		}

		public void Dispose()
		{
			if (Interlocked.Exchange(ref disposed, 1) != 0)
				return;

			var ownsCurrent = ReferenceEquals(Interlocked.CompareExchange(ref current, null, this), this);
			if (sessionToken != 0)
				XrProbe.RequestStop(sessionToken);
			listener.ReleaseInput();
			if (ownsCurrent)
				XrProbe.SetPointerListener(null);
		}

		sealed class PointerForwarder(QuestXrBridge owner) : Java.Lang.Object, XrProbe.IPointerListener
		{
			readonly object pointerLock = new();
			int2? lastPosition;

			public void OnPointerEvent(int type, int x, int y)
			{
				lock (pointerLock)
					OnPointerEventCore(type, x, y);
			}

			void OnPointerEventCore(int type, int x, int y)
			{
				if (Volatile.Read(ref owner.disposed) != 0)
					return;

				if (type == XrProbe.PointerShiftOn || type == XrProbe.PointerShiftOff)
				{
					owner.input.SetModifiers(type == XrProbe.PointerShiftOn ? Modifiers.Shift : Modifiers.None);
					return;
				}

				var size = Volatile.Read(ref owner.surfaceSize);
				var width = (int)(size >> 32);
				var height = (int)size;
				if (XrFrameConverter.TryMapBoardPixel(x, y, width, height,
					out var sourceX, out var sourceY))
					lastPosition = new int2(sourceX, sourceY);
				else if (type != XrProbe.PointerUp && type != XrProbe.PointerContextUp)
					return;

				if (lastPosition is not { } position)
					return;

				switch (type)
				{
					case XrProbe.PointerMove:
						owner.input.Move(position);
						break;
					case XrProbe.PointerDown:
						owner.input.Down(position, MouseButton.Left);
						break;
					case XrProbe.PointerUp:
						owner.input.Up(position, MouseButton.Left);
						break;
					case XrProbe.PointerContextDown:
						owner.input.Down(position, MouseButton.Right);
						break;
					case XrProbe.PointerContextUp:
						owner.input.Up(position, MouseButton.Right);
						break;
					case XrProbe.PointerPanDown:
						owner.input.Down(position, MouseButton.Middle);
						break;
					case XrProbe.PointerPanUp:
						owner.input.Up(position, MouseButton.Middle);
						break;
					case XrProbe.PointerScrollUp:
						owner.input.Scroll(position, 2);
						break;
					case XrProbe.PointerScrollDown:
						owner.input.Scroll(position, -2);
						break;
				}
			}

			public void ReleaseInput()
			{
				lock (pointerLock)
				{
					if (lastPosition is { } position)
						owner.input.Up(position);
					owner.input.SetModifiers(Modifiers.None);
					lastPosition = null;
				}
			}
		}
	}
}
#endif
