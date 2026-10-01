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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using OpenRA.FileFormats;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Platforms.Default;
using OpenRA.Widgets;

namespace OpenRA.Quest.DesktopTest
{
	/// <summary>
	/// Runs the Quest start path (Game.InitializeEmbedded + EmbeddedFrame) on the desktop with the
	/// VR UI enabled, walks through menu, VR settings, VR quick menu, a game, control groups,
	/// the VR keyboard and the passthrough see-through mask, and saves screenshots of each step.
	/// Usage: OpenRA.Quest.DesktopTest &lt;engine dir&gt; &lt;support dir with Content/ra/v2&gt; &lt;output dir&gt;
	/// </summary>
	static class Program
	{
		static string outputDir;
		static readonly List<string> Report = [];
		static int failures;

		static int Main(string[] args)
		{
			if (args.Length != 3)
			{
				Console.WriteLine("Usage: OpenRA.Quest.DesktopTest <engine dir> <support dir> <output dir>");
				return 2;
			}

			AppDomain.CurrentDomain.UnhandledException += (_, e) => Console.Error.WriteLine($"UNHANDLED: {e.ExceptionObject}");
			Log("Start");
			Platform.OverrideEngineDir(args[0]);
			Platform.OverrideSupportDir(args[1]);
			outputDir = args[2];
			Directory.CreateDirectory(outputDir);

			// Pretend to be the XR host so every VR widget is active.
			VrRuntime.Available = true;
			VrRuntime.PassthroughAvailable = () => true;

			VrRuntime.Apply = _ => { };
			VrRuntime.Recenter = () => Log("Hook: Recenter");

			try
			{
				Run();
			}
			catch (Exception e)
			{
				failures++;
				Log($"FEHLER: {e}");
			}
			finally
			{
				try { Game.ShutdownEmbedded(); }
				catch (Exception e) { Log($"Shutdown: {e.Message}"); }

				File.WriteAllLines(Path.Combine(outputDir, "report.txt"), Report);
			}

			Console.WriteLine(failures == 0 ? "ALLE PRÜFUNGEN BESTANDEN" : $"{failures} PRÜFUNG(EN) FEHLGESCHLAGEN");
			return failures == 0 ? 0 : 1;
		}

		static void Run()
		{
			Log("Erzeuge Plattform");
			var platform = new DefaultPlatform();
			Log("Plattform erzeugt");
			if (Environment.GetEnvironmentVariable("DESKTOPTEST_PROBE") == "1")
			{
				Game.InitializeSettings(new Arguments());
				Log("Settings ok");
				var sound = new Sound(platform, Game.Settings.Sound);
				Log("Sound ok");
				var renderer = new Renderer(platform, new GraphicSettings { Mode = WindowMode.Windowed, WindowedSize = new int2(1280, 800) }, 4096);
				Log("Renderer ok");
				return;
			}
			// 1. Staged start exactly like the Quest app, with its headset settings.
			var steps = Game.InitializeEmbedded(new Arguments(), platform, "ra", settings =>
			{
				settings.Graphics.Mode = WindowMode.Windowed;
				settings.Graphics.WindowedSize = new int2(1280, 800);
				settings.Graphics.CapFramerate = true;
				settings.Graphics.MaxFramerate = 30;
				settings.Graphics.DisableHardwareCursors = true;
				settings.Game.MouseControlStyle = MouseControlStyle.Modern;
				settings.Game.FetchNews = false;
				settings.Game.EnableDiscordService = false;
				settings.Game.IntroductionPromptVersion = int.MaxValue;
				settings.Debug.CheckVersion = false;
				settings.Debug.SendSystemInformation = false;
				settings.Debug.SystemInformationVersionPrompt = int.MaxValue;
			});

			var watch = Stopwatch.StartNew();
			while (steps.MoveNext())
			{
				Log($"Ladeschritt {steps.Current}: {watch.ElapsedMilliseconds} ms");
				watch.Restart();
			}

			Check("main menu open", Frames(10000, () => Ui.Root.GetOrNull("MAINMENU")?.IsVisible() == true));
			Screenshot("01-hauptmenue");

			// 2. VR tab in OpenRA's settings menu.
			var wr = Game.worldRenderer;
			var settingsPanel = Ui.OpenWindow("SETTINGS_PANEL", new WidgetArgs
			{
				{ "world", wr.World }, { "worldRenderer", wr }, { "onExit", (Action)(() => { }) }
			});
			var vrTab = settingsPanel.Get("SETTINGS_TAB_CONTAINER").GetOrNull<ButtonWidget>("VR_PANEL");
			Check("VR tab exists in settings", vrTab != null);
			vrTab?.OnClick();
			Frames(600);
			Check("VR panel visible after tab click", settingsPanel.GetOrNull("VR_PANEL")?.IsVisible() == true);
			Screenshot("02-einstellungen-vr");
			Ui.CloseWindow();
			Frames(300);

			// 3. VR quick menu (left menu button on the Quest).
			VrQuickMenuLogic.Toggle(Game.worldRenderer);
			Frames(600);
			Check("quick menu open", VrQuickMenuLogic.IsOpen);
			Screenshot("03-vr-menue");
			VrQuickMenuLogic.Navigate(1);
			VrQuickMenuLogic.Navigate(1);
			Frames(200);
			Screenshot("03b-vr-menue-stickfokus");
			VrQuickMenuLogic.CloseMenu();
			Frames(300);
			Check("quick menu closed", !VrQuickMenuLogic.IsOpen);

			// 4. A game on Blitz through the local server (like "Launch.Map").
			Game.LoadMap("blitz.oramap");
			Check("game started", Frames(30000, () =>
				Game.OrderManager.World?.Type == WorldType.Regular && Game.OrderManager.World.LocalPlayer != null &&
				Game.OrderManager.LocalFrameNumber > 20));
			Frames(1500);
			Screenshot("04-partie");

			var world = Game.OrderManager.World;
			var groupButton = Ui.Root.GetOrNull<ButtonWidget>("GROUP_1");
			Check("control group bar visible", groupButton?.IsVisible() == true && Ui.Root.GetOrNull("VR_CONTROL_GROUPS")?.IsVisible() == true);

			// 5. Control groups: select the MCV, store it in group 1, label shows the count.
			var mcv = world.Actors.FirstOrDefault(a => a.Owner == world.LocalPlayer && a.Info.Name == "mcv");
			Check("MCV present", mcv != null);
			if (mcv != null)
			{
				world.Selection.Combine(world, [mcv], false, true);
				world.ControlGroups.CreateControlGroup(0);
				world.Selection.Clear();
				Frames(300);
				Check("group 1 label counts 1 unit", groupButton?.GetText() == "1 (1)");
				groupButton?.OnMouseDown(default);
				groupButton?.OnMouseUp(default);
				Frames(100);
				Check("tapping group 1 selects the MCV", world.Selection.Contains(mcv));
			}

			Screenshot("05-gruppen");

			// 6. Passthrough see-through mask over unexplored areas.
			const int Block = VrSeeThroughMask.Block;
			var (texture, width, height) = Game.Renderer.ScreenTexture;
			var blocksX = (width + Block - 1) / Block;
			var blocksY = (height + Block - 1) / Block;
			var mask = new bool[blocksX * blocksY];
			var computed = VrSeeThroughMask.Compute(Game.worldRenderer, mask, blocksX, blocksY);
			var share = mask.Count(m => m) * 100.0 / mask.Length;
			Check("see-through mask computed in game", computed);
			Check("mask marks some unexplored area", share > 1);
			Log($"Maske: {share:F1} % der Blöcke unerkundet");
			SaveMaskOverlay(mask, blocksX, "06-maske");

			var maskWatch = Stopwatch.StartNew();
			for (var i = 0; i < 30; i++)
				VrSeeThroughMask.Compute(Game.worldRenderer, mask, blocksX, blocksY);
			Log($"Maske berechnen: {maskWatch.Elapsed.TotalMilliseconds / 30:F2} ms pro Bild (Desktop)");

			// 7. VR keyboard: open the chat (Enter) and type through the on-screen keys.
			Ui.HandleKeyPress(new KeyInput { Event = KeyInputEvent.Down, Key = Keycode.RETURN });
			Ui.HandleKeyPress(new KeyInput { Event = KeyInputEvent.Up, Key = Keycode.RETURN });
			Frames(300);
			var chatField = Ui.KeyboardFocusWidget as TextFieldWidget;
			Check("chat text field focused", chatField != null);
			var keyboard = Ui.Root.GetOrNull("VR_KEYBOARD");
			Check("VR keyboard shown", keyboard?.IsVisible() == true);
			if (keyboard != null && chatField != null)
			{
				foreach (var letter in new[] { "h", "i" })
					keyboard.Children.OfType<ButtonWidget>().First(b => b.GetText() == letter).OnClick();
				Frames(200);
				Check("typed 'hi' into the chat field", chatField.Text == "hi");
				Screenshot("07-tastatur");
				keyboard.Children.OfType<ButtonWidget>().Last().OnClick(); // Close
				Frames(300);
				Check("keyboard closed", Ui.Root.GetOrNull("VR_KEYBOARD") == null);
			}

			// 8. Frame cost with the VR widgets active (desktop reference, not Quest numbers).
			Game.Settings.Graphics.CapFramerate = false;
			var frameWatch = Stopwatch.StartNew();
			var before = Game.RenderFrame;
			Frames(3000);
			var rendered = Game.RenderFrame - before;
			Log($"Desktop: {rendered / frameWatch.Elapsed.TotalSeconds:F0} gerenderte Bilder/s ohne Limit");
		}

		static bool Frames(int milliseconds, Func<bool> until = null)
		{
			var watch = Stopwatch.StartNew();
			while (watch.ElapsedMilliseconds < milliseconds)
			{
				VrKeyboardLogic.Update(Game.worldRenderer?.World);
				if (!Game.EmbeddedFrame())
					throw new InvalidOperationException("OpenRA requested exit during the test.");

				if (until != null && until())
					return true;

				Thread.Sleep(1);
			}

			return until == null;
		}

		static void Screenshot(string name)
		{
			var (pixels, backingWidth, width, height) = Game.Renderer.ReadScreenPixelsBgra();
			var data = new byte[4 * width * height];
			for (var y = 0; y < height; y++)
				Array.Copy(pixels, 4 * y * backingWidth, data, 4 * y * width, 4 * width);

			new Png(data, SpriteFrameType.Bgra32, width, height).Save(Path.Combine(outputDir, name + ".png"));
		}

		static void SaveMaskOverlay(bool[] mask, int blocksX, string name)
		{
			var (pixels, backingWidth, width, height) = Game.Renderer.ReadScreenPixelsBgra();
			var data = new byte[4 * width * height];
			for (var y = 0; y < height; y++)
			{
				for (var x = 0; x < width; x++)
				{
					var s = 4 * (y * backingWidth + x);
					var d = 4 * (y * width + x);
					var black = pixels[s] <= 10 && pixels[s + 1] <= 10 && pixels[s + 2] <= 10;
					var marked = mask[y / VrSeeThroughMask.Block * blocksX + x / VrSeeThroughMask.Block];

					// Magenta = would show the room (marked and black); other pixels unchanged.
					if (marked && black)
					{
						data[d] = 255; data[d + 1] = 0; data[d + 2] = 255;
					}
					else
					{
						data[d] = pixels[s]; data[d + 1] = pixels[s + 1]; data[d + 2] = pixels[s + 2];
					}

					data[d + 3] = 255;
				}
			}

			new Png(data, SpriteFrameType.Bgra32, width, height).Save(Path.Combine(outputDir, name + ".png"));
		}

		static void Check(string what, bool ok)
		{
			if (!ok)
				failures++;

			Log($"{(ok ? "OK  " : "FAIL")} {what}");
		}

		static void Log(string line)
		{
			Console.WriteLine(line);
			Report.Add(line);
		}
	}
}
