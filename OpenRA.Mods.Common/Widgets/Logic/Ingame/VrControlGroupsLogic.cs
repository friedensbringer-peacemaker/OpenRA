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

using System.Linq;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	/// <summary>
	/// Control group buttons for XR controllers, which have no number keys:
	/// tap selects the group, holding for <see cref="HoldToSaveMilliseconds"/> stores the current
	/// selection in it (replacing its old members). Only shown when an XR host is active.
	/// </summary>
	public class VrControlGroupsLogic : ChromeLogic
	{
		public const int HoldToSaveMilliseconds = 3000;

		// Below this the button just shows its number; above it the save countdown appears.
		const int CountdownDelayMilliseconds = 400;

		[FluentReference("seconds")]
		const string SaveCountdown = "button-vr-control-group-save-countdown";

		[FluentReference]
		const string Saved = "button-vr-control-group-saved";

		readonly World world;
		readonly GroupButton[] buttons;

		sealed class GroupButton
		{
			public ButtonWidget Widget;
			public int Group;
			public long PressedAt = -1;
			public bool SavedDuringHold;
			public long ShowSavedUntil;

			// Group number and unit count, refreshed once per tick instead of on every draw.
			public string IdleLabel = "";
		}

		[ObjectCreator.UseCtor]
		public VrControlGroupsLogic(Widget widget, World world)
		{
			this.world = world;
			widget.IsVisible = () => VrRuntime.Available && world.LocalPlayer != null && !world.IsGameOver;

			buttons = widget.Children.OfType<ButtonWidget>()
				.Select((button, index) => new GroupButton { Widget = button, Group = index + 1, IdleLabel = (index + 1).ToString(NumberFormatInfo) })
				.ToArray();

			foreach (var entry in buttons)
			{
				var e = entry;
				e.Widget.OnMouseDown = _ =>
				{
					e.PressedAt = Game.RunTime;
					e.SavedDuringHold = false;
				};

				e.Widget.OnMouseUp = _ =>
				{
					if (!e.SavedDuringHold)
						world.ControlGroups.SelectControlGroup(GroupIndex(e.Group));

					e.PressedAt = -1;
				};

				e.Widget.GetText = () => Label(e);
			}
		}

		// OpenRA's control groups are 0-based and ordered 1..9, 0 like the number keys.
		static int GroupIndex(int group) => group - 1;

		string Label(GroupButton e)
		{
			if (Game.RunTime < e.ShowSavedUntil)
				return FluentProvider.GetMessage(Saved);

			var held = e.PressedAt < 0 ? 0 : Game.RunTime - e.PressedAt;
			if (e.Widget.Depressed && !e.SavedDuringHold && held >= CountdownDelayMilliseconds)
			{
				var seconds = (HoldToSaveMilliseconds - held + 999) / 1000;
				return FluentProvider.GetMessage(SaveCountdown, "seconds", seconds);
			}

			return e.IdleLabel;
		}

		static readonly System.Globalization.NumberFormatInfo NumberFormatInfo =
			System.Globalization.CultureInfo.InvariantCulture.NumberFormat;

		public override void Tick()
		{
			foreach (var e in buttons)
			{
				var count = world.ControlGroups.GetActorsInControlGroup(GroupIndex(e.Group)).Count();
				e.IdleLabel = count > 0 ? $"{e.Group} ({count})" : e.Group.ToString(NumberFormatInfo);
			}

			foreach (var e in buttons)
			{
				if (e.PressedAt < 0 || e.SavedDuringHold || !e.Widget.Depressed)
					continue;

				if (Game.RunTime - e.PressedAt < HoldToSaveMilliseconds)
					continue;

				// Never wipe a group by accident: saving needs a non-empty selection.
				e.SavedDuringHold = true;
				if (!world.Selection.Actors.Any())
					continue;

				world.ControlGroups.CreateControlGroup(GroupIndex(e.Group));
				e.ShowSavedUntil = Game.RunTime + 1200;
				VrRuntime.Haptic?.Invoke();
			}
		}
	}
}
