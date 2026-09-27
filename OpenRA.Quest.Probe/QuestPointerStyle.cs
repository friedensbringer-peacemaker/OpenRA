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
using Com.Friedensbringer.Openra.XR;
using OpenRA.Mods.Common.Widgets.Logic;

namespace OpenRA.Quest.Probe
{
	/// <summary>
	/// Ray and target marker style for the native XR layer. The source of truth is OpenRA's
	/// <see cref="VrSettings"/>; a copy in Android preferences styles the ray during the loading
	/// screen, before OpenRA's settings are available.
	/// </summary>
	sealed class QuestPointerStyle
	{
		readonly ISharedPreferences preferences;

		public QuestPointerStyle(Activity activity)
		{
			preferences = activity.GetSharedPreferences("openra-xr-controls", FileCreationMode.Private)!;
		}

		/// <summary>Applies the last known style (used when the XR session starts).</summary>
		public void ApplyCached()
		{
			XrProbe.SetPointerStyle(preferences.GetBoolean("ray_visible", true),
				Math.Clamp(preferences.GetInt("ray_thickness", 1), 0, 2),
				Math.Clamp(preferences.GetInt("ray_color", 0), 0, 3),
				Math.Clamp(preferences.GetInt("target_style", 1), 0, 2));
		}

		/// <summary>Takes over values from OpenRA's VR settings and remembers them for the next start.</summary>
		public void Apply(VrSettings settings)
		{
			var thickness = Math.Clamp(settings.RayThickness, 0, 2);
			var color = Math.Clamp(settings.RayColor, 0, 3);
			var target = Math.Clamp(settings.TargetStyle, 0, 2);
			XrProbe.SetPointerStyle(settings.RayVisible, thickness, color, target);
			preferences.Edit()!.PutBoolean("ray_visible", settings.RayVisible)!.PutInt("ray_thickness", thickness)!
				.PutInt("ray_color", color)!.PutInt("target_style", target)!.Apply();
		}
	}
}
#endif
