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
using NUnit.Framework;
using OpenRA.Quest.Probe;

namespace OpenRA.Test
{
	[TestFixture]
	sealed class XrFrameConverterTest
	{
		[Test]
		public void CropsBackingRowsAndPreservesBottomUpOrientation()
		{
			var bgra = new byte[4 * 2 * 4];
			SetPixel(0, 0, 0, 0, 255);     // Red at bottom left.
			SetPixel(1, 0, 0, 255, 0);     // Green at bottom right.
			SetPixel(0, 1, 255, 0, 0);     // Blue at top left.
			SetPixel(1, 1, 255, 255, 255); // White at top right.
			SetPixel(3, 0, 255, 0, 255);   // Padding must not appear.
			bgra[3] = 0; // The final board must stay opaque even if a source pixel is transparent.

			var rgba = XrFrameConverter.Convert(bgra, 4, 2, 2);
			AssertPixel(0, 0, 0, 0, 0, 255);
			AssertPixel(256, 0, 255, 0, 0, 255);
			AssertPixel(767, 0, 0, 255, 0, 255);
			AssertPixel(256, 511, 0, 0, 255, 255);
			AssertPixel(767, 511, 255, 255, 255, 255);
			Assert.That(XrFrameConverter.TryMapBoardPixel(0, 0, 2, 2, out _, out _), Is.False);
			Assert.That(XrFrameConverter.TryMapBoardPixel(256, 0, 2, 2, out var topX, out var topY), Is.True);
			Assert.That((topX, topY), Is.EqualTo((0, 0)));
			Assert.That(XrFrameConverter.TryMapBoardPixel(767, 511, 2, 2, out var bottomX, out var bottomY), Is.True);
			Assert.That((bottomX, bottomY), Is.EqualTo((1, 1)));

			void SetPixel(int x, int y, byte blue, byte green, byte red)
			{
				var i = (y * 4 + x) * 4;
				bgra[i] = blue;
				bgra[i + 1] = green;
				bgra[i + 2] = red;
				bgra[i + 3] = 255;
			}

			void AssertPixel(int x, int y, byte red, byte green, byte blue, byte alpha)
			{
				var i = (y * XrFrameConverter.BoardWidth + x) * 4;
				Assert.That(rgba[i], Is.EqualTo(red));
				Assert.That(rgba[i + 1], Is.EqualTo(green));
				Assert.That(rgba[i + 2], Is.EqualTo(blue));
				Assert.That(rgba[i + 3], Is.EqualTo(alpha));
			}
		}

		[Test]
		public void ReusedBufferClearsOldLetterboxPixels()
		{
			var rgba = new byte[XrFrameConverter.BoardWidth * XrFrameConverter.BoardHeight * 4];
			Array.Fill(rgba, (byte)123);
			var bgra = new byte[4 * 4 * 4];
			XrFrameConverter.ConvertInto(bgra, 4, 4, 1, rgba);
			XrFrameConverter.ConvertInto(bgra, 4, 1, 4, rgba);

			const int oldPictureOnly = (256 * XrFrameConverter.BoardWidth + 100) * 4;
			Assert.That(rgba[oldPictureOnly], Is.Zero);
			Assert.That(rgba[oldPictureOnly + 1], Is.Zero);
			Assert.That(rgba[oldPictureOnly + 2], Is.Zero);
			Assert.That(rgba[oldPictureOnly + 3], Is.EqualTo(255));
		}
	}
}
