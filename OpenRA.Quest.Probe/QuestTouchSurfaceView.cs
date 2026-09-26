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
	sealed class QuestTouchSurfaceView(Context context, QuestInputQueue input, Func<MouseButton> currentButton,
		Func<bool> acceptsTouch)
		: GLSurfaceView(context)
	{
		int activePointerId = -1;
		int2? lastTouchPosition;

		public override bool OnTouchEvent(MotionEvent? e)
		{
			if (!acceptsTouch())
			{
				activePointerId = -1;
				lastTouchPosition = null;
				return true;
			}

			if (e == null || Width <= 0 || Height <= 0)
				return base.OnTouchEvent(e);

			switch (e.ActionMasked)
			{
				case MotionEventActions.Down:
					activePointerId = e.GetPointerId(e.ActionIndex);
					lastTouchPosition = Position(e, e.ActionIndex);
					input.Down(lastTouchPosition.Value, currentButton());
					return true;
				case MotionEventActions.PointerDown:
					// Keep the original finger as the mouse pointer. A second finger
					// must not turn a drag into a click or a context order.
					return true;
				case MotionEventActions.Move:
					var moveIndex = e.FindPointerIndex(activePointerId);
					if (moveIndex >= 0)
					{
						lastTouchPosition = Position(e, moveIndex);
						input.Move(lastTouchPosition.Value);
					}
					else
						ReleaseLastTouch();

					return true;
				case MotionEventActions.PointerUp:
					if (e.GetPointerId(e.ActionIndex) == activePointerId)
						ReleaseTouch(Position(e, e.ActionIndex));

					return true;
				case MotionEventActions.Up:
					if (activePointerId >= 0)
						ReleaseTouch(Position(e, e.ActionIndex));

					return true;
				case MotionEventActions.Cancel:
					ReleaseLastTouch();
					return true;
				default:
					return base.OnTouchEvent(e);
			}
		}

		int2 Position(MotionEvent e, int pointerIndex)
			=> new(
				Math.Clamp((int)e.GetX(pointerIndex), 0, Width - 1),
				Math.Clamp((int)e.GetY(pointerIndex), 0, Height - 1));

		void ReleaseTouch(int2 position)
		{
			input.Up(position);
			activePointerId = -1;
			lastTouchPosition = null;
		}

		void ReleaseLastTouch()
		{
			if (lastTouchPosition.HasValue)
				ReleaseTouch(lastTouchPosition.Value);
			else
				activePointerId = -1;
		}

		public void CancelTouch()
		{
			if (activePointerId >= 0)
				ReleaseLastTouch();
		}

		public override bool OnGenericMotionEvent(MotionEvent? e)
		{
			if (!acceptsTouch())
				return true;

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
