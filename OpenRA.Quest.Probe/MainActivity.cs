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

using System.Numerics;
using Android.App;
using Android.OS;
using Android.Widget;
using OpenRA.Primitives;

namespace OpenRA.Quest.Probe
{
	/// <summary>
	/// Android packaging probe. It links the real OpenRA.Game assembly and runs
	/// its tabletop coordinate mapper, but does not launch a game or an XR session.
	/// </summary>
	[Activity(Label = "OpenRA Quest Probe", MainLauncher = true)]
	public class MainActivity : Activity
	{
		protected override void OnCreate(Bundle? savedInstanceState)
		{
			base.OnCreate(savedInstanceState);

			var pointer = new TabletopPointer(Vector3.Zero, Vector3.UnitX, Vector3.UnitZ,
				2, 1, new Size(1000, 500));
			var projected = pointer.TryMapRay(new Vector3(0, 1, 0), -Vector3.UnitY, out var position);
			var status = projected && position == new int2(500, 250)
				? "OpenRA.Game-Bibliothek geladen. Tabletop-Projektion funktioniert."
				: "OpenRA.Game-Bibliothek geladen. Tabletop-Projektion fehlgeschlagen.";

			SetContentView(new TextView(this)
			{
				Text = $"{status}\n\nTechnischer Android-ARM64-Test; noch kein Spiel und keine XR-Darstellung.",
				TextSize = 22
			});
		}
	}
}
