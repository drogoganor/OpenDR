#region Copyright & License Information
/*
 * Copyright 2007-2018 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Dr.Traits;
using OpenRA.Primitives;

namespace OpenRA.Mods.Dr.SpriteLoaders
{
	public class DrTilLoader : ISpriteLoader
	{
		private class TilHeader
		{
			public string Magic1;
			public int Version;
		}

		private class DrTilFrame : ISpriteFrame
		{
			public SpriteFrameType Type { get; private set; }
			public Size Size { get; private set; }
			public Size FrameSize { get; private set; }
			public float2 Offset { get; set; }
			public byte[] Data { get; set; }
			public bool DisableExportPadding { get { return false; } }

			public DrTilFrame()
			{
				Type = SpriteFrameType.Rgb24;
				const int tileSize = 24;
				Data = new byte[tileSize * tileSize * 3];
				Offset = new float2(0, 0);
				FrameSize = new Size(tileSize, tileSize);
				Size = FrameSize;
			}

			public DrTilFrame(Stream s, bool isMask = false)
			{
				Type = SpriteFrameType.Indexed8;
				const int tileSize = 24;
				Data = new byte[tileSize * tileSize];
				if (!isMask)
				{
					s.ReadUInt8();
				}

				for (var i = 0; i < tileSize * tileSize; i++)
				{
					Data[i] = s.ReadUInt8();
				}

				Offset = new float2(0, 0);
				FrameSize = new Size(tileSize, tileSize);
				Size = FrameSize;
			}
		}

		private static bool IsDrTil(Stream s)
		{
			var start = s.Position;
			var h = new TilHeader()
			{
				Magic1 = s.ReadASCII(4),
				Version = s.ReadInt32()
			};

			if (h.Magic1 != "TILE")
			{
				s.Position = start;
				return false;
			}

			if (h.Version == 0x0240) return true;
			s.Position = start;
			return false;
		}

		private static List<DrTilFrame> ParseFrames(Stream s)
		{
			var start = s.Position;

			var frames = new List<DrTilFrame>();

			for (var variations = 0; variations < 8; variations++)
			{
				for (var types = 0; types < 16; types++)
				{
					var frame = new DrTilFrame(s);
					frames.Add(frame);
				}
			}

			const int chunkSize = 577;

			// Skip water animation
			s.Position += chunkSize * 65;
			s.Position -= 64;

			for (var artIndex = 0; artIndex < 4; artIndex++)
			{
				for (var shoreType = 0; shoreType < 14; shoreType++)
				{
					var frame = new DrTilFrame(s);
					frames.Add(frame);
				}

				// for (var maskType = 0; maskType < 2; maskType++)
				// {
				// 	var frame = new DrTilFrame(s);
				// 	frames.Add(frame);
				// }
				s.Position += 2 * chunkSize; // Skip mask frame
			}

			var something1 = s.ReadUInt8();
			var something2 = s.ReadUInt8();
			var something3 = s.ReadUInt8();
			var maxMaskTiles = 180;
			for (var maskType = 0; maskType < maxMaskTiles; maskType++)
			{
				var frame = new DrTilFrame(s, isMask: true);
				frames.Add(frame);

				if ((maskType + 2) % 4 == 0)
				{
					something1 = s.ReadUInt8(); // ???
					something2 = s.ReadUInt8(); // ???
					something3 = s.ReadUInt8(); // ???
					var something4 = s.ReadUInt8(); // ???
				}
			}

			s.Position = start;
			return frames;
		}

		public bool TryParseSprite(Stream s, string filename, out ISpriteFrame[] frames, out TypeDictionary metadata)
		{
			metadata = null;
			if (!IsDrTil(s))
			{
				frames = null;
				return false;
			}

			var rawFrames = ParseFrames(s);
			frames = BuildTransitionTiles(rawFrames, filename);
			return true;
		}

		private static Color Blend(Color color, Color backColor, double amount)
		{
			var r = (byte)((color.R * amount) + backColor.R * (1 - amount));
			var g = (byte)((color.G * amount) + backColor.G * (1 - amount));
			var b = (byte)((color.B * amount) + backColor.B * (1 - amount));
			return Color.FromArgb(r, g, b);
		}

		private DrTilFrame[] BuildTransitionTiles(List<DrTilFrame> frames, string filename)
		{
			var tiles1 = new[] { 8, 9, 10, 11 };
			var tiles2 = new[] { 16, 17, 18, 19 };
			var masks = new[]
			{
				195, 208, 219, 233, // n, e, w, s
				187, 192, 201, 218, // inner: se, sw, nw, ne
				227, 203, 235, 240 // outer: nw, ne, sw, se, weird artifacts
			};

			// HACK: HACK HACK HACK
			// Assume the palette filename and let's go!
			var paletteFilename = filename.Replace(".TIL", ".PAL");

			using (var s = Game.ModData.DefaultFileSystem.Open(paletteFilename))
			{
				var palette = PaletteFromDrFile.PaletteFromStream(s, new PaletteFromDrFileInfo()
				{
					Filename = paletteFilename,
					TerrainPaletteMultiplier = 6,
				});

				foreach (var mask in masks)
				{
					foreach (var tile in tiles1)
					{
						foreach (var tile2 in tiles2)
						{
							var tileFrame1 = frames[tile];
							var tileFrame2 = frames[tile2];
							var maskFrame = frames[mask];
							const int tileSize = 24;

							var newTile = new DrTilFrame();

							for (var i = 0; i < tileSize * tileSize; i++)
							{
								var index1 = tileFrame1.Data[i];
								var index2 = tileFrame2.Data[i];
								var maskValue = maskFrame.Data[i] * 4;
								var maskAmount = maskValue / 252.0;

								var pixel1 = Color.FromArgb(palette[index1]);
								var pixel2 = Color.FromArgb(palette[index2]);
								var resultPixel = Blend(pixel1, pixel2, maskAmount);

								var startIndex = i * 3;
								newTile.Data[startIndex] = resultPixel.R;
								newTile.Data[startIndex + 1] = resultPixel.G;
								newTile.Data[startIndex + 2] = resultPixel.B;
							}

							frames.Add(newTile);
						}
					}
				}
			}

			return frames.ToArray();
		}
	}
}
