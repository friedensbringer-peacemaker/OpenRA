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

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace OpenRA.Quest.Probe
{
	/// <summary>
	/// Transfers Android panel input to OpenRA's GL thread without invoking
	/// game widgets from the Android UI thread.
	/// </summary>
	sealed class QuestInputQueue
	{
		const long MultiTapWindowMilliseconds = 250;
		const int MultiTapDistancePixels = 4;

		readonly ConcurrentQueue<MouseInput> events = new();
		readonly object stateLock = new();
		readonly Dictionary<MouseButton, TapHistory> tapHistory = new();
		readonly Func<long> nowMilliseconds;
		int2? lastPosition;
		MouseButton pressedButton;
		int pressedTapCount;
		Modifiers modifiers;
		bool enabled;

		public QuestInputQueue(Func<long>? nowMilliseconds = null)
		{
			this.nowMilliseconds = nowMilliseconds ?? (() => Environment.TickCount64);
		}

		public void Down(int2 position, MouseButton button)
		{
			lock (stateLock)
			{
				if (!enabled)
					return;

				// A missing Android Up must end a held drag at its last actual
				// position, not at the next finger's starting position.
				UpCore(lastPosition ?? position);
				MoveCore(position);

				pressedButton = button;
				pressedTapCount = DetectTapCount(button, position);
				Enqueue(new MouseInput(MouseInputEvent.Down, button, position, int2.Zero, modifiers, pressedTapCount));
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

			Enqueue(new MouseInput(MouseInputEvent.Up, pressedButton, position, int2.Zero, modifiers, pressedTapCount));
			pressedButton = MouseButton.None;
			pressedTapCount = 0;
		}

		int DetectTapCount(MouseButton button, int2 position)
		{
			var now = nowMilliseconds();
			var count = 1;
			if (tapHistory.TryGetValue(button, out var previous) &&
				now >= previous.Time && now - previous.Time < MultiTapWindowMilliseconds &&
				(position - previous.Position).Length < MultiTapDistancePixels)
				count = Math.Min(previous.Count + 1, 3);

			tapHistory[button] = new TapHistory(now, position, count);
			return count;
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
				pressedTapCount = 0;
				tapHistory.Clear();
			}
		}

		readonly record struct TapHistory(long Time, int2 Position, int Count);

		void Enqueue(MouseInput input)
			=> events.Enqueue(input);
	}
}
