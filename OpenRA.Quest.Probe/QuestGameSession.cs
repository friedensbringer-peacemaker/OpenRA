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
using OpenRA.Mods.Common.Orders;
using OpenRA.Network;
using OpenRA.Primitives;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Quest.Probe
{
	/// <summary>
	/// Keeps a local Red Alert game alive on the Android GL thread. This is a
	/// flat-screen stepping stone; it does not create an OpenXR session.
	/// </summary>
	sealed class QuestGameSession : IQuestGame
	{
		const string BotType = "normal";

		readonly Renderer? previousRenderer = Game.Renderer;
		readonly ModData? previousModData = Game.ModData;
		readonly LocalPlayerProfile? previousLocalPlayerProfile = Game.LocalPlayerProfile;
		readonly Sound? previousSound = Game.Sound;
		readonly OrderManager? previousOrderManager = Game.OrderManager;
		readonly WorldRenderer? previousWorldRenderer = Game.worldRenderer;
		readonly MouseControlStyle previousMouseControlStyle = Game.Settings.Game.MouseControlStyle;
		readonly MouseScrollType previousMouseScroll = Game.Settings.Game.MouseScroll;
		readonly bool previousAlternateScrollButton = Game.Settings.Game.UseAlternateScrollButton;

		Renderer? renderer;
		ModData? modData;
		Sound? sound;
		OrderManager? orderManager;
		OpenRA.FileSystem.IReadOnlyPackage? mapPackage;
		Map? map;
		World? world;
		WorldRenderer? worldRenderer;
		McvDoubleClickInputHandler? inputHandler;
		long lastProgressLogTime;
		bool disposed;

		readonly string appFiles;
		readonly Size size;
		readonly QuestInputQueue input;

		/// <summary>Cheap; call <see cref="LoadSteps"/> once per frame until it completes.</summary>
		public QuestGameSession(string appFiles, Size size, QuestInputQueue input)
		{
			this.appFiles = appFiles;
			this.size = size;
			this.input = input;
		}

		/// <summary>
		/// Builds the game in stages on the GL thread. Each step ends one frame, so Android lifecycle
		/// callbacks that wait for the GL thread are not blocked for the whole 8-10 s load (STAB-002).
		/// The caller disposes the session if a step throws.
		/// </summary>
		public IEnumerator<string> LoadSteps()
		{
			Game.Settings.Game.MouseControlStyle = MouseControlStyle.Modern;
			Game.Settings.Game.MouseScroll = MouseScrollType.Standard;
			Game.Settings.Game.UseAlternateScrollButton = false;
			var settings = new GraphicSettings
			{
				Mode = WindowMode.Windowed,
				WindowedSize = new int2(size.Width, size.Height),
				GLProfile = GLProfile.Embedded
			};

			var platform = new ProbePlatform(size, input);
			renderer = new Renderer(platform, settings, 4096);
			Game.Renderer = renderer;
			yield return "Renderer";
			var mods = new InstalledMods([Path.Combine(appFiles, "mods")], []);
			if (!mods.TryGetValue("ra", out var manifest))
				throw new InvalidOperationException("The Red Alert mod is unavailable.");

			modData = new ModData(manifest, mods);
			Game.ModData = modData;
			yield return "Moddaten";
			Game.LocalPlayerProfile = new LocalPlayerProfile(
				Path.Combine(appFiles, Game.Settings.Game.AuthProfile),
				modData.GetOrCreate<PlayerDatabase>());
			renderer.InitializeFonts(modData);
			sound = new Sound(platform, Game.Settings.Sound);
			Game.Sound = sound;
			yield return "Schriften und Ton";
			orderManager = new OrderManager(new EchoConnection());
			Game.OrderManager = orderManager;

			mapPackage = modData.ModFiles.OpenPackage("ra|maps/blitz.oramap");
			map = new Map(modData, mapPackage);
			orderManager.LobbyInfo.GlobalSettings.Map = map.Uid;
			orderManager.LobbyInfo.Slots.Add("Multi0", new Session.Slot { PlayerReference = "Multi0" });
			orderManager.LobbyInfo.Slots.Add("Multi1", new Session.Slot { PlayerReference = "Multi1", AllowBots = true });
			var localClientId = orderManager.Connection.LocalClientId;
			orderManager.LobbyInfo.Clients.Add(new Session.Client
			{
				Index = localClientId,
				Name = "Quest-Probe",
				Slot = "Multi0",
				Faction = "Random",
				Color = Game.Settings.Player.Color,
				PreferredColor = Game.Settings.Player.Color,
				SpawnPoint = 1,
				IsAdmin = true,
				State = Session.ClientState.Ready
			});

			yield return "Karte";
			modData.MapCache.LoadMaps(modData);
			yield return "Kartenliste";
			modData.PrepareMap(map);
			yield return "Kartenregeln";
			var mapPreview = modData.MapCache[map.Uid];
			var lobbyOptions = orderManager.LobbyInfo.GlobalSettings.LobbyOptions;
			foreach (var option in mapPreview.PlayerActorInfo.TraitInfos<ILobbyOptions>()
				.Concat(mapPreview.WorldActorInfo.TraitInfos<ILobbyOptions>())
				.SelectMany(info => info.LobbyOptions(mapPreview)))
				lobbyOptions[option.Id] = new Session.LobbyOptionState
				{
					IsLocked = option.IsLocked,
					Value = option.DefaultValue,
					PreferredValue = option.DefaultValue
				};

			if (!lobbyOptions.ContainsKey("explored"))
				throw new InvalidOperationException("Die Red-Alert-Karte enthält keine initialisierte Lobby-Option 'explored'.");

			QuestDiagnostics.Write($"Lokale Lobby-Optionen für {map.Title}: {lobbyOptions.Count} Standardwerte initialisiert.");
			var botInfo = map.Rules.Actors[SystemActors.Player].TraitInfos<IBotInfo>().FirstOrDefault(b => b.Type == BotType)
				?? throw new InvalidOperationException($"Red Alert bot type '{BotType}' is unavailable on Blitz.");
			var botColor = Color.FromArgb(245, 6, 6);
			if (botColor == Game.Settings.Player.Color)
				botColor = Color.FromArgb(47, 134, 242);

			var botClient = new Session.Client
			{
				Index = localClientId + 1,
				Name = botInfo.Name,
				Bot = botInfo.Type,
				BotControllerClientIndex = localClientId,
				Slot = "Multi1",
				Faction = "Random",
				Color = botColor,
				PreferredColor = botColor,
				SpawnPoint = 2,
				State = Session.ClientState.Ready
			};
			orderManager.LobbyInfo.Clients.Add(botClient);
			renderer.SetMaximumViewportSize(size);
			world = new World(map, modData, orderManager, WorldType.Regular);
			yield return "Welt";
			orderManager.World = world;
			var botPlayer = world.Players.SingleOrDefault(p => p.ClientIndex == botClient.Index);
			if (botPlayer == null || !botPlayer.IsBot || botPlayer.BotType != BotType ||
				botPlayer.PlayerActor.TraitsImplementing<IBot>()
					.All(b => b.Info.Type != BotType || b.Player != botPlayer) ||
				world.LocalPlayer.RelationshipWith(botPlayer) != PlayerRelationship.Enemy)
				throw new InvalidOperationException("The Red Alert AI opponent was not activated as an enemy.");

			worldRenderer = new WorldRenderer(modData, world);
			yield return "Weltdarstellung";
			Game.worldRenderer = worldRenderer;
			inputHandler = new McvDoubleClickInputHandler(world, worldRenderer);
			world.LoadComplete(worldRenderer);
			orderManager.StartGame();
			worldRenderer.RefreshPalette();
			world.PostLoadComplete(worldRenderer);
			worldRenderer.Viewport.Center(map.CenterOfCell(world.LocalPlayer.HomeLocation));
			Ui.LastTickTime.Value = Game.RunTime;
			lastProgressLogTime = Game.RunTime;
			QuestDiagnostics.Write(
				$"Fortlaufende lokale OpenRA-Spielsession initialisiert. KI-Gegner {botPlayer.BotType} auf Startfeld {botPlayer.HomeLocation} aktiviert.");
		}

		/// <summary>The direct skirmish has no exit button; the app is closed via the system.</summary>
		public bool ExitRequested => false;

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

			if (now - lastProgressLogTime >= 5000)
			{
				Android.Util.Log.Info("OpenRA.Quest.Probe", $"OpenRA-Simulation: Tick {world.WorldTick}, Netzframe {orderManager.NetFrameNumber}.");
				lastProgressLogTime = now;
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
			renderer.EndFrame(inputHandler ?? throw new InvalidOperationException("The game input handler is unavailable."));
		}

		internal sealed class McvDoubleClickInputHandler(World world, WorldRenderer worldRenderer) : IInputHandler
		{
			readonly DefaultInputHandler inner = new(world);
			bool pendingDeploy;
			int2 pressedAt;

			public void ModifierKeys(Modifiers mods) => inner.ModifierKeys(mods);
			public void OnKeyInput(KeyInput input) => inner.OnKeyInput(input);
			public void OnTextInput(string text) => inner.OnTextInput(text);

			public void OnMouseInput(MouseInput input)
			{
				if (input.Button == MouseButton.Left && input.Event == MouseInputEvent.Down &&
					input.MultiTapCount == 2 && IsSelectedMcvAt(input.Location))
				{
					pendingDeploy = true;
					pressedAt = input.Location;
					return;
				}

				if (pendingDeploy)
				{
					if (input.Event == MouseInputEvent.Move)
					{
						if ((input.Location - pressedAt).Length > 24)
							pendingDeploy = false;
						return;
					}

					if (input.Button == MouseButton.Left && input.Event == MouseInputEvent.Up)
					{
						pendingDeploy = false;
						if ((input.Location - pressedAt).Length <= 24 && IsSelectedMcvAt(input.Location))
						{
							inner.OnKeyInput(new KeyInput { Event = KeyInputEvent.Down, Key = Keycode.F });
							inner.OnKeyInput(new KeyInput { Event = KeyInputEvent.Up, Key = Keycode.F });
							QuestDiagnostics.Write("Doppelklick auf ausgewählten MCV: Deploy ausgelöst.");
						}

						return;
					}
				}

				inner.OnMouseInput(input);
			}

			bool IsSelectedMcvAt(int2 location)
			{
				if (world.OrderGenerator is not UnitOrderGenerator || world.Selection.Actors.Count != 1)
					return false;

				var selected = world.Selection.Actors.First();
				if (selected.Owner != world.LocalPlayer ||
					!selected.Info.Name.Equals("mcv", StringComparison.OrdinalIgnoreCase))
					return false;

				var worldPixel = worldRenderer.Viewport.ViewToWorldPx(location);
				return world.ScreenMap.ActorsAtMouse(worldPixel).Any(pair => pair.Actor == selected);
			}
		}

		/// <summary>Screen blocks (in pixels) for the see-through map mask.</summary>
		public const int SeeThroughBlock = 8;

		// OpenRA UI drawn over the battlefield: never punch holes into these, even over shroud.
		static readonly string[] OpaqueWidgets =
		[
			"SIDEBAR_BACKGROUND_TOP", "SIDEBAR_PRODUCTION", "SIDEBAR_MONEYBIN", "COMMAND_BAR",
			"COMMAND_BAR_BACKGROUND", "STANCE_BAR", "SUPPORT_POWERS", "VR_CONTROL_GROUPS", "MUTE_INDICATOR", "CHAT_ROOT"
		];

		/// <summary>
		/// Marks screen blocks that show unexplored map (or space outside the map) for the local
		/// player, so passthrough can show through the black shroud there. Returns false when no
		/// mask applies (observer, menu open, game not running). Runs on the GL thread.
		/// </summary>
		public bool TryComputeSeeThroughMask(bool[] mask, int blocksX, int blocksY)
			=> !disposed && ComputeSeeThroughMask(worldRenderer, mask, blocksX, blocksY);

		/// <summary>Shared by the direct skirmish session and the main menu host.</summary>
		public static bool ComputeSeeThroughMask(WorldRenderer? worldRenderer, bool[] mask, int blocksX, int blocksY)
		{
			if (worldRenderer == null || Ui.CurrentWindow() != null || worldRenderer.World.Type != WorldType.Regular)
				return false;

			var shroud = worldRenderer.World.RenderPlayer?.Shroud;
			if (shroud == null || worldRenderer.World.IsGameOver)
				return false;

			var viewport = worldRenderer.Viewport;
			for (var by = 0; by < blocksY; by++)
			{
				var cy = by * SeeThroughBlock + SeeThroughBlock / 2;
				for (var bx = 0; bx < blocksX; bx++)
				{
					var cx = bx * SeeThroughBlock + SeeThroughBlock / 2;
					var world = worldRenderer.ProjectedPosition(viewport.ViewToWorldPx(new int2(cx, cy)));
					mask[by * blocksX + bx] = !shroud.IsExplored(world);
				}
			}

			foreach (var id in OpaqueWidgets)
			{
				var widget = Ui.Root.GetOrNull(id);
				if (widget == null || !widget.IsVisible())
					continue;

				var bounds = widget.RenderBounds;
				var x0 = Math.Max(0, bounds.Left / SeeThroughBlock);
				var y0 = Math.Max(0, bounds.Top / SeeThroughBlock);
				var x1 = Math.Min(blocksX - 1, (bounds.Right - 1) / SeeThroughBlock);
				var y1 = Math.Min(blocksY - 1, (bounds.Bottom - 1) / SeeThroughBlock);
				for (var by = y0; by <= y1; by++)
					for (var bx = x0; bx <= x1; bx++)
						mask[by * blocksX + bx] = false;
			}

			// The production palette grows downwards with more rows: keep the whole sidebar column opaque.
			var sidebar = Ui.Root.GetOrNull("SIDEBAR_BACKGROUND_TOP");
			if (sidebar != null && sidebar.IsVisible())
			{
				var bounds = sidebar.RenderBounds;
				var x0 = Math.Max(0, bounds.Left / SeeThroughBlock);
				var x1 = Math.Min(blocksX - 1, (bounds.Right - 1) / SeeThroughBlock);
				for (var by = 0; by < blocksY; by++)
					for (var bx = x0; bx <= x1; bx++)
						mask[by * blocksX + bx] = false;
			}

			return true;
		}

		public (ITexture Texture, int Width, int Height) ScreenTexture =>
			renderer?.ScreenTexture ?? throw new InvalidOperationException("The OpenRA renderer is unavailable.");

		public (byte[] Pixels, int BackingWidth, int Width, int Height) ReadScreenPixelsBgra()
		{
			ObjectDisposedException.ThrowIf(disposed, this);
			return renderer?.ReadScreenPixelsBgra() ??
				throw new InvalidOperationException("The OpenRA renderer is unavailable.");
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
			Game.LocalPlayerProfile = previousLocalPlayerProfile!;
			Game.Renderer = previousRenderer;
			Game.Settings.Game.MouseControlStyle = previousMouseControlStyle;
			Game.Settings.Game.MouseScroll = previousMouseScroll;
			Game.Settings.Game.UseAlternateScrollButton = previousAlternateScrollButton;
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
