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
using Android.Graphics;
using Com.Friedensbringer.Openra.XR;
using OpenRA.Mods.Common.Widgets.Logic;

namespace OpenRA.Quest.Probe
{
	/// <summary>
	/// Experimental bridge between OpenRA's GL thread and a native OpenXR
	/// session on a second thread. Device behavior remains unverified.
	/// </summary>
	sealed class QuestXrBridge : IDisposable
	{
		const long FrameIntervalMilliseconds = 33;
		static QuestXrBridge? current;

		readonly Activity activity;
		readonly QuestInputQueue input;
		readonly Action<string> onStatus;
		readonly PointerForwarder listener;
		readonly QuestPointerStyle pointerStyle;
		readonly byte[] boardPixels = new byte[XrFrameConverter.BoardWidth * XrFrameConverter.BoardHeight * 4];

		// Separate buffer: cards are drawn on the UI thread while game frames come from the GL thread.
		readonly byte[] cardPixels = new byte[XrFrameConverter.BoardWidth * XrFrameConverter.BoardHeight * 4];
		long lastFrameTime;
		long lastFrameLogTime;
		long publishedFrames;
		int seeThroughShroud;
		bool[] seeThroughMask = [];
		byte[] seeThroughMaskBytes = [];
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
			pointerStyle = new QuestPointerStyle(activity);

			// Hooks for the VR tab in OpenRA's settings menu (VrSettingsLogic).
			VrRuntime.Available = true;
			VrRuntime.Apply = ApplySettings;
			VrRuntime.Recenter = XrProbe.RequestRecenter;
			VrRuntime.Deploy = () => input.KeyTap(Keycode.F);
			VrRuntime.OpenGameMenu = () => input.KeyTap(Keycode.ESCAPE);
			VrRuntime.PassthroughAvailable = () => XrProbe.IsPassthroughAvailable;
		}

		void ApplySettings(VrSettings settings)
		{
			pointerStyle.Apply(settings);
			XrProbe.SetBoardLayout(settings.BoardDistance, settings.BoardWidth, settings.BoardHeightOffset);
			XrProbe.SetPassthrough(settings.PassthroughMode, settings.PassthroughOpacity, settings.PassthroughLook);
			Volatile.Write(ref seeThroughShroud, settings.PassthroughMode == 2 ? 1 : 0);
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
				pointerStyle.ApplyCached();
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
		public void PublishFrame(IQuestGame session)
		{
			if (Volatile.Read(ref running) == 0 || Volatile.Read(ref disposed) != 0)
				return;

			var now = Environment.TickCount64;
			if (now - lastFrameTime < FrameIntervalMilliseconds)
				return;

			lastFrameTime = now;

			// Stored VR settings become available with the game's ModData; apply them once.
			if (publishedFrames == 0 && Game.ModData != null)
				ApplySettings(Game.ModData.GetSettings<VrSettings>());

			var (texture, width, height) = session.ScreenTexture;

			// Passthrough mode 2: unexplored (black) map areas become transparent so the room shows through.
			const int Block = QuestGameSession.SeeThroughBlock;
			var blocksX = (width + Block - 1) / Block;
			var blocksY = (height + Block - 1) / Block;
			if (seeThroughMask.Length != blocksX * blocksY)
			{
				seeThroughMask = new bool[blocksX * blocksY];
				seeThroughMaskBytes = new byte[blocksX * blocksY];
			}

			var useMask = Volatile.Read(ref seeThroughShroud) != 0 &&
				session.TryComputeSeeThroughMask(seeThroughMask, blocksX, blocksY);

			// Fast path (PERF-001): asynchronous native readback of the screen texture, conversion in C++.
			if (texture is OpenRA.Platforms.Default.ITextureInternal internalTexture &&
				width == XrFrameConverter.BoardWidth && height == XrFrameConverter.BoardHeight)
			{
				if (useMask)
					for (var i = 0; i < seeThroughMask.Length; i++)
						seeThroughMaskBytes[i] = seeThroughMask[i] ? (byte)1 : (byte)0;

				if (!XrProbe.CaptureFrame((int)internalTexture.ID, width, height,
					useMask ? seeThroughMaskBytes : null, blocksX, Block))
					return; // The first readback is still in flight.
			}
			else
			{
				var (pixels, backingWidth, readWidth, readHeight) = session.ReadScreenPixelsBgra();
				XrFrameConverter.ConvertInto(pixels, backingWidth, readWidth, readHeight, boardPixels,
					useMask ? seeThroughMask : null, blocksX, Block);
				if (!XrProbe.SubmitFrame(boardPixels))
					throw new InvalidOperationException("OpenXR rejected the OpenRA frame.");
			}

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

		/// <summary>Replace the stale game frame with an unmistakable error screen after a session failure.</summary>
		public void PublishFailure(string message)
		{
			if (!IsRunning || Volatile.Read(ref disposed) != 0)
				return;

			var submitted = SubmitCard(Color.Rgb(44, 13, 20), (canvas, text) =>
			{
				text.TextSize = 58;
				canvas.DrawText("OpenRA-Partie angehalten", 72, 180, text);
				text.TextSize = 31;
				canvas.DrawText("Ein Fehler im Quest-Testport hat das Spiel beendet.", 72, 260, text);
				var detail = message.Replace('\n', ' ').Replace('\r', ' ');
				if (detail.Length > 70)
					detail = detail[..67] + "...";
				canvas.DrawText(detail, 72, 325, text);
				canvas.DrawText("Bitte die App neu starten. Der Fehler wurde protokolliert.", 72, 410, text);
			});

			if (submitted)
				QuestDiagnostics.Write("XR-Fehlerbild nach angehaltener Partie übertragen.");
		}

		/// <summary>
		/// Shows what is loading and that it takes a while, so nobody quits during the long first start.
		/// Called once per second on the UI thread until the first game frame replaces it.
		/// </summary>
		public void PublishLoading(TimeSpan elapsed)
		{
			if (!IsRunning || Volatile.Read(ref disposed) != 0 || publishedFrames > 0)
				return;

			var seconds = (int)elapsed.TotalSeconds;
			SubmitCard(Color.Rgb(18, 22, 28), (canvas, text) =>
			{
				text.FakeBoldText = true;
				text.TextSize = 96;
				canvas.DrawText("xr.openra", 90, 190, text);
				text.FakeBoldText = false;
				text.TextSize = 38;
				text.Color = Color.Rgb(214, 170, 90);
				canvas.DrawText("Red Alert auf OpenRA – Echtzeitstrategie in VR", 94, 250, text);

				text.Color = Color.White;
				text.TextSize = 36;
				canvas.DrawText("Das Spiel wird geladen. Das dauert etwa 30 Sekunden.", 94, 350, text);
				canvas.DrawText("Bitte die App nicht beenden, auch wenn sich scheinbar nichts tut.", 94, 400, text);

				using var bar = new Paint { Color = Color.Rgb(58, 64, 74) };
				canvas.DrawRect(94, 450, 1186, 474, bar);
				bar.Color = Color.Rgb(214, 170, 90);
				canvas.DrawRect(94, 450, 94 + 1092 * Math.Min(0.95f, seconds / 30f), 474, bar);
				text.TextSize = 30;
				canvas.DrawText($"Lädt seit {seconds} s …", 94, 520, text);

				text.Color = Color.Rgb(170, 178, 190);
				text.TextSize = 28;
				canvas.DrawText("Bedienung: rechter Trigger = auswählen · A = Befehl · Griff halten = Karte ziehen", 94, 610, text);
				canvas.DrawText("rechter Stick = Zoom · X = Bauhof entfalten · linke Menütaste = Schnellmenü", 94, 650, text);

				text.TextAlign = Paint.Align.Right;
				canvas.DrawText($"xr.openra {AppVersion}", 1240, 770, text);
				text.TextAlign = Paint.Align.Left;
			});
		}

		string AppVersion
		{
			get
			{
				try { return activity.PackageManager?.GetPackageInfo(activity.PackageName!, 0)?.VersionName ?? ""; }
				catch (Exception) { return ""; }
			}
		}

		/// <summary>Draws a full-board 2D card with Android's Canvas and submits it to the XR quad.</summary>
		bool SubmitCard(Color background, Action<Canvas, Paint> draw)
		{
			var width = XrFrameConverter.BoardWidth;
			var height = XrFrameConverter.BoardHeight;
			using var bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Argb8888!);
			using var canvas = new Canvas(bitmap!);
			using var text = new Paint(PaintFlags.AntiAlias) { Color = Color.White };
			canvas.DrawColor(background);
			draw(canvas, text);

			var argb = new int[width * height];
			bitmap!.GetPixels(argb, 0, width, 0, 0, width, height);
			lock (cardPixels)
			{
				for (var y = 0; y < height; y++)
				for (var x = 0; x < width; x++)
				{
					var pixel = argb[y * width + x];
					var offset = ((height - 1 - y) * width + x) * 4;
					cardPixels[offset] = (byte)(pixel >> 16);
					cardPixels[offset + 1] = (byte)(pixel >> 8);
					cardPixels[offset + 2] = (byte)pixel;
					cardPixels[offset + 3] = 255;
				}

				return XrProbe.SubmitFrame(cardPixels);
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

				// The quick menu is an OpenRA widget (VrQuickMenuLogic); widget calls run on the game thread.
				if (type == XrProbe.PointerMenuToggle)
				{
					ReleaseInput();
					Game.RunAfterTick(() => VrQuickMenuLogic.Toggle(Game.worldRenderer));
					QuestDiagnostics.Write("XR-Schnellmenü umgeschaltet.");
					return;
				}

				if (type == XrProbe.PointerDeploy)
				{
					Game.RunAfterTick(VrQuickMenuLogic.CloseMenu);
					owner.input.KeyTap(Keycode.F);
					QuestDiagnostics.Write("Deploy-Befehl vom linken Controller gesendet.");
					return;
				}

				if (VrQuickMenuLogic.IsOpen)
				{
					// Stick, A and B work even when the ray misses the menu; the ray itself
					// still clicks the menu buttons like a mouse (handled below).
					switch (type)
					{
						case XrProbe.PointerScrollUp:
							Game.RunAfterTick(() => VrQuickMenuLogic.Navigate(-1));
							return;
						case XrProbe.PointerScrollDown:
							Game.RunAfterTick(() => VrQuickMenuLogic.Navigate(1));
							return;
						case XrProbe.PointerMenuSelect:
							Game.RunAfterTick(VrQuickMenuLogic.ActivateFocused);
							return;
						case XrProbe.PointerMenuBack:
							Game.RunAfterTick(VrQuickMenuLogic.CloseMenu);
							return;
						case XrProbe.PointerContextDown:
						case XrProbe.PointerContextUp:
						case XrProbe.PointerShiftOn:
						case XrProbe.PointerShiftOff:
							return;
					}
				}

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
