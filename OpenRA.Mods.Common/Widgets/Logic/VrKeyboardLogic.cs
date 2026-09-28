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
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	/// <summary>
	/// On-screen keyboard for XR controllers. Appears while a text field has keyboard focus and an
	/// XR host is active; keys type into that text field like a physical keyboard would.
	/// </summary>
	public class VrKeyboardLogic : ChromeLogic
	{
		const string WidgetId = "VR_KEYBOARD";
		const int KeyWidth = 56;
		const int KeyHeight = 44;
		const int Gap = 6;
		const int Margin = 16;
		const int TitleHeight = 30;

		[FluentReference]
		const string BackspaceLabel = "button-vr-keyboard-backspace";

		[FluentReference]
		const string EnterLabel = "button-vr-keyboard-enter";

		[FluentReference]
		const string ShiftLabel = "button-vr-keyboard-shift";

		[FluentReference]
		const string SpaceLabel = "button-vr-keyboard-space";

		[FluentReference]
		const string CloseLabel = "button-vr-keyboard-close";

		static readonly string[] Rows = ["1234567890ß", "qwertzuiopü", "asdfghjklöä", "yxcvbnm,.-"];

		static Widget current;
		static TextFieldWidget target;

		bool shift;

		[ObjectCreator.UseCtor]
		public VrKeyboardLogic(Widget widget)
		{
			var template = widget.Get<ButtonWidget>("KEY_TEMPLATE");
			widget.RemoveChild(template);

			for (var row = 0; row < Rows.Length; row++)
			{
				var y = TitleHeight + row * (KeyHeight + Gap);
				var x = Margin + (row == 3 ? KeyWidth * 2 + Gap * 2 : row * (KeyWidth / 2));
				foreach (var c in Rows[row])
				{
					var key = c.ToString();
					AddKey(widget, template, x, y, KeyWidth, () => shift ? key.ToUpperInvariant() : key, () =>
					{
						Type(shift ? key.ToUpperInvariant() : key);
						shift = false;
					});
					x += KeyWidth + Gap;
				}

				if (row == 0)
					AddKey(widget, template, x, y, KeyWidth * 2 + Gap, Label(BackspaceLabel), () => Press(Keycode.BACKSPACE));
				else if (row == 2)
					AddKey(widget, template, x, y, KeyWidth * 2 + Gap, Label(EnterLabel), () => Press(Keycode.RETURN));
			}

			var shiftKey = AddKey(widget, template, Margin, TitleHeight + 3 * (KeyHeight + Gap), KeyWidth * 2 + Gap,
				Label(ShiftLabel), () => shift ^= true);
			shiftKey.IsHighlighted = () => shift;

			var bottom = TitleHeight + 4 * (KeyHeight + Gap);
			AddKey(widget, template, Margin + (KeyWidth + Gap) * 2, bottom, (KeyWidth + Gap) * 7 - Gap,
				Label(SpaceLabel), () => Type(" "));
			AddKey(widget, template, Margin + (KeyWidth + Gap) * 10, bottom, KeyWidth * 3 + Gap * 2,
				Label(CloseLabel), Close);
		}

		static Func<string> Label(string key)
		{
			var text = FluentProvider.GetMessage(key);
			return () => text;
		}

		static ButtonWidget AddKey(Widget parent, ButtonWidget template, int x, int y, int width,
			Func<string> text, Action onClick)
		{
			var key = (ButtonWidget)template.Clone();
			key.Bounds = new WidgetBounds(x, y, width, KeyHeight);
			key.GetText = text;
			key.OnClick = onClick;
			parent.AddChild(key);
			return key;
		}

		static void Type(string text)
		{
			target?.HandleTextInput(text);
		}

		static void Press(Keycode key)
		{
			target?.HandleKeyPress(new KeyInput { Event = KeyInputEvent.Down, Key = key });
			target?.HandleKeyPress(new KeyInput { Event = KeyInputEvent.Up, Key = key });
		}

		static void Close()
		{
			target?.YieldKeyboardFocus();
			Hide();
		}

		static void Hide()
		{
			target = null;
			current?.Parent?.RemoveChild(current);
			current = null;
		}

		static bool IsAttached(Widget widget)
		{
			for (var w = widget; w != null; w = w.Parent)
			{
				if (!w.IsVisible())
					return false;

				if (w == Ui.Root)
					return true;
			}

			return false;
		}

		/// <summary>
		/// Shows or hides the keyboard to match the focused text field. The XR host calls this
		/// once per frame on the game thread. Does nothing without an XR host.
		/// </summary>
		public static void Update(World world)
		{
			if (!VrRuntime.Available)
				return;

			var focused = Ui.KeyboardFocusWidget as TextFieldWidget;
			if (focused == null || focused.IsDisabled() || !IsAttached(focused))
			{
				if (current != null)
					Hide();

				return;
			}

			if (current != null && current.Parent == null)
				current = null;

			target = focused;
			if (current == null)
				current = Game.LoadWidget(world, WidgetId, Ui.Root, new WidgetArgs());

			// Keep the edited field visible: keyboard at the top when the field sits in the lower half.
			var screen = Game.Renderer.Resolution;
			var fieldCenter = focused.RenderBounds.Y + focused.RenderBounds.Height / 2;
			current.Bounds.X = (screen.Width - current.Bounds.Width) / 2;
			current.Bounds.Y = fieldCenter > screen.Height / 2
				? Margin
				: screen.Height - current.Bounds.Height - Margin;
		}
	}
}
