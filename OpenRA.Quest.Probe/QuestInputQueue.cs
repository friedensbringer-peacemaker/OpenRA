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

using System.Collections.Concurrent;

namespace OpenRA.Quest.Probe
{
	/// <summary>
	/// Transfers Android panel input to OpenRA's GL thread without invoking
	/// game widgets from the Android UI thread.
	/// </summary>
	sealed class QuestInputQueue
	{
		readonly ConcurrentQueue<MouseInput> events = new();
		readonly object stateLock = new();
		int2? lastPosition;
		MouseButton pressedButton;
		Modifiers modifiers;
		bool enabled;

		public void Down(int2 position, MouseButton button)
		{
			lock (stateLock)
			{
				if (!enabled)
					return;

				MoveCore(position);
				UpCore(position);

				pressedButton = button;
				Enqueue(new MouseInput(MouseInputEvent.Down, button, position, int2.Zero, modifiers, 1));
			}
		}

		public void Move(int2 position)
		{
			lock (stateLock)
			{
				if (!enabled)
					return;

				MoveCore(position);
			}
		}

		void MoveCore(int2 position)
		{
			if (lastPosition == position)
				return;

			var delta = lastPosition.HasValue ? position - lastPosition.Value : int2.Zero;
			lastPosition = position;
			Enqueue(new MouseInput(MouseInputEvent.Move, pressedButton, position, delta, modifiers, 0));
		}

		public void Up(int2 position)
		{
			lock (stateLock)
			{
				if (!enabled)
					return;

				MoveCore(position);
				UpCore(position);
			}
		}

		public void Scroll(int2 position, int steps)
		{
			lock (stateLock)
			{
				if (!enabled || steps == 0)
					return;

				MoveCore(position);
				Enqueue(new MouseInput(MouseInputEvent.Scroll, MouseButton.None, position,
					new int2(0, steps), modifiers, 0));
			}
		}

		void UpCore(int2 position)
		{
			if (pressedButton == MouseButton.None)
				return;

			Enqueue(new MouseInput(MouseInputEvent.Up, pressedButton, position, int2.Zero, modifiers, 1));
			pressedButton = MouseButton.None;
		}

		public void Pump(IInputHandler handler)
		{
			Modifiers currentModifiers;
			lock (stateLock)
				currentModifiers = modifiers;

			while (events.TryDequeue(out var input))
			{
				handler.ModifierKeys(input.Modifiers);
				handler.OnMouseInput(input);
			}

			handler.ModifierKeys(currentModifiers);
		}

		public void SetModifiers(Modifiers modifiers)
		{
			lock (stateLock)
				this.modifiers = modifiers;
		}

		public void SetEnabled(bool enabled)
		{
			lock (stateLock)
			{
				this.enabled = enabled;
				if (!enabled)
					modifiers = Modifiers.None;
				while (events.TryDequeue(out _)) { }
				lastPosition = null;
				pressedButton = MouseButton.None;
			}
		}

		void Enqueue(MouseInput input)
			=> events.Enqueue(input);
	}
}
