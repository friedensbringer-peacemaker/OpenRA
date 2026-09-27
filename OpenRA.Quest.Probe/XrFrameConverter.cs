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

namespace OpenRA.Quest.Probe
{
	/// <summary>
	/// Crops the BGRA OpenRA intermediate framebuffer and fits it into a
	/// bottom-up RGBA OpenGL swapchain image without changing the game's aspect ratio.
	/// </summary>
	internal static class XrFrameConverter
	{
		public const int BoardWidth = 1280;
		public const int BoardHeight = 800;

		public static byte[] Convert(byte[] bgra, int backingWidth, int width, int height)
		{
			var rgba = new byte[BoardWidth * BoardHeight * 4];
			ConvertInto(bgra, backingWidth, width, height, rgba);
			return rgba;
		}

		/// <summary>Overwrites a reusable destination, including any letterbox area.</summary>
		public static void ConvertInto(byte[] bgra, int backingWidth, int width, int height, byte[] rgba)
			=> ConvertInto(bgra, backingWidth, width, height, rgba, null, 0, 1);

		/// <summary>
		/// Like <see cref="ConvertInto(byte[], int, int, int, byte[])"/>; additionally makes near-black pixels
		/// fully transparent where <paramref name="seeThrough"/> marks the screen block (top-left screen
		/// coordinates, <paramref name="blockSize"/> pixels per block). Passthrough then shows the room there.
		/// </summary>
		public static void ConvertInto(byte[] bgra, int backingWidth, int width, int height, byte[] rgba,
			bool[]? seeThrough, int blocksX, int blockSize)
		{
			ArgumentNullException.ThrowIfNull(bgra);
			ArgumentNullException.ThrowIfNull(rgba);
			if (width <= 0 || height <= 0 || backingWidth < width ||
				(long)backingWidth * height > bgra.Length / 4)
				throw new ArgumentOutOfRangeException(nameof(bgra), "Invalid BGRA framebuffer dimensions.");
			if (rgba.Length != BoardWidth * BoardHeight * 4)
				throw new ArgumentException("Invalid RGBA board buffer length.", nameof(rgba));

			Array.Clear(rgba);
			for (var pixel = 0; pixel < BoardWidth * BoardHeight; pixel++)
				rgba[pixel * 4 + 3] = 255;

			var (drawWidth, drawHeight, offsetX, offsetY) = Fit(width, height);

			for (var y = 0; y < drawHeight; y++)
			{
				// OpenRA flips this intermediate framebuffer when presenting it to the
				// 2D window. Reverse its rows before uploading it to the XR swapchain.
				var sourceY = height - 1 - (int)((long)y * height / drawHeight);
				for (var x = 0; x < drawWidth; x++)
				{
					var sourceX = (int)((long)x * width / drawWidth);
					var source = (sourceY * backingWidth + sourceX) * 4;
					var target = ((y + offsetY) * BoardWidth + x + offsetX) * 4;
					if (seeThrough != null && IsSeeThrough(bgra, source, seeThrough, blocksX, blockSize, sourceX, sourceY))
					{
						rgba[target] = rgba[target + 1] = rgba[target + 2] = rgba[target + 3] = 0;
						continue;
					}

					rgba[target] = bgra[source + 2];
					rgba[target + 1] = bgra[source + 1];
					rgba[target + 2] = bgra[source];
					rgba[target + 3] = 255;
				}
			}
		}

		// Shroud is pure black; a small tolerance keeps soft edges and dark terrain opaque.
		const int SeeThroughMaxChannel = 10;

		static bool IsSeeThrough(byte[] bgra, int source, bool[] mask, int blocksX, int blockSize, int x, int y)
		{
			if (bgra[source] > SeeThroughMaxChannel || bgra[source + 1] > SeeThroughMaxChannel ||
				bgra[source + 2] > SeeThroughMaxChannel)
				return false;

			var block = y / blockSize * blocksX + x / blockSize;
			return block >= 0 && block < mask.Length && mask[block];
		}

		/// <summary>Maps a top-left XR pointer to a top-left OpenRA surface pixel.</summary>
		public static bool TryMapBoardPixel(int x, int y, int width, int height,
			out int sourceX, out int sourceY)
		{
			sourceX = 0;
			sourceY = 0;
			if (width <= 0 || height <= 0 || x < 0 || x >= BoardWidth || y < 0 || y >= BoardHeight)
				return false;

			var (drawWidth, drawHeight, offsetX, offsetY) = Fit(width, height);
			var bottomY = BoardHeight - 1 - y;
			if (x < offsetX || x >= offsetX + drawWidth ||
				bottomY < offsetY || bottomY >= offsetY + drawHeight)
				return false;

			sourceX = (int)((long)(x - offsetX) * width / drawWidth);
			sourceY = height - 1 - (int)((long)(bottomY - offsetY) * height / drawHeight);
			return true;
		}

		static (int DrawWidth, int DrawHeight, int OffsetX, int OffsetY) Fit(int width, int height)
		{
			var scale = Math.Min((double)BoardWidth / width, (double)BoardHeight / height);
			var drawWidth = Math.Min(BoardWidth, Math.Max(1, (int)Math.Round(width * scale)));
			var drawHeight = Math.Min(BoardHeight, Math.Max(1, (int)Math.Round(height * scale)));
			return (drawWidth, drawHeight, (BoardWidth - drawWidth) / 2,
				(BoardHeight - drawHeight) / 2);
		}
	}
}
