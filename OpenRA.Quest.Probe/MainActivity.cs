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

using System.IO;
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
			var appFiles = FilesDir?.AbsolutePath ?? throw new InvalidOperationException("Android app storage is unavailable.");
			Platform.OverrideSupportDir(appFiles);

			var pointer = new TabletopPointer(Vector3.Zero, Vector3.UnitX, Vector3.UnitZ,
				2, 1, new Size(1000, 500));
			var projected = pointer.TryMapRay(new Vector3(0, 1, 0), -Vector3.UnitY, out var position);
			var platformReady = Platform.CurrentPlatform == PlatformType.Android && Platform.SupportDir.StartsWith(appFiles, StringComparison.Ordinal);
			var modReady = false;
			var modulesReady = false;
			try
			{
				var modRoot = Path.Combine(appFiles, "mods");
				var modDir = Path.Combine(modRoot, "ra");
				Directory.CreateDirectory(modDir);
				using (var asset = (Assets ?? throw new InvalidOperationException("Android assets are unavailable.")).Open("mods/ra/mod.yaml"))
				using (var file = File.Create(Path.Combine(modDir, "mod.yaml")))
					asset.CopyTo(file);

				var mods = new InstalledMods([modRoot], []);
				if (mods.TryGetValue("ra", out var manifest))
				{
					modReady = manifest.Rules.Length > 0;
					using var creator = new ObjectCreator(manifest, mods);
					modulesReady = creator.FindType("ContentInstallerFileSystemLoader") != null &&
						creator.FindType("AudLoader") != null;
				}
			}
			catch (Exception e)
			{
				Android.Util.Log.Error("OpenRA.Quest.Probe", $"Red-Alert-Mod oder Assemblies konnten nicht geladen werden: {e}");
			}

			var status = projected && position == new int2(500, 250) && platformReady && modReady && modulesReady
				? "OpenRA.Game geladen. Android-Speicher, Tabletop, Red-Alert-Modmanifest und Mod-Assemblies funktionieren."
				: "OpenRA.Game geladen. Plattform-, Tabletop- oder Modprüfung fehlgeschlagen.";
			Android.Util.Log.Info("OpenRA.Quest.Probe", status);

			SetContentView(new TextView(this)
			{
				Text = $"{status}\n\nTechnischer Android-ARM64-Test; noch kein Spiel und keine XR-Darstellung.",
				TextSize = 22
			});
		}
	}
}
