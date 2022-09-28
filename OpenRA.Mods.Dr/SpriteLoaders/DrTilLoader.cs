#region Copyright & License Information
/*
 * Copyright 2007-2022 The OpenRA Developers (see AUTHORS)
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
using OpenRA.FileFormats;
using OpenRA.Graphics;
using OpenRA.Mods.Dr.Traits;
using OpenRA.Primitives;
using Size = OpenRA.Primitives.Size;

namespace OpenRA.Mods.Dr.SpriteLoaders
{
	public class DrTilLoader : ISpriteLoader
	{
		public class TilHeader
		{
			public string Magic1;
			public int Version;
		}

		public class DrTilFrame : ISpriteFrame
		{
			const int TileSize = 24;

			public SpriteFrameType Type { get; private set; }
			public Size Size { get; private set; }
			public Size FrameSize { get; private set; }
			public float2 Offset { get; set; }
			public byte[] Data { get; set; }
			public bool DisableExportPadding { get { return false; } }

			public static int Counter = 0;

			public DrTilFrame(SpriteFrameType type)
			{
				Type = type;
				var size = TileSize * TileSize;
				if (type == SpriteFrameType.Rgba32)
				{
					size *= 4;
				}

				Data = new byte[size];
				Offset = new float2(0, 0);
				FrameSize = new Size(TileSize, TileSize);
				Size = FrameSize;
			}

			public DrTilFrame(Stream s, bool isMask = false, ImmutablePalette palette = null)
			{
				Type = SpriteFrameType.Indexed8;

				Data = new byte[TileSize * TileSize];

				if (!isMask)
				{
					s.ReadUInt8();
				}

				for (var i = 0; i < TileSize * TileSize; i++)
				{
					Data[i] = s.ReadUInt8();
				}

				var rgbByteArray = new List<byte>();
				for (var i = 0; i < TileSize * TileSize; i++)
				{
					if (palette == null)
					{
						var byteVal = (byte)(Data[i] * 4);
						rgbByteArray.AddRange(new[] { byteVal, byteVal, byteVal, (byte)255 });
					}
					else
					{
						var color = palette.GetColor(Data[i]);
						rgbByteArray.AddRange(new[] { color.R, color.G, color.B, (byte)255 });
					}
				}

				var png = new Png(rgbByteArray.ToArray(), SpriteFrameType.Rgba32, TileSize, TileSize);
				png.Save($"C:\\temp\\{Counter:D4}-til.png");
				Counter++;

				Offset = new float2(0, 0);
				FrameSize = new Size(TileSize, TileSize);
				Size = FrameSize;
			}
		}

		static bool IsDrTil(Stream s)
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

		static DrTilFrame[] ParseFrames(Stream s, ImmutablePalette palette)
		{
			var start = s.Position;

			var frames = new List<DrTilFrame>();

			for (var variations = 0; variations < 8; variations++)
			{
				for (var types = 0; types < 16; types++)
				{
					var frame = new DrTilFrame(s, palette: palette);
					frames.Add(frame);
				}
			}

			for (var waterFrame = 0; waterFrame < 64; waterFrame++)
			{
				var frame = new DrTilFrame(s, isMask: true, palette: palette);
				frames.Add(frame);
			}

			const int ChunkSize = 577;

			for (var artIndex = 0; artIndex < 4; artIndex++)
			{
				// Skip empty tile
				s.Position += ChunkSize;

				for (var shoreType = 0; shoreType < 14; shoreType++)
				{
					var frame = new DrTilFrame(s, palette: palette);
					frames.Add(frame);
				}

				// Skip empty tile
				s.Position += ChunkSize;
			}

			var maskFrames = new List<DrTilFrame>();
			for (var maskType = 0; maskType < 256; maskType++)
			{
				if (maskType % 4 == 0)
				{
					var something1 = s.ReadUInt8(); // ???
					var something2 = s.ReadUInt8(); // ???
					var something3 = s.ReadUInt8(); // ???
					var something4 = s.ReadUInt8(); // ???
				}

				var frame = new DrTilFrame(s, isMask: true);
				maskFrames.Add(frame);

				// frames.Add(frame);
			}

			for (var maskTemplateType = 0; maskTemplateType < 4; maskTemplateType++)
			{
				var frame = new DrTilFrame(s, isMask: true);

				// frames.Add(frame);
			}

			var cornerIndices = new List<List<int>>
			{
				// Rough
				new List<int> { 0, 1, 2, 3 },

				// Crumbled
				new List<int> { 64, 65, 66, 67 },

				// Square
				new List<int> { 128, 129, 130, 131 },

				// Blob
				new List<int> { 192, 193, 194, 195 },
			};

			foreach (var cornerSet in cornerIndices)
			{
				var se = maskFrames[cornerSet[0]];
				var sw = maskFrames[cornerSet[1]];
				var nw = maskFrames[cornerSet[2]];
				var ne = maskFrames[cornerSet[3]];

				// Create edge tiles by adding
				var north = CombineMaskTiles(nw, ne);
				var east = CombineMaskTiles(se, ne);
				var south = CombineMaskTiles(sw, se);
				var west = CombineMaskTiles(sw, nw);

				// frames.AddRange(new[] { MaskToRgbaTile(north), MaskToRgbaTile(east), MaskToRgbaTile(south), MaskToRgbaTile(west) });
				var neInner = CombineMaskTiles(north, east);
				var nwInner = CombineMaskTiles(north, west);
				var swInner = CombineMaskTiles(south, west);
				var seInner = CombineMaskTiles(south, east);

				// frames.AddRange(new[] { MaskToRgbaTile(neInner), MaskToRgbaTile(nwInner), MaskToRgbaTile(swInner), MaskToRgbaTile(seInner) });
				for (var tileIndex = 2; tileIndex < 16; tileIndex++)
				{
					var mappedTileIndex = tileIndex * 4;
					var sourceTile = frames[mappedTileIndex];

					// Starts at 248
					frames.Add(MaskTile(sourceTile, se));
					frames.Add(MaskTile(sourceTile, sw));
					frames.Add(MaskTile(sourceTile, nw));
					frames.Add(MaskTile(sourceTile, ne));
					frames.Add(MaskTile(sourceTile, south));
					frames.Add(MaskTile(sourceTile, west));
					frames.Add(MaskTile(sourceTile, north));
					frames.Add(MaskTile(sourceTile, east));
					frames.Add(MaskTile(sourceTile, seInner));
					frames.Add(MaskTile(sourceTile, swInner));
					frames.Add(MaskTile(sourceTile, nwInner));
					frames.Add(MaskTile(sourceTile, neInner));
				}
			}

			s.Position = start;
			return frames.ToArray();

			////////////////////////////////////////////////

			DrTilFrame MaskTile(DrTilFrame source, DrTilFrame mask)
			{
				var newFrame = new DrTilFrame(SpriteFrameType.Rgba32);
				for (var i = 0; i < 24 * 24; i++)
				{
					var newIndex = i * 4;
					var color = palette.GetColor(source.Data[i]);
					newFrame.Data[newIndex] = color.R;
					newFrame.Data[newIndex + 1] = color.G;
					newFrame.Data[newIndex + 2] = color.B;
					newFrame.Data[newIndex + 3] = (byte)Math.Min(255, mask.Data[i] * 4);
				}

				return newFrame;
			}

			DrTilFrame CombineMaskTiles(DrTilFrame a, DrTilFrame b)
			{
				var newFrame = new DrTilFrame(SpriteFrameType.Indexed8);
				for (var i = 0; i < 24 * 24; i++)
				{
					var newVal = (byte)Math.Min(255, a.Data[i] + b.Data[i]);
					newFrame.Data[i] = newVal;
				}

				return newFrame;
			}

			DrTilFrame MaskToRgbaTile(DrTilFrame frame)
			{
				var newFrame = new DrTilFrame(SpriteFrameType.Rgba32);
				for (var i = 0; i < 24 * 24; i++)
				{
					var newIndex = i * 4;
					var newVal = (byte)Math.Min(255, frame.Data[i] * 4);
					newFrame.Data[newIndex] = newVal;
					newFrame.Data[newIndex + 1] = newVal;
					newFrame.Data[newIndex + 2] = newVal;
					newFrame.Data[newIndex + 3] = 255;
				}

				return newFrame;
			}
		}

		public bool TryParseSprite(Stream s, string filename, out ISpriteFrame[] frames, out TypeDictionary metadata)
		{
			metadata = null;
			if (!IsDrTil(s))
			{
				frames = null;
				return false;
			}

			var paletteFile = filename.Replace(".TIL", ".PAL");
			var terrainPaletteMultiplier = 4;
			if (paletteFile.Contains("BARREN"))
			{
				terrainPaletteMultiplier = 6;
			}

			ImmutablePalette palette = null;
			using (var paletteStream = Game.ModData.DefaultFileSystem.Open(paletteFile))
			{
				palette = PaletteFromDrFile.PaletteFromStream(paletteStream, new PaletteFromDrFileInfo(
					"terrain", paletteFile, Path.GetFileNameWithoutExtension(paletteFile).ToUpper(), terrainPaletteMultiplier));
			}

			frames = ParseFrames(s, palette);

			DrTilFrame.Counter = 0;
			return true;
		}
	}
}
