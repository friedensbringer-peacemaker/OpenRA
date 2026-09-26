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

#if QUEST_XR
using Android.App;
using Android.Content;
using Android.Graphics;
using Com.Friedensbringer.Openra.XR;

namespace OpenRA.Quest.Probe
{
	/// <summary>A controller-operated overlay in the same pixels as the XR game board.</summary>
	sealed class QuestXrMenu
	{
		const int Left = 200;
		const int Top = 70;
		const int Width = 880;
		const int Height = 660;
		const int FirstRow = 118;
		const int RowHeight = 72;
		const int RowCount = 7;
		static readonly string[] ThicknessNames = ["Dünn", "Mittel", "Dick"];
		static readonly string[] ColorNames = ["Weiß", "Warmgelb", "Blau", "Grau"];
		static readonly string[] TargetNames = ["Punkt", "Ring", "Kreuz"];

		readonly object stateLock = new();
		readonly Android.Content.ISharedPreferences preferences;
		int[]? overlay;
		int pressedRow = -1;
		int hoveredRow = -1;
		int color;
		int thickness;
		int target;
		bool rayVisible;
		bool open;

		public enum Action { None, Deploy, OpenGameMenu }

		public QuestXrMenu(Activity activity)
		{
			preferences = activity.GetSharedPreferences("openra-xr-controls", FileCreationMode.Private)!;
			color = Math.Clamp(preferences.GetInt("ray_color", 0), 0, 3);
			thickness = Math.Clamp(preferences.GetInt("ray_thickness", 1), 0, 2);
			target = Math.Clamp(preferences.GetInt("target_style", 1), 0, 2);
			rayVisible = preferences.GetBoolean("ray_visible", true);
		}

		public bool IsOpen
		{
			get { lock (stateLock) return open; }
		}

		public void ApplyStyle()
		{
			lock (stateLock)
				XrProbe.SetPointerStyle(rayVisible, thickness, color, target);
		}

		public void Toggle()
		{
			lock (stateLock)
			{
				open = !open;
				pressedRow = -1;
				hoveredRow = -1;
				overlay = null;
			}
		}

		public void Close()
		{
			lock (stateLock)
			{
				open = false;
				pressedRow = -1;
				overlay = null;
			}
		}

		/// <summary>Consumes right-controller input while the menu is visible.</summary>
		public Action HandlePointer(int type, int x, int y)
		{
			lock (stateLock)
			{
				if (!open)
					return Action.None;

				var row = RowAt(x, y);
				if (hoveredRow != row)
				{
					hoveredRow = row;
					overlay = null;
				}

				if (type == XrProbe.PointerDown)
					pressedRow = row;
				else if (type == XrProbe.PointerUp)
				{
					var selected = pressedRow == row ? row : -1;
					pressedRow = -1;
					switch (selected)
					{
						case 0:
							open = false;
							break;
						case 1:
							open = false;
							return Action.Deploy;
						case 2:
							open = false;
							return Action.OpenGameMenu;
						case 3:
							rayVisible = !rayVisible;
							SaveStyle();
							break;
						case 4:
							thickness = (thickness + 1) % 3;
							SaveStyle();
							break;
						case 5:
							color = (color + 1) % 4;
							SaveStyle();
							break;
						case 6:
							target = (target + 1) % 3;
							SaveStyle();
							break;
					}

					overlay = null;
				}

				return Action.None;
			}
		}

		public void Draw(byte[] rgba)
		{
			lock (stateLock)
			{
				if (!open)
					return;

				overlay ??= BuildOverlay();
				for (var y = 0; y < Height; y++)
				for (var x = 0; x < Width; x++)
				{
					var argb = overlay[y * Width + x];
					var alpha = (int)((uint)argb >> 24);
					if (alpha == 0)
						continue;

					var offset = ((XrFrameConverter.BoardHeight - 1 - Top - y) *
						XrFrameConverter.BoardWidth + Left + x) * 4;
					var inverse = 255 - alpha;
					rgba[offset] = (byte)((((argb >> 16) & 255) * alpha + rgba[offset] * inverse) / 255);
					rgba[offset + 1] = (byte)((((argb >> 8) & 255) * alpha + rgba[offset + 1] * inverse) / 255);
					rgba[offset + 2] = (byte)(((argb & 255) * alpha + rgba[offset + 2] * inverse) / 255);
				}
			}
		}

		void SaveStyle()
		{
			preferences.Edit()!.PutBoolean("ray_visible", rayVisible)!.PutInt("ray_thickness", thickness)!
				.PutInt("ray_color", color)!.PutInt("target_style", target)!.Apply();
			XrProbe.SetPointerStyle(rayVisible, thickness, color, target);
		}

		static int RowAt(int x, int y)
		{
			if (x < Left + 24 || x >= Left + Width - 24 || y < Top + FirstRow)
				return -1;

			var row = (y - Top - FirstRow) / RowHeight;
			return row < RowCount ? row : -1;
		}

		int[] BuildOverlay()
		{
			using var bitmap = Bitmap.CreateBitmap(Width, Height, Bitmap.Config.Argb8888!);
			using var canvas = new Canvas(bitmap!);
			using var fill = new Paint(PaintFlags.AntiAlias);
			using var text = new Paint(PaintFlags.AntiAlias) { TextSize = 35 };
			fill.Color = Color.Argb(241, 13, 20, 31);
			canvas.DrawRect(0, 0, Width, Height, fill);
			fill.Color = Color.Rgb(175, 46, 54);
			canvas.DrawRect(0, 0, Width, 7, fill);
			text.Color = Color.White;
			text.TextSize = 43;
			canvas.DrawText("OPENRA · QUEST", 40, 67, text);
			text.TextSize = 25;
			text.Color = Color.Rgb(194, 204, 218);
			canvas.DrawText("Linke Menütaste: schließen · Rechts zeigen und Trigger drücken", 40, 104, text);
			var labels = new[]
			{
				"Weiterspielen",
				"Bauhof / Einheit entfalten (F)",
				"OpenRA-Spielmenü öffnen",
				$"Controllerstrahl: {(rayVisible ? "Ein" : "Aus")}",
				$"Strahlstärke: {ThicknessNames[thickness]}",
				$"Strahlfarbe: {ColorNames[color]}",
				$"Zielpunkt: {TargetNames[target]}"
			};
			for (var row = 0; row < RowCount; row++)
			{
				var top = FirstRow + row * RowHeight;
				fill.Color = row == hoveredRow ? Color.Rgb(77, 96, 126) :
					row % 2 == 0 ? Color.Rgb(36, 45, 60) : Color.Rgb(29, 38, 52);
				canvas.DrawRect(24, top, Width - 24, top + RowHeight - 8, fill);
				text.Color = Color.White;
				text.TextSize = 35;
				canvas.DrawText(labels[row], 48, top + 45, text);
			}

			var pixels = new int[Width * Height];
			bitmap!.GetPixels(pixels, 0, Width, 0, 0, Width, Height);
			return pixels;
		}
	}
}
#endif
