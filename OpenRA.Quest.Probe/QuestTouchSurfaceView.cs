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

using Android.Content;
using Android.Opengl;
using Android.Views;

namespace OpenRA.Quest.Probe
{
	/// <summary>
	/// Turns touches on the flat Android game panel into queued OpenRA mouse
	/// events. The selectable button lets the panel issue RTS context orders.
	/// </summary>
	sealed class QuestTouchSurfaceView(Context context, QuestInputQueue input, Func<MouseButton> currentButton)
		: GLSurfaceView(context)
	{
		public override bool OnTouchEvent(MotionEvent? e)
		{
			if (e == null || Width <= 0 || Height <= 0)
				return base.OnTouchEvent(e);

			var position = new int2(
				Math.Clamp((int)e.GetX(), 0, Width - 1),
				Math.Clamp((int)e.GetY(), 0, Height - 1));

			switch (e.ActionMasked)
			{
				case MotionEventActions.Down:
					input.Down(position, currentButton());
					return true;
				case MotionEventActions.Move:
					input.Move(position);
					return true;
				case MotionEventActions.Up:
				case MotionEventActions.Cancel:
					input.Up(position);
					return true;
				default:
					return base.OnTouchEvent(e);
			}
		}

		public override bool OnGenericMotionEvent(MotionEvent? e)
		{
			if (e != null && e.ActionMasked == MotionEventActions.HoverMove && Width > 0 && Height > 0)
			{
				input.Move(new int2(
					Math.Clamp((int)e.GetX(), 0, Width - 1),
					Math.Clamp((int)e.GetY(), 0, Height - 1)));
				return true;
			}

			return base.OnGenericMotionEvent(e);
		}
	}
}
