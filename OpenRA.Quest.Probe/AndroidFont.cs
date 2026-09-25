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

using System.IO;
using System.Security.Cryptography;
using Android.App;
using Android.Graphics;
using Java.Nio;
using OpenRA.Primitives;

namespace OpenRA.Quest.Probe
{
	/// <summary>
	/// Rasterizes the font bytes supplied by OpenRA with Android's text API.
	/// The temporary font file is needed by Typeface.CreateFromFile.
	/// </summary>
	sealed class AndroidFont : IFont
	{
		readonly Typeface typeface;
		bool disposed;

		public AndroidFont(byte[] data)
		{
			var cacheDir = Application.Context?.CacheDir?.AbsolutePath
				?? throw new InvalidOperationException("Android cache storage is unavailable.");
			var name = $"openra-font-{Convert.ToHexString(SHA256.HashData(data))}.ttf";
			var path = System.IO.Path.Combine(cacheDir, name);
			if (!File.Exists(path))
				File.WriteAllBytes(path, data);

			typeface = Typeface.CreateFromFile(path)
				?? throw new InvalidDataException("Android could not load the OpenRA font.");
		}

		public FontGlyph CreateGlyph(char c, int size, float deviceScale)
		{
			ObjectDisposedException.ThrowIf(disposed, this);
			using var paint = new Paint(PaintFlags.AntiAlias)
			{
				TextSize = size * deviceScale,
				Color = Android.Graphics.Color.White
			};
			paint.SetTypeface(typeface);
			var text = c.ToString();
			using var bounds = new Rect();
			paint.GetTextBounds(text, 0, text.Length, bounds);
			var width = Math.Max(0, bounds.Width());
			var height = Math.Max(0, bounds.Height());
			var glyph = new FontGlyph
			{
				Offset = new int2(bounds.Left, bounds.Top),
				Size = new Size(width, height),
				Advance = paint.MeasureText(text),
				Data = new byte[width * height]
			};

			if (width == 0 || height == 0)
				return glyph;

			using var bitmap = Bitmap.CreateBitmap(width, height, Bitmap.Config.Alpha8!);
			using var canvas = new Canvas(bitmap);
			canvas.DrawText(text, -bounds.Left, -bounds.Top, paint);
			var stride = bitmap.RowBytes;
			using var buffer = ByteBuffer.Allocate(stride * height);
			bitmap.CopyPixelsToBuffer(buffer);
			var padded = new byte[stride * height];
			buffer.Position(0);
			buffer.Get(padded);
			for (var y = 0; y < height; y++)
				Array.Copy(padded, y * stride, glyph.Data, y * width, width);

			return glyph;
		}

		public void Dispose()
		{
			if (disposed)
				return;

			typeface.Dispose();
			disposed = true;
		}
	}
}
