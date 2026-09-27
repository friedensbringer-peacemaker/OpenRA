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
using OpenRA.Graphics;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	/// <summary>
	/// Quick menu for XR controllers in the look of OpenRA's own menus. Opened by the XR host
	/// (left menu button); usable with the controller ray or with stick, A and B.
	/// </summary>
	public class VrQuickMenuLogic : ChromeLogic
	{
		const string WindowId = "VR_QUICK_MENU";

		[FluentReference]
		const string RayOn = "button-vr-quick-menu-ray-on";

		[FluentReference]
		const string RayOff = "button-vr-quick-menu-ray-off";

		[FluentReference("mode")]
		const string Passthrough = "button-vr-quick-menu-passthrough";

		[FluentReference]
		const string PassthroughOff = "options-vr-passthrough-short.off";

		[FluentReference]
		const string PassthroughBackground = "options-vr-passthrough-short.background";

		[FluentReference]
		const string PassthroughSeeThrough = "options-vr-passthrough-short.see-through";

		static volatile VrQuickMenuLogic current;

		readonly World world;
		readonly WorldRenderer worldRenderer;
		readonly VrSettings vrSettings;
		readonly List<(ButtonWidget Button, Action Activate)> entries = [];
		int focused = -1;

		/// <summary>Safe to read from the XR input thread.</summary>
		public static bool IsOpen => current != null;

		[ObjectCreator.UseCtor]
		public VrQuickMenuLogic(Widget widget, World world, WorldRenderer worldRenderer, ModData modData)
		{
			this.world = world;
			this.worldRenderer = worldRenderer;
			vrSettings = modData.GetSettings<VrSettings>();

			AddEntry(widget, "RESUME", Close);
			AddEntry(widget, "DEPLOY", () => { Close(); VrRuntime.Deploy?.Invoke(); });
			AddEntry(widget, "GAME_MENU", () => { Close(); VrRuntime.OpenGameMenu?.Invoke(); });
			AddEntry(widget, "VR_SETTINGS", () => { Close(); OpenVrSettings(); });
			AddEntry(widget, "RECENTER", () => VrRuntime.Recenter?.Invoke());

			var ray = AddEntry(widget, "TOGGLE_RAY", () =>
			{
				vrSettings.RayVisible ^= true;
				Game.Settings.Save();
				VrRuntime.Apply?.Invoke(vrSettings);
			});
			var rayOn = FluentProvider.GetMessage(RayOn);
			var rayOff = FluentProvider.GetMessage(RayOff);
			ray.GetText = () => vrSettings.RayVisible ? rayOn : rayOff;

			// Cycles Off -> room as background -> background with see-through unexplored map.
			var passthrough = AddEntry(widget, "PASSTHROUGH", () =>
			{
				vrSettings.PassthroughMode = (vrSettings.PassthroughMode + 1) % 3;
				Game.Settings.Save();
				VrRuntime.Apply?.Invoke(vrSettings);
			});
			string[] modeNames = [PassthroughOff, PassthroughBackground, PassthroughSeeThrough];
			passthrough.GetText = () => FluentProvider.GetMessage(Passthrough, "mode",
				FluentProvider.GetMessage(modeNames[Math.Clamp(vrSettings.PassthroughMode, 0, 2)]));
			passthrough.IsDisabled = () => VrRuntime.PassthroughAvailable?.Invoke() == false;

			current = this;
		}

		ButtonWidget AddEntry(Widget widget, string id, Action activate)
		{
			var button = widget.Get<ButtonWidget>(id);
			var index = entries.Count;
			button.OnClick = activate;
			button.IsHighlighted = () => focused == index;
			entries.Add((button, activate));
			return button;
		}

		void Close()
		{
			if (current != this)
				return;

			current = null;
			Ui.CloseWindow();
		}

		void OpenVrSettings()
		{
			var panel = Ui.OpenWindow("SETTINGS_PANEL", new WidgetArgs()
			{
				{ "world", world },
				{ "worldRenderer", worldRenderer },
				{ "onExit", () => { } },
			});

			// Jump straight to the VR tab instead of the first settings page.
			panel.Get("SETTINGS_TAB_CONTAINER").GetOrNull<ButtonWidget>("VR_PANEL")?.OnClick();
		}

		/// <summary>Opens or closes the menu. Call on the game thread (Game.RunAfterTick).</summary>
		public static void Toggle(WorldRenderer worldRenderer)
		{
			if (current != null)
			{
				current.Close();
				return;
			}

			if (worldRenderer == null)
				return;

			Ui.OpenWindow(WindowId, new WidgetArgs()
			{
				{ "world", worldRenderer.World },
				{ "worldRenderer", worldRenderer },
			});
		}

		/// <summary>Moves the stick focus up (-1) or down (+1).</summary>
		public static void Navigate(int direction)
		{
			var menu = current;
			if (menu == null || menu.entries.Count == 0)
				return;

			var count = menu.entries.Count;
			menu.focused = menu.focused < 0
				? (direction > 0 ? 0 : count - 1)
				: (menu.focused + direction + count) % count;
		}

		/// <summary>Activates the entry focused with the stick (A button).</summary>
		public static void ActivateFocused()
		{
			var menu = current;
			if (menu == null || menu.focused < 0 || menu.entries[menu.focused].Button.IsDisabled())
				return;

			menu.entries[menu.focused].Activate();
		}

		public static void CloseMenu()
		{
			current?.Close();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing && current == this)
				current = null;

			base.Dispose(disposing);
		}
	}
}
