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
	public class DrSprLoader : ISpriteLoader
	{
		const int HeaderSize = 32;

		SprHeader header;

		class SprHeader
		{
			public string Magic1;
			public int Version;
			public int Nanims;
			public int Nrots;
			public int Szx;
			public int Szy;
			public int Npics;
			public int Nsects;
			public int OffSections;
			public int OffAnims;
			public int OffPicoffs;
			public int OffBits;
			public bool IsShadow = false;
		}

		class SprFrameInfo
		{
			public int S;
			public int A;
			public int R;
			public int BmpSzx;
			public int BmpSzy;
			public int Lastanim;
		}

		class DrSprFrame : ISpriteFrame
		{
			public Size Size { get; private set; }
			public Size FrameSize { get; private set; }
			public float2 Offset { get; private set; }
			public byte[] Data { get; set; }
			public bool DisableExportPadding { get { return false; } }
			public int2 Hotspot { get; set; }

			public int Growto4(int x)
			{
				return x + 3 & ~3;
			}

			public DrSprFrame(Stream s, SprHeader sph, SprFrameInfo info)
			{
				int picindex = info.A * sph.Nrots + info.R;
				var read_int = new Func<int, int>((off) =>
				{
					s.Position = off;
					int vr = s.ReadInt32();
					return vr;
				});

				var picnr = read_int(HeaderSize + picindex * 4);
				if (picnr >= sph.Npics)
					throw new Exception("Pic number was greater or equal to number of pics.");

				var picoff = read_int(sph.OffPicoffs + 8 * picnr);
				var nextpicoff = read_int(sph.OffPicoffs + 8 * (picnr + 1));
				var start = s.Position;
				s.Position = sph.OffBits + picoff;
				var tempData = s.ReadBytes(nextpicoff - picoff);
				Data = new byte[(sph.Szx + 2) * sph.Szy];

				var pixindex = new Func<int, int, int>((x, y) =>
				{
					int vr = (y * sph.Szx) + x;
					return vr;
				});

				int curr = 0;
				for (int l = 0; l < sph.Szy; ++l)
				{
					int step = 0, currx = 0, cnt, i;
					while (currx < sph.Szx)
					{
						cnt = tempData[curr++];
						if ((step & 1) != 0)
							cnt &= 0x7f;
						if ((step & 1) != 0)
						{
							if (!sph.IsShadow)
							{
								for (i = 0; i < cnt; ++i, ++curr)
								{
									int newIndex = pixindex(currx + i, l);
									Data[newIndex] = tempData[curr];
								}
							}
							else
							{
								for (i = 0; i < cnt; ++i)
								{
									int newIndex = pixindex(currx + i, l);
									Data[newIndex] = 47;
								}
							}
						}

						currx += cnt;
						++step;
					}

					if (currx != sph.Szx)
						throw new Exception("Current x was not equal to the line size.");
				}

				var dataWidth = sph.Szx;
				var dataHeight = sph.Szy;
				if (dataWidth % 2 == 1)
					dataWidth += 1;

				if (dataHeight % 2 == 1)
					dataHeight += 1;

				Offset = new float2(0, 0);
				FrameSize = new Size(sph.Szx, sph.Szy);
				Size = FrameSize;

				s.Position = start;
			}
		}

		bool IsDrSpr(Stream s)
		{
			var start = s.Position;
			var h = new SprHeader()
			{
				Magic1 = s.ReadASCII(4),
				Version = s.ReadInt32(),
				Nanims = s.ReadInt32(),
				Nrots = s.ReadInt32(),
				Szx = s.ReadInt32(),
				Szy = s.ReadInt32(),
				Npics = s.ReadInt32(),
				Nsects = s.ReadInt32()
			};

			if (h.Magic1 != "RSPR" && h.Magic1 != "SSPR")
			{
				s.Position = start;
				return false;
			}

			if (h.Magic1 == "SSPR")
			{
				h.IsShadow = true;
			}

			if (h.Version != 0x0210)
			{
				s.Position = start;
				return false;
			}

			h.OffSections = HeaderSize + 4 * h.Nanims * h.Nrots;
			h.OffAnims = h.OffSections + 16 * h.Nsects;
			h.OffPicoffs = h.OffAnims + 4 * h.Nanims;
			h.OffBits = h.OffPicoffs + 8 * h.Npics + 4;
			header = h;

			return true;
		}

		DrSprFrame[] ParseFrames(Stream s, out TypeDictionary metadata)
		{
			var start = s.Position;

			metadata = new TypeDictionary();
			var frames = new List<DrSprFrame>();
			for (int sect = 0; sect < header.Nsects; ++sect)
			{
				s.Position = header.OffSections + 16 * sect;
				int firstanim = s.ReadInt32();
				int lastanim = s.ReadInt32();
				s.ReadInt32(); // Framerate
				int numhotspots = s.ReadInt32();

				int bmp_szx = header.Szx * header.Nrots;
				int bmp_szy = header.Szy * (lastanim - firstanim + 1);

				int rotOffset = 0;
				if (header.Nrots >= 4)
					rotOffset = header.Nrots / 4;

				for (int r = 0; r < header.Nrots; ++r)
				{
					int newR = r + rotOffset;
					if (newR >= header.Nrots)
						newR -= header.Nrots;

					for (int a = firstanim; a <= lastanim; ++a)
					{
						var sfi = new SprFrameInfo()
						{
							A = a,
							R = newR,
							S = sect,
							BmpSzx = bmp_szx,
							BmpSzy = bmp_szy,
							Lastanim = lastanim
						};
						var frame = new DrSprFrame(s, header, sfi);
						frames.Add(frame);
					}
				}

				if (numhotspots > 0)
				{
					int off_hotspots, h;
					s.Seek(header.OffPicoffs + 8 * header.Npics, SeekOrigin.Begin);
					off_hotspots = header.OffBits;
					for (h = 0; h < numhotspots; ++h)
					{
						int frameindex = 0;
						for (int r = 0; r < header.Nrots; ++r)
						{
							for (int a = firstanim; a <= lastanim; ++a)
							{
								int picindex = a * header.Nrots + r;
								var read_int = new Func<int, int>((off) =>
								{
									s.Position = off;
									return s.ReadInt32();
								});

								int headersize = 32;
								int picnr = read_int(headersize + picindex * 4);
								int hotoff = read_int(header.OffPicoffs + 8 * picnr + 4);
								s.Position = off_hotspots + 4 + 3 * (hotoff + h);
								byte hx = s.ReadUInt8();
								byte hy = s.ReadUInt8();

								metadata.Add(new DrFrameMetadata()
								{
									Hotspot = new int2(hx, hy),
									FrameIndex = frameindex
								});

								frameindex++;
							}
						}
					}
				}
			}

			s.Position = start;
			return frames.ToArray();
		}

		public bool TryParseSprite(Stream s, out ISpriteFrame[] frames, out TypeDictionary metadata)
		{
			metadata = null;
			if (!IsDrSpr(s))
			{
				frames = null;
				return false;
			}

			frames = ParseFrames(s, out metadata);
			return true;
		}
	}
}
