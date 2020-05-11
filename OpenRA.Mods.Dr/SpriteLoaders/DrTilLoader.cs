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

using System.Collections.Generic;
using System.IO;
using OpenRA.Graphics;
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
			public float2 Offset { get; private set; }
			public byte[] Data { get; set; }
			public bool DisableExportPadding { get { return false; } }

			public DrTilFrame(Stream s)
			{
				Type = SpriteFrameType.Indexed;
				const int tileSize = 24;

				Data = new byte[tileSize * tileSize];

				s.ReadUInt8();
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

		private static DrTilFrame[] ParseFrames(Stream s)
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

			s.Position += chunkSize * 65;
			s.Position -= 64;

			for (var artIndex = 0; artIndex < 4; artIndex++)
			{
				for (var shoreType = 0; shoreType < 14; shoreType++)
				{
					var frame = new DrTilFrame(s);
					frames.Add(frame);
				}

				s.Position += 2 * chunkSize; // Skip mask frame
			}

			s.Position = start;
			return frames.ToArray();
		}

		public bool TryParseSprite(Stream s, out ISpriteFrame[] frames, out TypeDictionary metadata)
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
