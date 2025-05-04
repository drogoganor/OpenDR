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
using System.Globalization;
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
		readonly bool exportPng = false;

		public class TilHeader
		{
			public string Magic1;
			public int Version;
		}

		public class DrTilFrame : ISpriteFrame
		{
			const int TileSize = 24;

			public SpriteFrameType Type { get; }
			public Size Size { get; }
			public Size FrameSize { get; }
			public float2 Offset { get; set; }
			public byte[] Data { get; set; }
			public bool DisableExportPadding { get { return false; } }

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

			public DrTilFrame(Stream s, bool isMask = false)
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
					var frame = new DrTilFrame(s);
					frames.Add(frame);
				}
			}

			for (var waterFrame = 0; waterFrame < 64; waterFrame++)
			{
				var frame = new DrTilFrame(s, isMask: true);
				frames.Add(frame);
			}

			const int ChunkSize = 577;

			for (var artIndex = 0; artIndex < 4; artIndex++)
			{
				// Skip empty tile
				s.Position += ChunkSize;

				for (var shoreType = 0; shoreType < 14; shoreType++)
				{
					var frame = new DrTilFrame(s);
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
					s.ReadUInt8(); // ???
					s.ReadUInt8(); // ???
					s.ReadUInt8(); // ???
					s.ReadUInt8(); // ???
				}

				var frame = new DrTilFrame(s, isMask: true);
				maskFrames.Add(frame);
			}

			// Editor mask select tiles (discard)
			for (var maskTemplateType = 0; maskTemplateType < 4; maskTemplateType++)
			{
				var _ = new DrTilFrame(s, isMask: true);
			}

			var cornerIndices = new List<List<int>>
			{
				// Rough
				new() { 0, 1, 2, 3 },

				// Crumbled
				new() { 64, 65, 66, 67 },

				// Square
				new() { 128, 129, 130, 131 },

				// Blob
				new() { 192, 193, 194, 195 },
			};

			foreach (var cornerSet in cornerIndices)
			{
				var se = maskFrames[cornerSet[0]];
				var sw = maskFrames[cornerSet[1]];
				var nw = maskFrames[cornerSet[2]];
				var ne = maskFrames[cornerSet[3]];

				// Create edge tiles by adding
				var north = CombineMaskTilesAdditive(nw, ne);
				var east = CombineMaskTilesAdditive(se, ne);
				var south = CombineMaskTilesAdditive(sw, se);
				var west = CombineMaskTilesAdditive(sw, nw);

				var neInner = CombineMaskTilesAdditive(north, east);
				var nwInner = CombineMaskTilesAdditive(north, west);
				var swInner = CombineMaskTilesAdditive(south, west);
				var seInner = CombineMaskTilesAdditive(south, east);

				var neSwBridge = CombineMaskTilesSubtractive(neInner, swInner);
				var nwSeBridge = CombineMaskTilesSubtractive(nwInner, seInner);

				for (var tileIndex = 2; tileIndex < 16; tileIndex++)
				{
					var mappedTileIndex = tileIndex * 8;
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
					frames.Add(MaskTile(sourceTile, neSwBridge));
					frames.Add(MaskTile(sourceTile, nwSeBridge));
				}
			}

			// Mask sea tiles

			// Inner: 4, 9, 18, 35 (SW, NW, NE, SE)
			// Edge: 12, 25, 36, 50 (N, E, S, W)
			// Outer: 28, 57, 44, 52 (NE, SE, NW, SW)
			// Bridge: 41, 20 (NWSE, NESW)
			var seaTiles = new List<int> { 4, 9, 18, 35, 12, 25, 50, 36, 44, 28, 57, 52, 20, 41 };
			var seaTileMasks = new List<List<int>>
			{
				new() { 192, 206, 220, 234 }, // 4
				new() { 193, 207, 221, 235 }, // 9
				new() { 195, 209, 223, 237 }, // 18
				new() { 199, 213, 227, 241 }, // 35
				new() { 194, 208, 222, 236 }, // 12
				new() { 197, 211, 225, 239 }, // 25
				new() { 203, 217, 231, 245 }, // 50
				new() { 200, 214, 228, 242 }, // 36
				new() { 202, 216, 230, 244 }, // 44
				new() { 198, 212, 226, 240 }, // 28
				new() { 205, 219, 233, 247 }, // 57
				new() { 204, 218, 232, 246 }, // 52
				new() { 196, 210, 224, 238 }, // 20
				new() { 201, 215, 229, 243 }, // 41
			};

			for (var maskIndex = 0; maskIndex < seaTiles.Count; maskIndex++)
			{
				var seaTileMask = seaTiles[maskIndex];
				var thisSeaTiles = seaTileMasks[maskIndex];

				foreach (var seaTile in thisSeaTiles)
				{
					// Mask it up
					frames.Add(MaskTile(frames[seaTile], maskFrames[seaTileMask]));
				}
			}

			AddShadowFrames();

			s.Position = start;
			return frames.ToArray();

			////////////////////////////////////////////////

			void AddShadowFrames()
			{
				// Shadow tiles
				const byte ShadowAlpha = 35;
				const byte ShadowColor = 0;

				var cornerSet = cornerIndices[1];

				var se = maskFrames[cornerSet[0]];
				var sw = maskFrames[cornerSet[1]];
				var nw = maskFrames[cornerSet[2]];
				var ne = maskFrames[cornerSet[3]];

				// Create edge tiles by adding
				var north = CombineMaskTilesAdditive(nw, ne);
				var east = CombineMaskTilesAdditive(se, ne);
				var south = CombineMaskTilesAdditive(sw, se);
				var west = CombineMaskTilesAdditive(sw, nw);

				var neInner = CombineMaskTilesAdditive(north, east);
				var nwInner = CombineMaskTilesAdditive(north, west);
				var swInner = CombineMaskTilesAdditive(south, west);
				var seInner = CombineMaskTilesAdditive(south, east);

				var neSwBridge = CombineMaskTilesSubtractive(neInner, swInner);
				var nwSeBridge = CombineMaskTilesSubtractive(nwInner, seInner);

				frames.Add(ShadowTile(north, ShadowColor, ShadowAlpha));
				frames.Add(ShadowTile(east, ShadowColor, ShadowAlpha));
				frames.Add(ShadowTile(south, ShadowColor, ShadowAlpha));
				frames.Add(ShadowTile(west, ShadowColor, ShadowAlpha));

				frames.Add(ShadowTile(nw, ShadowColor, ShadowAlpha));
				frames.Add(ShadowTile(ne, ShadowColor, ShadowAlpha));
				frames.Add(ShadowTile(sw, ShadowColor, ShadowAlpha));
				frames.Add(ShadowTile(se, ShadowColor, ShadowAlpha));

				frames.Add(ShadowTile(swInner, ShadowColor, ShadowAlpha));
				frames.Add(ShadowTile(seInner, ShadowColor, ShadowAlpha));
				frames.Add(ShadowTile(nwInner, ShadowColor, ShadowAlpha));
				frames.Add(ShadowTile(neInner, ShadowColor, ShadowAlpha));

				frames.Add(ShadowTile(nwSeBridge, ShadowColor, ShadowAlpha));
				frames.Add(ShadowTile(neSwBridge, ShadowColor, ShadowAlpha));

				frames.Add(FullShadowTile(ShadowColor, ShadowAlpha));
			}

			DrTilFrame ShadowTile(DrTilFrame mask, byte color, byte alpha)
			{
				var newFrame = new DrTilFrame(SpriteFrameType.Rgba32);
				for (var i = 0; i < 24 * 24; i++)
				{
					var newIndex = i * 4;
					newFrame.Data[newIndex] = color;
					newFrame.Data[newIndex + 1] = color;
					newFrame.Data[newIndex + 2] = color;
					newFrame.Data[newIndex + 3] = Math.Min(alpha, mask.Data[i]);
				}

				return newFrame;
			}

			DrTilFrame FullShadowTile(byte color, byte alpha)
			{
				var newFrame = new DrTilFrame(SpriteFrameType.Rgba32);
				for (var i = 0; i < 24 * 24; i++)
				{
					var newIndex = i * 4;
					newFrame.Data[newIndex] = color;
					newFrame.Data[newIndex + 1] = color;
					newFrame.Data[newIndex + 2] = color;
					newFrame.Data[newIndex + 3] = Math.Min(alpha, (byte)255);
				}

				return newFrame;
			}

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

			DrTilFrame CombineMaskTilesAdditive(DrTilFrame a, DrTilFrame b)
			{
				var newFrame = new DrTilFrame(SpriteFrameType.Indexed8);
				for (var i = 0; i < 24 * 24; i++)
				{
					var newVal = (byte)Math.Min(255, a.Data[i] + b.Data[i]);
					newFrame.Data[i] = newVal;
				}

				return newFrame;
			}

			DrTilFrame CombineMaskTilesSubtractive(DrTilFrame a, DrTilFrame b, bool invert = false)
			{
				var newFrame = new DrTilFrame(SpriteFrameType.Indexed8);
				for (var i = 0; i < 24 * 24; i++)
				{
					var newVal = Math.Min(a.Data[i], b.Data[i]);

					if (invert)
						newVal = (byte)(byte.MaxValue - newVal);

					newFrame.Data[i] = newVal;
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

			var paletteFile = filename.Replace(".TIL", ".PAL").Replace(".til", ".pal");
			var terrainPaletteMultiplier = 4;
			if (paletteFile.Contains("BARREN") || paletteFile.Contains("JUNGLE"))
			{
				terrainPaletteMultiplier = 6;
			}
			else if (paletteFile.Contains("aust") || paletteFile.Contains("auralien"))
			{
				terrainPaletteMultiplier = 5;
			}
			else if (paletteFile.Contains("volcanic"))
			{
				terrainPaletteMultiplier = 4;
			}

			ImmutablePalette palette = null;
			using (var paletteStream = Game.ModData.DefaultFileSystem.Open(paletteFile))
			{
				palette = PaletteFromDrFile.PaletteFromStream(paletteStream, new PaletteFromDrFileInfo(
					"terrain", paletteFile, Path.GetFileNameWithoutExtension(paletteFile).ToUpper(CultureInfo.InvariantCulture), terrainPaletteMultiplier));
			}

			frames = ParseFrames(s, palette);

			if (exportPng)
			{
				const int TileSize = 24;
				for (var tileIndex = 0; tileIndex < frames.Length; tileIndex++)
				{
					var tile = frames[tileIndex];
					var rgbByteArray = new List<byte>();
					for (var i = 0; i < TileSize * TileSize; i++)
					{
						if (palette == null)
						{
							var byteVal = (byte)(tile.Data[i] * 4);
							rgbByteArray.AddRange(new[] { byteVal, byteVal, byteVal, (byte)255 });
						}
						else
						{
							var color = palette.GetColor(tile.Data[i]);
							rgbByteArray.AddRange(new[] { color.R, color.G, color.B, (byte)255 });
						}
					}

					var png = new Png(rgbByteArray.ToArray(), SpriteFrameType.Rgba32, TileSize, TileSize);
					png.Save($"C:\\temp\\{tileIndex:D4}-til.png");
				}
			}

			return true;
		}
	}
}
