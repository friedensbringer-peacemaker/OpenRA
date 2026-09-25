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
using System.Numerics;
using NUnit.Framework;
using OpenRA.Primitives;

namespace OpenRA.Test
{
	[TestFixture]
	sealed class TabletopPointerTest
	{
		static TabletopPointer CreatePointer()
			=> new(Vector3.Zero, Vector3.UnitX, Vector3.UnitZ, 2, 1, new Size(1000, 500));

		[Test]
		public void MapsControllerRayToBoardPixels()
		{
			var pointer = CreatePointer();
			Assert.That(pointer.TryMapRay(new Vector3(0, 1, 0), -Vector3.UnitY, out var center), Is.True);
			Assert.That(center, Is.EqualTo(new int2(500, 250)));

			Assert.That(pointer.TryMapRay(new Vector3(-1, 1, -0.5f), -Vector3.UnitY, out var topLeft), Is.True);
			Assert.That(topLeft, Is.EqualTo(int2.Zero));

			Assert.That(pointer.TryMapRay(new Vector3(1, 1, 0.5f), -Vector3.UnitY, out var bottomRight), Is.True);
			Assert.That(bottomRight, Is.EqualTo(new int2(999, 499)));
		}

		[Test]
		public void RejectsRaysThatMissOrPointAwayFromBoard()
		{
			var pointer = CreatePointer();
			Assert.That(pointer.TryMapRay(new Vector3(2, 1, 0), -Vector3.UnitY, out _), Is.False);
			Assert.That(pointer.TryMapRay(new Vector3(0, 1, 0), Vector3.UnitY, out _), Is.False);
			Assert.That(pointer.TryMapRay(new Vector3(0, 1, 0), Vector3.UnitX, out _), Is.False);
		}

		[Test]
		public void SendsMoveClickDragAndReleaseThroughOpenRaInput()
		{
			var pointer = CreatePointer();
			var handler = new RecordingInputHandler();
			Assert.That(pointer.Update(new Vector3(0, 1, 0), -Vector3.UnitY, MouseButton.None, Modifiers.None, handler), Is.True);
			Assert.That(pointer.Update(new Vector3(0, 1, 0), -Vector3.UnitY, MouseButton.Left, Modifiers.None, handler), Is.True);
			Assert.That(pointer.Update(new Vector3(0.2f, 1, 0), -Vector3.UnitY, MouseButton.Left, Modifiers.None, handler), Is.True);
			Assert.That(pointer.Update(new Vector3(2, 1, 0), -Vector3.UnitY, MouseButton.Left, Modifiers.None, handler), Is.False);

			Assert.That(handler.Events, Has.Count.EqualTo(4));
			Assert.That(handler.Events[0].Event, Is.EqualTo(MouseInputEvent.Move));
			Assert.That(handler.Events[1].Event, Is.EqualTo(MouseInputEvent.Down));
			Assert.That(handler.Events[2].Event, Is.EqualTo(MouseInputEvent.Move));
			Assert.That(handler.Events[2].Delta, Is.EqualTo(new int2(100, 0)));
			Assert.That(handler.Events[3].Event, Is.EqualTo(MouseInputEvent.Up));
			Assert.That(handler.Events[3].Location, Is.EqualTo(new int2(600, 250)));
		}

		[Test]
		public void ForwardsAdditiveSelectionModifier()
		{
			var pointer = CreatePointer();
			var handler = new RecordingInputHandler();
			pointer.Update(new Vector3(0, 1, 0), -Vector3.UnitY, MouseButton.Left, Modifiers.Shift, handler);

			Assert.That(handler.LastModifiers, Is.EqualTo(Modifiers.Shift));
			Assert.That(handler.Events[1].Modifiers, Is.EqualTo(Modifiers.Shift));
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
