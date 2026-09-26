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

		sealed class RecordingInputHandler : IInputHandler
		{
			public readonly List<MouseInput> Events = [];
			public void ModifierKeys(Modifiers mods) { }
			public void OnKeyInput(KeyInput input) { }
			public void OnTextInput(string text) { }
			public void OnMouseInput(MouseInput input) => Events.Add(input);
		}
	}
}
