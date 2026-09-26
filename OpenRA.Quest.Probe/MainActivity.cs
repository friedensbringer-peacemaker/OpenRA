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
using Android.Content.Res;
using Android.Opengl;
using Android.OS;
using Android.Widget;
using OpenRA.Primitives;
using Bitmap = Android.Graphics.Bitmap;

namespace OpenRA.Quest.Probe
{
	/// <summary>
	/// Android packaging probe. It loads OpenRA's rules and one map, but does not
	/// launch a game or an XR session.
	/// </summary>
	[Activity(Label = "OpenRA Quest Probe", MainLauncher = true)]
	public class MainActivity : Activity
	{
		GLSurfaceView? glView;

		protected override void OnCreate(Bundle? savedInstanceState)
		{
			base.OnCreate(savedInstanceState);
			var appFiles = FilesDir?.AbsolutePath ?? throw new InvalidOperationException("Android app storage is unavailable.");
			Platform.OverrideEngineDir(appFiles);
			Platform.OverrideSupportDir(appFiles);
			Game.InitializeSettings(new Arguments());
			Log.AddChannel("perf", "perf.log");
			Log.AddChannel("debug", "debug.log");
			Log.AddChannel("server", "server.log", true);
			Log.AddChannel("sound", "sound.log");
			Log.AddChannel("graphics", "graphics.log");
			Log.AddChannel("geoip", "geoip.log");
			Log.AddChannel("nat", "nat.log");
			Log.AddChannel("client", "client.log");

			var pointer = new TabletopPointer(Vector3.Zero, Vector3.UnitX, Vector3.UnitZ,
				2, 1, new Size(1000, 500));
			var projected = pointer.TryMapRay(new Vector3(0, 1, 0), -Vector3.UnitY, out var position);
			var platformReady = Platform.CurrentPlatform == PlatformType.Android && Platform.SupportDir.StartsWith(appFiles, StringComparison.Ordinal);
			var modReady = false;
			var modulesReady = false;
			var rulesReady = false;
			var mapReady = false;
			Bitmap? terrainPreview = null;
			try
			{
				var assets = Assets ?? throw new InvalidOperationException("Android assets are unavailable.");
				CopyAssetTree(assets, "mods/common", appFiles);
				CopyAssetTree(assets, "mods/ra", appFiles);
				CopyAssetTree(assets, "glsl", appFiles);
				using (var source = assets.Open("global mix database.dat"))
				using (var output = File.Create(Path.Combine(appFiles, "global mix database.dat")))
					source.CopyTo(output);

				var modRoot = Path.Combine(appFiles, "mods");
				var mods = new InstalledMods([modRoot], []);
				if (mods.TryGetValue("ra", out var manifest))
				{
					modReady = manifest.Rules.Length > 0;
					using var creator = new ObjectCreator(manifest, mods);
					modulesReady = creator.FindType("ContentInstallerFileSystemLoader") != null &&
						creator.FindType("AudLoader") != null;
					using var modData = new ModData(manifest, mods);
					Game.ModData = modData;
					try
					{
						var rules = modData.DefaultRules;
						rulesReady = rules.Actors.Count > 0 && rules.Weapons.Count > 0;
						Android.Util.Log.Info("OpenRA.Quest.Probe", $"Red-Alert-Regeln: {rules.Actors.Count} Akteure, {rules.Weapons.Count} Waffen.");

						using var mapPackage = modData.ModFiles.OpenPackage("ra|maps/blitz.oramap");
						using var map = new Map(modData, mapPackage);
						var parsedMap = !map.InvalidCustomRules && map.Rules.Actors.Count > 0 && map.MapSize.Width > 0 && map.MapSize.Height > 0;
						Android.Util.Log.Info("OpenRA.Quest.Probe", $"Karte: {map.Title}, {map.MapSize.Width}x{map.MapSize.Height}, Tileset {map.Tileset}.");
						if (parsedMap)
						{
							terrainPreview = CreateTerrainPreview(map);
							using var enlarged = Bitmap.CreateScaledBitmap(terrainPreview,
								terrainPreview.Width * 8, terrainPreview.Height * 8, false);
							using var previewFile = File.Create(Path.Combine(appFiles, "terrain-preview.png"));
							if (!enlarged.Compress(Bitmap.CompressFormat.Png!, 100, previewFile))
								throw new IOException("Could not save the terrain preview.");
						}

						mapReady = parsedMap && terrainPreview != null;
					}
					finally
					{
						Game.ModData = null;
					}
				}
			}
			catch (Exception e)
			{
				Android.Util.Log.Error("OpenRA.Quest.Probe", $"Red-Alert-Daten konnten nicht geladen werden: {e}");
			}

			var status = projected && position == new int2(500, 250) && platformReady && modReady && modulesReady && rulesReady && mapReady
				? "OpenRA geladen. Red-Alert-Regeln und Testkarte funktionieren auf Android."
				: "OpenRA geladen. Plattform-, Regel- oder Kartenprüfung fehlgeschlagen.";
			Android.Util.Log.Info("OpenRA.Quest.Probe", status);

			var content = new LinearLayout(this) { Orientation = Android.Widget.Orientation.Vertical };
			content.SetPadding(24, 24, 24, 24);
			content.AddView(new TextView(this)
			{
				Text = $"{status}\n\n" +
					"Die untere Fläche zeigt mit importierten Originaldaten einen statischen OpenRA-Weltframe, " +
					"sonst Gelände-Farben. Noch keine bedienbare Partie oder XR-Darstellung.",
				TextSize = 22
			});
			if (terrainPreview != null)
			{
				var image = new ImageView(this);
				image.SetImageBitmap(terrainPreview);
				image.SetScaleType(ImageView.ScaleType.FitCenter);
				content.AddView(image, new LinearLayout.LayoutParams(-1, 0, 1));
			}

			if (terrainPreview != null)
			{
				glView = new GLSurfaceView(this);
				glView.SetEGLContextClientVersion(3);
				glView.SetRenderer(new GlesProbeRenderer(terrainPreview,
					Path.Combine(appFiles, "gles-terrain-preview.png"),
					Path.Combine(appFiles, "openra-terrain-preview.png"),
					Path.Combine(appFiles, "openra-renderer-ui-preview.png"),
					Path.Combine(appFiles, "openra-renderer-world-preview.png"),
					Path.Combine(appFiles, "openra-authentic-terrain-preview.png"),
					Path.Combine(appFiles, "openra-game-world-preview.png"),
					Path.Combine(appFiles, "openra-regular-world-preview.png")));
				glView.RenderMode = Rendermode.WhenDirty;
				content.AddView(glView, new LinearLayout.LayoutParams(-1, 300));
			}

			SetContentView(content);
		}

		protected override void OnPause()
		{
			glView?.OnPause();
			base.OnPause();
		}

		protected override void OnResume()
		{
			base.OnResume();
			glView?.OnResume();
		}

		static Bitmap CreateTerrainPreview(Map map)
		{
			var small = Bitmap.CreateBitmap(map.MapSize.Width, map.MapSize.Height, Bitmap.Config.Argb8888!);
			for (var y = 0; y < map.MapSize.Height; y++)
				for (var x = 0; x < map.MapSize.Width; x++)
				{
					var color = map.GetTerrainInfo(new MPos(x, y)).Color;
					small.SetPixel(x, y, new Android.Graphics.Color(unchecked((int)color.ToArgb())));
				}

			return small;
		}

		static void CopyAssetTree(AssetManager assets, string assetPath, string destinationRoot)
		{
			foreach (var name in assets.List(assetPath) ?? [])
			{
				var childPath = $"{assetPath}/{name}";
				if (assets.List(childPath) is { Length: > 0 })
				{
					CopyAssetTree(assets, childPath, destinationRoot);
					continue;
				}

				var target = Path.Combine(destinationRoot, childPath.Replace('/', Path.DirectorySeparatorChar));
				Directory.CreateDirectory(Path.GetDirectoryName(target)!);
				using var source = assets.Open(childPath);
				using var output = File.Create(target);
				source.CopyTo(output);
			}
		}
	}
}
