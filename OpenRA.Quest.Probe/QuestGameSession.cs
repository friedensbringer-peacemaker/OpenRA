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

using OpenRA.Graphics;
using OpenRA.Network;
using OpenRA.Primitives;
using OpenRA.Widgets;

namespace OpenRA.Quest.Probe
{
	/// <summary>
	/// Keeps a local Red Alert game alive on the Android GL thread. This is a
	/// flat-screen stepping stone; it does not create an OpenXR session.
	/// </summary>
	sealed class QuestGameSession : IDisposable
	{
		readonly Renderer? previousRenderer = Game.Renderer;
		readonly ModData? previousModData = Game.ModData;
		readonly Sound? previousSound = Game.Sound;
		readonly OrderManager? previousOrderManager = Game.OrderManager;
		readonly WorldRenderer? previousWorldRenderer = Game.worldRenderer;

		Renderer? renderer;
		ModData? modData;
		Sound? sound;
		OrderManager? orderManager;
		OpenRA.FileSystem.IReadOnlyPackage? mapPackage;
		Map? map;
		World? world;
		WorldRenderer? worldRenderer;
		bool disposed;

		public QuestGameSession(string appFiles, Size size, QuestInputQueue input)
		{
			try
			{
				var settings = new GraphicSettings
				{
					Mode = WindowMode.Windowed,
					WindowedSize = new int2(size.Width, size.Height),
					GLProfile = GLProfile.Embedded
				};

				var platform = new ProbePlatform(size, input);
				renderer = new Renderer(platform, settings, 4096);
				Game.Renderer = renderer;
				var mods = new InstalledMods([Path.Combine(appFiles, "mods")], []);
				if (!mods.TryGetValue("ra", out var manifest))
					throw new InvalidOperationException("The Red Alert mod is unavailable.");

				modData = new ModData(manifest, mods);
				Game.ModData = modData;
				renderer.InitializeFonts(modData);
				sound = new Sound(platform, Game.Settings.Sound);
				Game.Sound = sound;
				orderManager = new OrderManager(new EchoConnection());
				Game.OrderManager = orderManager;

				mapPackage = modData.ModFiles.OpenPackage("ra|maps/blitz.oramap");
				map = new Map(modData, mapPackage);
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

				modData.MapCache.LoadMaps(modData);
				modData.PrepareMap(map);
				renderer.SetMaximumViewportSize(size);
				world = new World(map, modData, orderManager, WorldType.Regular);
				orderManager.World = world;
				worldRenderer = new WorldRenderer(modData, world);
				Game.worldRenderer = worldRenderer;
				world.LoadComplete(worldRenderer);
				orderManager.StartGame();
				worldRenderer.RefreshPalette();
				world.PostLoadComplete(worldRenderer);
				worldRenderer.Viewport.Center(map.CenterOfCell(world.LocalPlayer.HomeLocation));
				Ui.LastTickTime.Value = Game.RunTime;
				Android.Util.Log.Info("OpenRA.Quest.Probe", "Fortlaufende lokale OpenRA-Spielsession initialisiert.");
			}
			catch
			{
				Dispose();
				throw;
			}
		}

		public void TickAndRender()
		{
			ObjectDisposedException.ThrowIf(disposed, this);
			if (renderer == null || worldRenderer == null || orderManager == null || sound == null)
				throw new InvalidOperationException("The OpenRA game session is incomplete.");

			var world = worldRenderer.World;
			var now = Game.RunTime;
			Game.PerformDelayedActions();
			if (Ui.LastTickTime.ShouldAdvance(now))
			{
				Ui.LastTickTime.AdvanceTickTime(now);
				Sync.RunUnsynced(world, Ui.Tick);
			}

			for (var ticks = 0; ticks < 5 && orderManager.LastTickTime.ShouldAdvance(now); ticks++)
			{
				orderManager.LastTickTime.AdvanceTickTime(now);
				sound.Tick();
				Sync.RunUnsynced(world, orderManager.TickImmediate);
				if (!orderManager.TryTick())
					break;

				Sync.RunUnsynced(world, () => world.OrderGenerator.Tick(world));
				world.Tick();
				Sync.RunUnsynced(world, () => world.TickRender(worldRenderer));
			}

			worldRenderer.BeginFrame();
			worldRenderer.Viewport.Tick();
			worldRenderer.PrepareRenderables();
			Ui.PrepareRenderables();
			worldRenderer.EndFrame();
			renderer.BeginWorld(worldRenderer.Viewport.CenterLocation, worldRenderer.Viewport.ViewportSize);
			sound.SetListenerPosition(worldRenderer.Viewport.CenterPosition);
			worldRenderer.Draw();
			renderer.BeginUI();
			worldRenderer.DrawAnnotations();
			Ui.Draw();
			renderer.EndFrame(new DefaultInputHandler(world));
		}

		public void Dispose()
		{
			if (disposed)
				return;

			disposed = true;
			DisposeSafely(worldRenderer, "WorldRenderer");
			if (worldRenderer == null)
			{
				DisposeSafely(world, "World");
				if (world == null)
					DisposeSafely(map, "Map");
			}

			DisposeSafely(mapPackage, "map package");
			DisposeSafely(orderManager, "OrderManager");
			DisposeSafely(sound, "Sound");
			DisposeSafely(modData, "ModData");
			DisposeSafely(renderer, "Renderer");
			Game.worldRenderer = previousWorldRenderer;
			Game.OrderManager = previousOrderManager;
			Game.Sound = previousSound;
			Game.ModData = previousModData;
			Game.Renderer = previousRenderer;
		}

		static void DisposeSafely(IDisposable? item, string name)
		{
			if (item == null)
				return;

			try { item.Dispose(); }
			catch (Exception e)
			{
				Android.Util.Log.Warn("OpenRA.Quest.Probe", $"{name} konnte nicht vollständig freigegeben werden: {e}");
			}
		}
	}
}
