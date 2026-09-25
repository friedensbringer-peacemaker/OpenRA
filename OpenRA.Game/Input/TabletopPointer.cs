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
using System.Numerics;
using OpenRA.Primitives;

namespace OpenRA
{
	/// <summary>
	/// Maps a tracked controller ray onto a rectangular tabletop and forwards it
	/// through the same input interface used by the desktop platform.
	/// Coordinates are in metres; right and down are unit vectors along the board.
	/// </summary>
	public sealed class TabletopPointer
	{
		static readonly MouseButton[] SupportedButtons = [MouseButton.Left, MouseButton.Right, MouseButton.Middle];

		readonly Vector3 center;
		readonly Vector3 right;
		readonly Vector3 down;
		readonly Vector3 normal;
		readonly float width;
		readonly float height;
		readonly Size resolution;
		int2? lastPosition;
		MouseButton pressedButtons;

		public TabletopPointer(Vector3 center, Vector3 right, Vector3 down, float width, float height, Size resolution)
		{
			if (!IsFinite(center) || !IsFinite(right) || !IsFinite(down) ||
				!float.IsFinite(width) || !float.IsFinite(height) || width <= 0 || height <= 0 ||
				resolution.Width <= 0 || resolution.Height <= 0 ||
				MathF.Abs(right.LengthSquared() - 1) > 0.001f ||
				MathF.Abs(down.LengthSquared() - 1) > 0.001f ||
				MathF.Abs(Vector3.Dot(right, down)) > 0.001f)
				throw new ArgumentException("The tabletop needs finite dimensions and perpendicular unit axes.");

			this.center = center;
			this.right = right;
			this.down = down;
			this.width = width;
			this.height = height;
			this.resolution = resolution;
			normal = Vector3.Cross(right, down);
		}

		public bool TryMapRay(Vector3 origin, Vector3 direction, out int2 position)
		{
			position = int2.Zero;
			if (!IsFinite(origin) || !IsFinite(direction))
				return false;

			var denominator = Vector3.Dot(direction, normal);
			if (MathF.Abs(denominator) < 0.000001f)
				return false;

			var distance = Vector3.Dot(center - origin, normal) / denominator;
			if (!float.IsFinite(distance) || distance < 0)
				return false;

			var offset = origin + direction * distance - center;
			var u = Vector3.Dot(offset, right) / width + 0.5f;
			var v = Vector3.Dot(offset, down) / height + 0.5f;
			if (!float.IsFinite(u) || !float.IsFinite(v) || u < 0 || u > 1 || v < 0 || v > 1)
				return false;

			position = new int2(
				Math.Min((int)(u * resolution.Width), resolution.Width - 1),
				Math.Min((int)(v * resolution.Height), resolution.Height - 1));
			return true;
		}

		/// <summary>
		/// Call once per input sample. A missed ray releases a held button at its
		/// last valid position, so dragging cannot remain stuck outside the board.
		/// </summary>
		public bool Update(Vector3 origin, Vector3 direction, MouseButton buttons, Modifiers modifiers, IInputHandler handler)
		{
			ArgumentNullException.ThrowIfNull(handler);
			handler.ModifierKeys(modifiers);

			if (!TryMapRay(origin, direction, out var position))
			{
				if (lastPosition.HasValue)
					ReleaseButtons(pressedButtons, lastPosition.Value, modifiers, handler);

				pressedButtons = MouseButton.None;
				lastPosition = null;
				return false;
			}

			if (!lastPosition.HasValue || position != lastPosition.Value)
				handler.OnMouseInput(new MouseInput(MouseInputEvent.Move, pressedButtons, position,
					lastPosition.HasValue ? position - lastPosition.Value : int2.Zero, modifiers, 0));

			ReleaseButtons(pressedButtons & ~buttons, position, modifiers, handler);
			foreach (var button in SupportedButtons)
				if ((buttons & ~pressedButtons & button) != 0)
					handler.OnMouseInput(new MouseInput(MouseInputEvent.Down, button, position, int2.Zero, modifiers, 1));

			pressedButtons = buttons;
			lastPosition = position;
			return true;
		}

		void ReleaseButtons(MouseButton buttons, int2 position, Modifiers modifiers, IInputHandler handler)
		{
			foreach (var button in SupportedButtons)
				if ((buttons & button) != 0)
					handler.OnMouseInput(new MouseInput(MouseInputEvent.Up, button, position, int2.Zero, modifiers, 1));
		}

		static bool IsFinite(Vector3 value)
			=> float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
	}
}
