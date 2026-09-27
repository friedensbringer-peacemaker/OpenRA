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

namespace OpenRA.Quest.Probe
{
	/// <summary>A running OpenRA instance driven frame by frame from the Android GL thread.</summary>
	interface IQuestGame : IDisposable
	{
		/// <summary>Builds the game in stages; each step ends one frame. The caller disposes on failure.</summary>
		IEnumerator<string> LoadSteps();

		/// <summary>Advances logic and draws one frame.</summary>
		void TickAndRender();

		/// <summary>True once the player asked to quit (e.g. "Exit" in the main menu).</summary>
		bool ExitRequested { get; }

		(byte[] Pixels, int BackingWidth, int Width, int Height) ReadScreenPixelsBgra();

		/// <summary>See <see cref="QuestGameSession.ComputeSeeThroughMask"/>.</summary>
		bool TryComputeSeeThroughMask(bool[] mask, int blocksX, int blocksY);
	}
}
