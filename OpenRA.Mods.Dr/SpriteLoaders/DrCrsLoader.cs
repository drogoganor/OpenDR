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
using OpenRA.Graphics;
using OpenRA.Primitives;

namespace OpenRA.Mods.Dr.SpriteLoaders
{
	public class DrCrsLoader : ISpriteLoader
	{
		CrsHeader header;

		private class CrsHeader
		{
			public string Magic1;
			public int Version;
			public int Nanims;
		}

		private class CrsFrameInfo
		{
			public int FrameIndex;
		}

		private class DrCrsFrame : ISpriteFrame
		{
			public SpriteFrameType Type { get; private set; }
			public Size Size { get; private set; }
			public Size FrameSize { get; private set; }
			public float2 Offset { get; private set; }
			public byte[] Data { get; set; }
			public bool DisableExportPadding { get { return false; } }

			public DrCrsFrame(Stream s, CrsHeader sph, CrsFrameInfo info)
			{
				Type = SpriteFrameType.Indexed;
				const int width = 32;
				const int numPixels = width * width;
				Data = new byte[numPixels];

				var pixindex = new Func<int, int, int>((x, y) => y * width + x);

				for (var y = 0; y < width; ++y)
				{
					for (var x = 0; x < width; ++x)
					{
						var newIndex = pixindex(x, y);
						Data[newIndex] = s.ReadUInt8();
					}
				}

				Offset = new float2(0, 0);
				FrameSize = new Size(width, width);
				Size = FrameSize;
			}
		}

		private bool IsDrCrs(Stream s)
		{
			var start = s.Position;
			var h = new CrsHeader()
			{
				Magic1 = s.ReadASCII(4),
				Version = s.ReadInt32(),
				Nanims = s.ReadInt32()
			};

			if (h.Magic1 != "CRSR")
			{
				s.Position = start;
				return false;
			}

			if (h.Version != 0x200)
			{
				s.Position = start;
				return false;
			}

			header = h;

			return true;
		}

		private DrCrsFrame[] ParseFrames(Stream s)
		{
			var start = s.Position;

			var frames = new List<DrCrsFrame>();
			for (var i = 0; i < header.Nanims; ++i)
			{
				var sfi = new CrsFrameInfo()
				{
					FrameIndex = i
				};
				var frame = new DrCrsFrame(s, header, sfi);
				frames.Add(frame);
			}

			frames.Reverse(19, 9);
			frames.Reverse(28, 4);
			frames.Reverse(32, 8);
			frames.Reverse(40, 10);
			frames.Reverse(50, 7);
			frames.Reverse(62, 6);
			frames.Reverse(68, 8);
			frames.Reverse(76, 8);
			frames.Reverse(90, 6);
			frames.Reverse(96, 8);
			frames.Reverse(104, 8);
			frames.Reverse(112, 4);
			frames.Reverse(116, 4);
			frames.Reverse(120, 4);
			frames.Reverse(124, 4);
			frames.Reverse(128, 7);
			frames.Reverse(135, 7);
			frames.Reverse(142, 7);
			frames.Reverse(149, 7);
			frames.Reverse(156, 7);
			frames.Reverse(163, 7);
			frames.Reverse(170, 7);
			frames.Reverse(177, 7);
			frames.Reverse(185, 9);
			frames.Reverse(194, 7);
			frames.Reverse(202, 8);
			frames.Reverse(210, 5);
			frames.Reverse(215, 5);
			frames.Reverse(229, 11);
			frames.Reverse(240, 11);
			frames.Reverse(251, 8);
			frames.Reverse(263, 8);
			frames.Reverse(279, 9);

			var isDemo = frames.Count < 296;
			if (!isDemo)
			{
				var cursor1 = frames[304];
				var cursor2 = frames[321];
				var cursor3 = frames[322];
				frames.RemoveAt(304);
				frames.RemoveRange(320, 2);
				frames.InsertRange(288, new[] { cursor1, cursor2, cursor3 });

				frames.Reverse(291, 8);
			}

			s.Position = start;
			return frames.ToArray();
		}

		public bool TryParseSprite(Stream s, out ISpriteFrame[] frames, out TypeDictionary metadata)
		{
			metadata = null;
			if (!IsDrCrs(s))
			{
				frames = null;
				return false;
			}

			frames = ParseFrames(s);
			return true;
		}
	}
}
