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

using System.Collections.Generic;
using System.IO;
using OpenRA.Graphics;
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
			public SpriteFrameType Type { get; private set; }
			public Size Size { get; private set; }
			public Size FrameSize { get; private set; }
			public float2 Offset { get; set; }
			public byte[] Data { get; set; }
			public bool DisableExportPadding { get { return false; } }

			public DrTilFrame(Stream s, bool isMask = false)
			{
				const int TileSize = 24;
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

				// var rgbByteArray = new List<byte>();
				// for (var i = 0; i < TileSize * TileSize; i++)
				// {
				// 	var byteVal = (byte)(Data[i] * 4);
				// 	rgbByteArray.AddRange(new[] { byteVal, byteVal, byteVal, (byte)255 });
				// }

				// var png = new Png(rgbByteArray.ToArray(), SpriteFrameType.Rgba32, TileSize, TileSize);
				// png.Save($"C:\\temp\\{counter:D4}-til.png");
				// counter++;
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

		static DrTilFrame[] ParseFrames(Stream s)
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

			const int ChunkSize = 577;

			// Skip water animation
			s.Position += ChunkSize * 65;
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
				s.Position += 2 * ChunkSize; // Skip mask frame
			}

			var something1 = s.ReadUInt8();
			var something2 = s.ReadUInt8();
			var something3 = s.ReadUInt8();
			for (var maskType = 0; maskType < 259; maskType++)
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
			return frames.ToArray();
		}

		public bool TryParseSprite(Stream s, string filename, out ISpriteFrame[] frames, out TypeDictionary metadata)
		{
			metadata = null;
			if (!IsDrTil(s))
			{
				frames = null;
				return false;
			}

			frames = ParseFrames(s);
			return true;
		}
	}
}
