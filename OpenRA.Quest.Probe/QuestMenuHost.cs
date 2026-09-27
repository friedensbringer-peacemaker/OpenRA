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

using OpenRA.Primitives;

namespace OpenRA.Quest.Probe
{
	/// <summary>
	/// Runs OpenRA's regular flow (shellmap, main menu, skirmish lobby with a local server,
	/// missions, settings) on the Android GL thread via <see cref="Game.InitializeEmbedded"/>
	/// and <see cref="Game.EmbeddedFrame"/>, instead of a fixed skirmish.
	/// </summary>
	sealed class QuestMenuHost : IQuestGame
	{
		readonly Size size;
		readonly QuestInputQueue input;
		bool initialized;
		bool exitRequested;
		bool disposed;

		public QuestMenuHost(Size size, QuestInputQueue input)
		{
			this.size = size;
			this.input = input;
		}

		public bool ExitRequested => exitRequested;

		public IEnumerator<string> LoadSteps()
		{
			var platform = new ProbePlatform(size, input);

			// Keep the MCV double-click deploy of the direct skirmish, but only in real games.
			Game.InputHandlerFactory = world => world != null && world.Type == WorldType.Regular && Game.worldRenderer != null
				? new QuestGameSession.McvDoubleClickInputHandler(world, Game.worldRenderer)
				: new DefaultInputHandler(world);

			var steps = Game.InitializeEmbedded(new Arguments(), platform, "ra", Configure);
			while (steps.MoveNext())
				yield return steps.Current;

			initialized = true;
			QuestDiagnostics.Write("OpenRA-Hauptmenü geladen.");
		}

		/// <summary>Headset-friendly defaults; runs before anything reads the settings.</summary>
		void Configure(Settings settings)
		{
			settings.Graphics.Mode = WindowMode.Windowed;
			settings.Graphics.WindowedSize = new int2(size.Width, size.Height);
			settings.Graphics.GLProfile = GLProfile.Embedded;
			settings.Graphics.DisableHardwareCursors = true;
			settings.Graphics.CapFramerate = true;
			settings.Graphics.MaxFramerate = 30;

			// Controller ray = mouse: modern mouse style, no keyboard-only scroll button.
			settings.Game.MouseControlStyle = MouseControlStyle.Modern;
			settings.Game.MouseScroll = MouseScrollType.Standard;
			settings.Game.UseAlternateScrollButton = false;

			// No online services from inside the headset app; skip first-run dialogs that need a keyboard.
			settings.Game.FetchNews = false;
			settings.Game.EnableDiscordService = false;
			settings.Game.IntroductionPromptVersion = int.MaxValue;
			settings.Debug.CheckVersion = false;
			settings.Debug.SendSystemInformation = false;
			settings.Debug.SystemInformationVersionPrompt = int.MaxValue;
			settings.Server.DiscoverNatDevices = false;
		}

		public void TickAndRender()
		{
			ObjectDisposedException.ThrowIf(disposed, this);
			if (!initialized || exitRequested)
				return;

			if (!Game.EmbeddedFrame())
			{
				exitRequested = true;
				QuestDiagnostics.Write("OpenRA hat das Beenden angefordert.");
			}
		}

		public (byte[] Pixels, int BackingWidth, int Width, int Height) ReadScreenPixelsBgra()
			=> Game.Renderer?.ReadScreenPixelsBgra() ?? throw new InvalidOperationException("The OpenRA renderer is unavailable.");

		public bool TryComputeSeeThroughMask(bool[] mask, int blocksX, int blocksY)
			=> !disposed && initialized && QuestGameSession.ComputeSeeThroughMask(Game.worldRenderer, mask, blocksX, blocksY);

		public void Dispose()
		{
			if (disposed)
				return;

			disposed = true;
			try
			{
				Game.ShutdownEmbedded();
			}
			catch (Exception e)
			{
				Android.Util.Log.Warn("OpenRA.Quest.Probe", $"OpenRA konnte nicht vollständig beendet werden: {e}");
			}
		}
	}
}
