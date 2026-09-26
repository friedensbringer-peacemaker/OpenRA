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

using System.Collections.Generic;
using NUnit.Framework;
using OpenRA.Quest.Probe;

namespace OpenRA.Test
{
	[TestFixture]
	sealed class QuestInputQueueTest
	{
		[Test]
		public void SendsTouchDragInOrderWithScreenDelta()
		{
			var queue = new QuestInputQueue();
			var handler = new RecordingInputHandler();
			queue.SetEnabled(true);
			queue.Down(new int2(10, 20), MouseButton.Left);
			queue.Move(new int2(15, 27));
			queue.Up(new int2(15, 27));
			queue.Pump(handler);

			Assert.That(handler.Events, Has.Count.EqualTo(4));
			Assert.That(handler.Events[0].Event, Is.EqualTo(MouseInputEvent.Move));
			Assert.That(handler.Events[1].Event, Is.EqualTo(MouseInputEvent.Down));
			Assert.That(handler.Events[1].Button, Is.EqualTo(MouseButton.Left));
			Assert.That(handler.Events[2].Event, Is.EqualTo(MouseInputEvent.Move));
			Assert.That(handler.Events[2].Delta, Is.EqualTo(new int2(5, 7)));
			Assert.That(handler.Events[2].Button, Is.EqualTo(MouseButton.Left));
			Assert.That(handler.Events[3].Event, Is.EqualTo(MouseInputEvent.Up));
			Assert.That(handler.Events[3].Button, Is.EqualTo(MouseButton.Left));
		}

		[Test]
		public void IgnoresTouchesWithoutSessionAndClearsHeldButton()
		{
			var queue = new QuestInputQueue();
			var handler = new RecordingInputHandler();
			queue.Down(new int2(1, 1), MouseButton.Left);
			queue.Pump(handler);
			Assert.That(handler.Events, Is.Empty);

			queue.SetEnabled(true);
			queue.Down(new int2(2, 2), MouseButton.Left);
			queue.SetEnabled(false);
			queue.SetEnabled(true);
			queue.Down(new int2(3, 3), MouseButton.Right);
			queue.Up(new int2(3, 3));
			queue.Pump(handler);

			Assert.That(handler.Events, Has.Count.EqualTo(3));
			Assert.That(handler.Events[0].Event, Is.EqualTo(MouseInputEvent.Move));
			Assert.That(handler.Events[1].Event, Is.EqualTo(MouseInputEvent.Down));
			Assert.That(handler.Events[1].Button, Is.EqualTo(MouseButton.Right));
			Assert.That(handler.Events[2].Event, Is.EqualTo(MouseInputEvent.Up));
			Assert.That(handler.Events[2].Button, Is.EqualTo(MouseButton.Right));
		}

		[Test]
		public void ForwardsZoomAsMouseWheelInput()
		{
			var queue = new QuestInputQueue();
			var handler = new RecordingInputHandler();
			queue.SetEnabled(true);
			queue.Scroll(new int2(50, 60), 4);
			queue.Pump(handler);

			Assert.That(handler.Events, Has.Count.EqualTo(2));
			Assert.That(handler.Events[1].Event, Is.EqualTo(MouseInputEvent.Scroll));
			Assert.That(handler.Events[1].Location, Is.EqualTo(new int2(50, 60)));
			Assert.That(handler.Events[1].Delta, Is.EqualTo(new int2(0, 4)));
		}

		[Test]
		public void ForwardsShiftForAdditiveSelection()
		{
			var queue = new QuestInputQueue();
			var handler = new RecordingInputHandler();
			queue.SetEnabled(true);
			queue.SetModifiers(Modifiers.Shift);
			queue.Down(new int2(20, 30), MouseButton.Left);
			queue.Up(new int2(20, 30));
			queue.Pump(handler);

			Assert.That(handler.LastModifiers, Is.EqualTo(Modifiers.Shift));
			Assert.That(handler.Events[1].Modifiers, Is.EqualTo(Modifiers.Shift));
			Assert.That(handler.Events[2].Modifiers, Is.EqualTo(Modifiers.Shift));
		}

		[Test]
		public void ReleasesInterruptedDragAtItsLastPosition()
		{
			var queue = new QuestInputQueue();
			var handler = new RecordingInputHandler();
			queue.SetEnabled(true);
			queue.Down(new int2(10, 10), MouseButton.Left);
			queue.Move(new int2(20, 10));
			queue.Down(new int2(80, 80), MouseButton.Right);
			queue.Pump(handler);

			Assert.That(handler.Events[3].Event, Is.EqualTo(MouseInputEvent.Up));
			Assert.That(handler.Events[3].Location, Is.EqualTo(new int2(20, 10)));
			Assert.That(handler.Events[4].Event, Is.EqualTo(MouseInputEvent.Move));
			Assert.That(handler.Events[5].Button, Is.EqualTo(MouseButton.Right));
		}

		[Test]
		public void CountsRapidNearbyTapsForDesktopSelectionSemantics()
		{
			long now = 1_000;
			var queue = new QuestInputQueue(() => now);
			var handler = new RecordingInputHandler();
			queue.SetEnabled(true);
			queue.Down(new int2(10, 10), MouseButton.Left);
			queue.Up(new int2(10, 10));
			now += 100;
			queue.Down(new int2(12, 10), MouseButton.Left);
			queue.Up(new int2(12, 10));
			now += 300;
			queue.Down(new int2(12, 10), MouseButton.Left);
			queue.Up(new int2(12, 10));
			queue.Pump(handler);

			var downs = handler.Events.FindAll(e => e.Event == MouseInputEvent.Down);
			var ups = handler.Events.FindAll(e => e.Event == MouseInputEvent.Up);
			Assert.That(downs.ConvertAll(e => e.MultiTapCount), Is.EqualTo(new[] { 1, 2, 1 }));
			Assert.That(ups.ConvertAll(e => e.MultiTapCount), Is.EqualTo(new[] { 1, 2, 1 }));
		}

		sealed class RecordingInputHandler : IInputHandler
		{
			public readonly List<MouseInput> Events = [];
			public Modifiers LastModifiers;
			public void ModifierKeys(Modifiers mods) => LastModifiers = mods;
			public void OnKeyInput(KeyInput input) { }
			public void OnTextInput(string text) { }
			public void OnMouseInput(MouseInput input) => Events.Add(input);
		}
	}
}
