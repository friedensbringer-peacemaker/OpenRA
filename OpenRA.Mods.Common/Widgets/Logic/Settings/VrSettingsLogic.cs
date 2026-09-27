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
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	/// <summary>Settings for head-mounted (XR) hosts. Stored in settings.yaml under "Vr".</summary>
	[YamlNode("Vr", shared: true)]
	public class VrSettings : SettingsModule
	{
		[Desc("Show the controller ray.")]
		public bool RayVisible = true;

		[Desc("Ray thickness: 0 thin, 1 medium, 2 thick.")]
		public int RayThickness = 1;

		[Desc("Ray color: 0 white, 1 warm yellow, 2 blue, 3 grey.")]
		public int RayColor = 0;

		[Desc("Target marker: 0 dot, 1 ring, 2 cross.")]
		public int TargetStyle = 1;

		[Desc("Distance of the game board in front of the head, in meters.")]
		public float BoardDistance = 1.4f;

		[Desc("Width of the game board in meters. The height follows the 16:10 aspect ratio.")]
		public float BoardWidth = 1.6f;

		[Desc("Vertical offset of the board center from eye height, in meters.")]
		public float BoardHeightOffset = 0f;

		public VrSettings Clone()
		{
			return (VrSettings)MemberwiseClone();
		}
	}

	/// <summary>Hooks provided by an XR host. Desktop builds leave them unset and hide the VR panel.</summary>
	public static class VrRuntime
	{
		public static bool Available;

		/// <summary>Applies changed settings to the XR presentation immediately.</summary>
		public static Action<VrSettings> Apply;

		/// <summary>Places the board in front of the current gaze again.</summary>
		public static Action Recenter;

		/// <summary>Deploys the selection (MCV etc.), like the F key.</summary>
		public static Action Deploy;

		/// <summary>Opens OpenRA's in-game menu, like the Escape key.</summary>
		public static Action OpenGameMenu;

		/// <summary>Short controller vibration to confirm an action (optional).</summary>
		public static Action Haptic;
	}

	public class VrSettingsLogic : ChromeLogic
	{
		[FluentReference]
		const string Thin = "options-vr-ray-thickness.thin";

		[FluentReference]
		const string Medium = "options-vr-ray-thickness.medium";

		[FluentReference]
		const string Thick = "options-vr-ray-thickness.thick";

		[FluentReference]
		const string White = "options-vr-ray-color.white";

		[FluentReference]
		const string WarmYellow = "options-vr-ray-color.warm-yellow";

		[FluentReference]
		const string Blue = "options-vr-ray-color.blue";

		[FluentReference]
		const string Grey = "options-vr-ray-color.grey";

		[FluentReference]
		const string Dot = "options-vr-target.dot";

		[FluentReference]
		const string Ring = "options-vr-target.ring";

		[FluentReference]
		const string Cross = "options-vr-target.cross";

		readonly VrSettings vrSettings;

		[ObjectCreator.UseCtor]
		public VrSettingsLogic(ModData modData, SettingsLogic settingsLogic, string panelID, string label)
		{
			vrSettings = modData.GetSettings<VrSettings>();

			// Only XR hosts show the panel; on desktop the tab is never created.
			if (VrRuntime.Available)
				settingsLogic.RegisterSettingsPanel(panelID, label, InitPanel, ResetPanel);
		}

		void Apply() => VrRuntime.Apply?.Invoke(vrSettings);

		Func<bool> InitPanel(Widget panel)
		{
			var scrollPanel = panel.Get<ScrollPanelWidget>("SETTINGS_SCROLLPANEL");

			var rayCheckbox = panel.Get<CheckboxWidget>("RAY_VISIBLE");
			rayCheckbox.IsChecked = () => vrSettings.RayVisible;
			rayCheckbox.OnClick = () => { vrSettings.RayVisible ^= true; Apply(); };

			BindChoice(panel.Get<DropDownButtonWidget>("RAY_THICKNESS_DROPDOWN"),
				[Thin, Medium, Thick], () => vrSettings.RayThickness, v => vrSettings.RayThickness = v);
			BindChoice(panel.Get<DropDownButtonWidget>("RAY_COLOR_DROPDOWN"),
				[White, WarmYellow, Blue, Grey], () => vrSettings.RayColor, v => vrSettings.RayColor = v);
			BindChoice(panel.Get<DropDownButtonWidget>("TARGET_STYLE_DROPDOWN"),
				[Dot, Ring, Cross], () => vrSettings.TargetStyle, v => vrSettings.TargetStyle = v);

			BindMeters(panel, "BOARD_DISTANCE", () => vrSettings.BoardDistance, v => vrSettings.BoardDistance = v, "m");
			BindMeters(panel, "BOARD_WIDTH", () => vrSettings.BoardWidth, v => vrSettings.BoardWidth = v, "m");
			BindMeters(panel, "BOARD_HEIGHT", () => vrSettings.BoardHeightOffset, v => vrSettings.BoardHeightOffset = v, "cm");

			panel.Get<ButtonWidget>("RECENTER_BUTTON").OnClick = () => VrRuntime.Recenter?.Invoke();

			SettingsUtils.AdjustSettingsScrollPanelLayout(scrollPanel);
			return () => false;
		}

		void BindChoice(DropDownButtonWidget dropdown, string[] labels, Func<int> get, Action<int> set)
		{
			var options = new List<int>();
			for (var i = 0; i < labels.Length; i++)
				options.Add(i);

			dropdown.GetText = () => FluentProvider.GetMessage(labels[Math.Clamp(get(), 0, labels.Length - 1)]);
			dropdown.OnMouseDown = _ =>
			{
				ScrollItemWidget SetupItem(int option, ScrollItemWidget template)
				{
					var item = ScrollItemWidget.Setup(template, () => get() == option, () => { set(option); Apply(); });
					item.Get<LabelWidget>("LABEL").GetText = () => FluentProvider.GetMessage(labels[option]);
					return item;
				}

				dropdown.ShowDropDown("LABEL_DROPDOWN_TEMPLATE", 500, options, SetupItem);
			};
		}

		void BindMeters(Widget panel, string id, Func<float> get, Action<float> set, string unit)
		{
			var slider = panel.Get<SliderWidget>(id);
			slider.Value = get();
			slider.OnChange += v =>
			{
				// Snap to 5 cm so the value label stays readable.
				set(MathF.Round(v * 20f) / 20f);
				Apply();
			};

			var valueLabel = panel.Get<LabelWidget>(id + "_VALUE");
			valueLabel.GetText = () => unit == "cm"
				? $"{MathF.Round(get() * 100f):+0;-0;0} cm"
				: $"{get():0.00} m";
		}

		Action ResetPanel(Widget panel)
		{
			var defaults = new VrSettings();
			return () =>
			{
				vrSettings.RayVisible = defaults.RayVisible;
				vrSettings.RayThickness = defaults.RayThickness;
				vrSettings.RayColor = defaults.RayColor;
				vrSettings.TargetStyle = defaults.TargetStyle;
				vrSettings.BoardDistance = defaults.BoardDistance;
				vrSettings.BoardWidth = defaults.BoardWidth;
				vrSettings.BoardHeightOffset = defaults.BoardHeightOffset;
				panel.Get<SliderWidget>("BOARD_DISTANCE").Value = defaults.BoardDistance;
				panel.Get<SliderWidget>("BOARD_WIDTH").Value = defaults.BoardWidth;
				panel.Get<SliderWidget>("BOARD_HEIGHT").Value = defaults.BoardHeightOffset;
				Apply();
			};
		}
	}
}
