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
using System.IO;
using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Support;
using OpenRA.Widgets;

namespace OpenRA
{
	/// <summary>
	/// Entry points for hosts that own the render loop themselves (e.g. an Android GL surface
	/// driving an XR headset). They run the same startup and frame logic as <see cref="Run"/>,
	/// but split into steps so the host can stay responsive and draw its own loading screen.
	/// </summary>
	public static partial class Game
	{
		/// <summary>Creates the handler that receives input pumped from the platform window.</summary>
		public static Func<World, IInputHandler> InputHandlerFactory = world => new DefaultInputHandler(world);

		static IInputHandler CreateInputHandler(World world) => InputHandlerFactory(world);

		static long embeddedNextLogic;
		static long embeddedNextRender;

		/// <summary>
		/// Mirrors <see cref="Initialize"/> and <see cref="InitializeMod"/> with a host-provided platform.
		/// Each yielded step names the stage that just finished. <paramref name="configureSettings"/>
		/// runs right after settings.yaml is loaded, before anything reads it.
		/// </summary>
		public static IEnumerator<string> InitializeEmbedded(Arguments args, IPlatform platform, string modId,
			Action<Settings> configureSettings)
		{
			try
			{
				EngineVersion = File.ReadAllText(Path.Combine(Platform.EngineDir, "VERSION")).Trim();
			}
			catch
			{
				EngineVersion = "Unknown";
			}

			// The host process may be reused after "Exit" (Android keeps it alive): start clean.
			state = RunStatus.Running;
			worldRenderer?.Dispose();
			worldRenderer = null;
			server?.Shutdown();
			OrderManager?.Dispose();
			if (ModData != null)
			{
				ModData.ModFiles.UnmountAll();
				ModData.Dispose();
				ModData = null;
			}

			// The audio device is independent of the GL context and would otherwise stay open twice.
			// An old Renderer belongs to a GL context that no longer exists and is only dropped.
			Sound?.Dispose();
			Sound = null;
			Renderer = null;

			InitializeSettings(args);
			configureSettings?.Invoke(Settings);

			Log.AddChannel("perf", "perf.log");
			Log.AddChannel("debug", "debug.log");
			Log.AddChannel("server", "server.log", true);
			Log.AddChannel("sound", "sound.log");
			Log.AddChannel("graphics", "graphics.log");
			Log.AddChannel("geoip", "geoip.log");
			Log.AddChannel("nat", "nat.log");
			Log.AddChannel("client", "client.log");

			Mods = new InstalledMods([Path.Combine(Platform.EngineDir, "mods")], []);
			ExternalMods = new ExternalMods();
			if (!Mods.TryGetValue(modId, out var manifest))
				throw new InvalidOperationException($"Unknown or invalid mod '{modId}'.");

			Renderer = new Renderer(platform, Settings.Graphics, manifest.RendererConstants.VertexBatchSize);
			Sound = new Sound(platform, Settings.Sound);
			yield return "Renderer und Ton";

			// Same as InitializeMod, split at the expensive stages.
			LobbyInfoChanged = () => { };
			ConnectionStateChanged = (om, p, conn) => { };
			BeforeGameStart = () => { };
			OnRemoteDirectConnect = endpoint => { };
			delayedActions = new ActionQueue();
			Ui.ResetAll();

			ModData = new ModData(manifest, Mods, true);
			LocalPlayerProfile = new LocalPlayerProfile(
				Path.Combine(Platform.SupportDir, Settings.Game.AuthProfile), ModData.GetOrCreate<PlayerDatabase>());
			yield return "Moddaten";

			if (!ModData.LoadScreen.BeforeLoad(ModData))
				throw new InvalidOperationException("The mod content is incomplete; import the game data first.");

			ModData.InitializeLoaders(ModData.DefaultFileSystem);
			Renderer.InitializeFonts(ModData);
			yield return "Schriften";

			using (new PerfTimer("LoadMaps"))
				ModData.MapCache.LoadMaps(ModData);
			yield return "Kartenliste";

			Cursor?.Dispose();
			Cursor = new CursorManager(ModData);
			PerfHistory.Items["render"].HasNormalTick = false;
			PerfHistory.Items["batches"].HasNormalTick = false;
			PerfHistory.Items["render_world"].HasNormalTick = false;
			PerfHistory.Items["render_widgets"].HasNormalTick = false;
			PerfHistory.Items["render_flip"].HasNormalTick = false;
			PerfHistory.Items["terrain_lighting"].HasNormalTick = false;

			JoinLocal();

			// Loads the shellmap and opens the main menu (or a launch map / replay from args).
			ModData.LoadScreen.StartGame(args);
			embeddedNextLogic = embeddedNextRender = RunTime;
			yield return "Hauptmenü";
		}

		/// <summary>
		/// One iteration of <see cref="Loop"/>: runs the logic ticks that are due and renders when
		/// the frame rate cap allows. Returns false once the game requested to exit.
		/// </summary>
		public static bool EmbeddedFrame()
		{
			const int MaxLogicTicksBehind = 250;
			const int MaxTicksPerFrame = 3;

			if (state != RunStatus.Running)
				return false;

			var logicInterval = Ui.Timestep;
			var logicWorld = worldRenderer?.World;
			if (logicWorld != null && (!logicWorld.IsReplay || logicWorld.ReplayTimestep != 0))
				logicInterval = logicWorld == OrderManager.World ? OrderManager.SuggestedTimestep : logicWorld.Timestep;

			var now = RunTime;
			if (now - embeddedNextLogic > MaxLogicTicksBehind)
				embeddedNextLogic = now;

			for (var ticks = 0; ticks < MaxTicksPerFrame && now >= embeddedNextLogic; ticks++)
			{
				embeddedNextLogic += Math.Max(1, logicInterval);
				LogicTick();
				if (state != RunStatus.Running)
					return false;
			}

			var renderInterval = Settings.Graphics.CapFramerate ? 1000 / Settings.Graphics.MaxFramerate.Clamp(1, 1000) : 0;
			if (now >= embeddedNextRender && !Renderer.WindowIsSuspended)
			{
				embeddedNextRender = now + renderInterval;
				RenderTick();
			}

			return state == RunStatus.Running;
		}

		/// <summary>Releases what <see cref="InitializeEmbedded"/> created, like the end of <see cref="Run"/>.</summary>
		public static void ShutdownEmbedded()
		{
			try
			{
				OrderManager?.Dispose();
				CloseServer();
			}
			finally
			{
				worldRenderer?.Dispose();
				worldRenderer = null;
				ModData?.Dispose();
				ModData = null;
				ChromeProvider.Deinitialize();
				Sound?.Dispose();
				Sound = null;
				Renderer?.Dispose();
				Renderer = null;
				state = RunStatus.Running;
			}
		}
	}
}
