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
using Android.Content;
using Android.Content.Res;
using Android.Opengl;
using Android.OS;
using Android.Widget;
using OpenRA.Primitives;
using Bitmap = Android.Graphics.Bitmap;

namespace OpenRA.Quest.Probe
{
	/// <summary>
	/// Android host for a local Red Alert game and its optional experimental
	/// OpenXR quad bridge.
	/// </summary>
#if QUEST_XR
	[Activity(Label = "OpenRA Quest XR", MainLauncher = false, Exported = true)]
	[IntentFilter(new[] { Intent.ActionMain },
		Categories = new[] { Intent.CategoryLauncher, "org.khronos.openxr.intent.category.IMMERSIVE_HMD" })]
#else
	[Activity(Label = "OpenRA Quest Probe", MainLauncher = true)]
#endif
	public class MainActivity : Activity
	{
		const int ImportRaArchiveRequestCode = 7001;
		GLSurfaceView? glView;
		Button? importButton;
		TextView? importStatus;

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
#if QUEST_XR
			const string xrState = "Eine experimentelle XR-Fläche kann gestartet werden.";
#else
			const string xrState = "XR-Darstellung fehlt noch.";
#endif
			content.AddView(new TextView(this)
			{
				Text = $"{status}\n\n" +
					"Die untere Fläche versucht mit importierten Originaldaten eine lokale Partie " +
					"fortlaufend anzuzeigen. Bei Fehlern bleibt der letzte Weltframe sichtbar. " +
					$"Die Tasten wählen Auswahl, Mehrfachauswahl, Befehle oder Kartenbewegung; {xrState}",
				TextSize = 22
			});
			var contentReady = File.Exists(Path.Combine(appFiles, "Content/ra/v2/snow.mix")) &&
				File.Exists(Path.Combine(appFiles, "Content/ra/v2/conquer.mix"));
			importStatus = new TextView(this)
			{
				Text = contentReady ? "Red-Alert-Daten vorhanden." : "Für die Spielansicht wird OpenRAs Red-Alert-Quickinstall-ZIP benötigt.",
				TextSize = 18
			};
			content.AddView(importStatus);
			if (!contentReady)
			{
				importButton = new Button(this) { Text = "Red-Alert-ZIP auswählen" };
				importButton.Click += (_, _) =>
				{
					var pickArchive = new Intent(Intent.ActionOpenDocument);
					pickArchive.AddCategory(Intent.CategoryOpenable);
					pickArchive.SetType("*/*");
					StartActivityForResult(pickArchive, ImportRaArchiveRequestCode);
				};
				content.AddView(importButton);
			}

			if (terrainPreview != null && !contentReady)
			{
				var image = new ImageView(this);
				image.SetImageBitmap(terrainPreview);
				image.SetScaleType(ImageView.ScaleType.FitCenter);
				content.AddView(image, new LinearLayout.LayoutParams(-1, 0, 1));
			}

			if (terrainPreview != null)
			{
				var input = new QuestInputQueue();
				var touchButton = MouseButton.Left;
				var gameView = new QuestTouchSurfaceView(this, input, () => touchButton);
				glView = gameView;
				glView.SetEGLContextClientVersion(3);
				glView.SetRenderer(new GlesProbeRenderer(terrainPreview,
					Path.Combine(appFiles, "gles-terrain-preview.png"),
					Path.Combine(appFiles, "openra-terrain-preview.png"),
					Path.Combine(appFiles, "openra-renderer-ui-preview.png"),
					Path.Combine(appFiles, "openra-renderer-world-preview.png"),
					Path.Combine(appFiles, "openra-authentic-terrain-preview.png"),
					Path.Combine(appFiles, "openra-game-world-preview.png"),
					Path.Combine(appFiles, "openra-regular-world-preview.png"), input, contentReady,
					running => RunOnUiThread(() =>
					{
						if (IsFinishing || IsDestroyed)
							return;

						if (glView != null)
						{
							glView.RenderMode = running ? Rendermode.Continuously : Rendermode.WhenDirty;
							if (!running)
								glView.RequestRender();
						}
					}),
					message => RunOnUiThread(() =>
					{
						if (!IsFinishing && !IsDestroyed && importStatus != null)
							importStatus.Text = message;
					})));
				glView.RenderMode = Rendermode.WhenDirty;
				content.AddView(glView, contentReady
					? new LinearLayout.LayoutParams(-1, 0, 1)
					: new LinearLayout.LayoutParams(-1, 300));
				var controls = new LinearLayout(this) { Orientation = Android.Widget.Orientation.Horizontal };
				var selectButton = new Button(this) { Text = "● Auswählen" };
				var orderButton = new Button(this) { Text = "Befehl" };
				var panButton = new Button(this) { Text = "Karte ziehen" };
				var additiveSelection = false;
				void UpdateSelectionModifier() => input.SetModifiers(touchButton == MouseButton.Left && additiveSelection
					? Modifiers.Shift : Modifiers.None);
				selectButton.Click += (_, _) =>
				{
					touchButton = MouseButton.Left;
					UpdateSelectionModifier();
					selectButton.Text = "● Auswählen";
					orderButton.Text = "Befehl";
					panButton.Text = "Karte ziehen";
				};
				orderButton.Click += (_, _) =>
				{
					touchButton = MouseButton.Right;
					UpdateSelectionModifier();
					selectButton.Text = "Auswählen";
					orderButton.Text = "● Befehl";
					panButton.Text = "Karte ziehen";
				};
				panButton.Click += (_, _) =>
				{
					touchButton = MouseButton.Middle;
					UpdateSelectionModifier();
					selectButton.Text = "Auswählen";
					orderButton.Text = "Befehl";
					panButton.Text = "● Karte ziehen";
				};
				controls.AddView(selectButton, new LinearLayout.LayoutParams(0, -2, 1));
				controls.AddView(orderButton, new LinearLayout.LayoutParams(0, -2, 1));
				controls.AddView(panButton, new LinearLayout.LayoutParams(0, -2, 1));
				content.AddView(controls);
				var zoomControls = new LinearLayout(this) { Orientation = Android.Widget.Orientation.Horizontal };
				var zoomInButton = new Button(this) { Text = "Karte +" };
				var zoomOutButton = new Button(this) { Text = "Karte −" };
				var additiveButton = new Button(this) { Text = "Mehrfach" };
				zoomInButton.Click += (_, _) => input.Scroll(new int2(gameView.Width / 2, gameView.Height / 2), 4);
				zoomOutButton.Click += (_, _) => input.Scroll(new int2(gameView.Width / 2, gameView.Height / 2), -4);
				additiveButton.Click += (_, _) =>
				{
					additiveSelection = !additiveSelection;
					additiveButton.Text = additiveSelection ? "● Mehrfach" : "Mehrfach";
					UpdateSelectionModifier();
				};
				zoomControls.AddView(zoomInButton, new LinearLayout.LayoutParams(0, -2, 1));
				zoomControls.AddView(zoomOutButton, new LinearLayout.LayoutParams(0, -2, 1));
				zoomControls.AddView(additiveButton, new LinearLayout.LayoutParams(0, -2, 1));
				content.AddView(zoomControls);
#if QUEST_XR
				if (contentReady)
				{
					var xrButton = new Button(this) { Text = "XR-Fläche starten (Experiment)" };
					xrButton.Click += (_, _) =>
					{
						var bridge = new QuestXrBridge(this, input, message =>
						{
							if (!IsFinishing && !IsDestroyed && importStatus != null)
								importStatus.Text = message;
						});
						if (!QuestXrBridge.Install(bridge) && importStatus != null)
							importStatus.Text = "OpenXR-Fläche läuft bereits.";
					};
					content.AddView(xrButton);
				}
#endif
			}

			SetContentView(content);
		}

		protected override async void OnActivityResult(int requestCode, Result resultCode, Intent? data)
		{
			base.OnActivityResult(requestCode, resultCode, data);
			if (requestCode != ImportRaArchiveRequestCode || resultCode != Result.Ok || data?.Data == null)
				return;

			if (importButton != null)
				importButton.Enabled = false;
			if (importStatus != null)
				importStatus.Text = "Red-Alert-Daten werden geprüft und importiert …";

			try
			{
				var archiveUri = data.Data;
				var appFiles = FilesDir?.AbsolutePath ?? throw new InvalidOperationException("Android app storage is unavailable.");
				await Task.Run(() =>
				{
					using var source = ContentResolver?.OpenInputStream(archiveUri) ??
						throw new IOException("The selected archive could not be opened.");
					RaContentImporter.Import(source, appFiles);
				});

				if (IsFinishing || IsDestroyed)
					return;

				if (importStatus != null)
					importStatus.Text = "Red-Alert-Daten importiert. Die Ansicht wird neu gestartet.";
				Recreate();
			}
			catch (Exception e)
			{
				Android.Util.Log.Error("OpenRA.Quest.Probe", $"Red-Alert-Import fehlgeschlagen: {e}");
				if (IsFinishing || IsDestroyed)
					return;

				if (importStatus != null)
					importStatus.Text = $"Import fehlgeschlagen: {e.Message}";
				if (importButton != null)
					importButton.Enabled = true;
			}
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

		protected override void OnDestroy()
		{
#if QUEST_XR
			QuestXrBridge.Current?.Dispose();
#endif
			base.OnDestroy();
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
