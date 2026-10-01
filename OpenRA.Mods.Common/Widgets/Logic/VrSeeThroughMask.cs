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
using OpenRA.Graphics;
using OpenRA.Widgets;

namespace OpenRA.Mods.Common.Widgets.Logic
{
	/// <summary>
	/// Marks screen blocks that show unexplored map (or space outside the map) for the render
	/// player, so an XR host can let passthrough show through the black shroud there.
	/// </summary>
	public static class VrSeeThroughMask
	{
		/// <summary>Screen pixels per mask block (both directions).</summary>
		public const int Block = 8;

		// OpenRA UI drawn over the battlefield: never punch holes into these, even over shroud.
		static readonly string[] OpaqueWidgets =
		[
			"SIDEBAR_BACKGROUND_TOP", "SIDEBAR_PRODUCTION", "SIDEBAR_MONEYBIN", "COMMAND_BAR",
			"COMMAND_BAR_BACKGROUND", "STANCE_BAR", "SUPPORT_POWERS", "VR_CONTROL_GROUPS", "MUTE_INDICATOR", "CHAT_ROOT"
		];

		/// <summary>
		/// Fills <paramref name="mask"/> (blocksX * blocksY, row-major, top-left origin). Returns false
		/// when no mask applies: no regular world (menu shellmap), no render player, game over or a window open.
		/// </summary>
		public static bool Compute(WorldRenderer worldRenderer, bool[] mask, int blocksX, int blocksY)
		{
			if (worldRenderer == null || Ui.CurrentWindow() != null || worldRenderer.World.Type != WorldType.Regular)
				return false;

			var shroud = worldRenderer.World.RenderPlayer?.Shroud;
			if (shroud == null || worldRenderer.World.IsGameOver)
				return false;

			var viewport = worldRenderer.Viewport;
			for (var by = 0; by < blocksY; by++)
			{
				var cy = by * Block + Block / 2;
				for (var bx = 0; bx < blocksX; bx++)
				{
					var cx = bx * Block + Block / 2;
					var world = worldRenderer.ProjectedPosition(viewport.ViewToWorldPx(new int2(cx, cy)));
					mask[by * blocksX + bx] = !shroud.IsExplored(world);
				}
			}

			foreach (var id in OpaqueWidgets)
			{
				var widget = Ui.Root.GetOrNull(id);
				if (widget != null && widget.IsVisible())
					ClearRect(mask, blocksX, blocksY, widget.RenderBounds.Left, widget.RenderBounds.Top,
						widget.RenderBounds.Right, widget.RenderBounds.Bottom);
			}

			// The production palette grows downwards with more rows: keep the whole sidebar column opaque.
			var sidebar = Ui.Root.GetOrNull("SIDEBAR_BACKGROUND_TOP");
			if (sidebar != null && sidebar.IsVisible())
				ClearRect(mask, blocksX, blocksY, sidebar.RenderBounds.Left, 0, sidebar.RenderBounds.Right, blocksY * Block);

			return true;
		}

		static void ClearRect(bool[] mask, int blocksX, int blocksY, int left, int top, int right, int bottom)
		{
			var x0 = Math.Max(0, left / Block);
			var y0 = Math.Max(0, top / Block);
			var x1 = Math.Min(blocksX - 1, (right - 1) / Block);
			var y1 = Math.Min(blocksY - 1, (bottom - 1) / Block);
			for (var by = y0; by <= y1; by++)
				for (var bx = x0; bx <= x1; bx++)
					mask[by * blocksX + bx] = false;
		}
	}
}
