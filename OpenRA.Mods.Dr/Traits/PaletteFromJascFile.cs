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
using System.Drawing;
using System.IO;
using OpenRA.FileSystem;
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Load JASC palette. Used for tileset JASC palettes.")]
	class PaletteFromJascFileInfo : ITraitInfo, IProvidesCursorPaletteInfo
	{
		[FieldLoader.Require]
		[PaletteDefinition]
		[Desc("internal palette name")]
		public readonly string Name = null;

		[Desc("If defined, load the palette only for this tileset.")]
		public readonly string Tileset = null;

		[FieldLoader.Require]
		[Desc("filename to load")]
		public readonly string Filename = null;

		[Desc("Map listed indices to shadow. Ignores previous color.")]
		public readonly int[] ShadowIndex = { };

		[Desc("Premultiply colors with their alpha values.")]
		public readonly bool Premultiply = false;

		public readonly bool AllowModifiers = true;

		[Desc("Whether this palette is available for cursors.")]
		public readonly bool CursorPalette = false;

		[Desc("Increase all RGB values by this amount.")]
		public readonly int Gamma = 0;

		public object Create(ActorInitializer init) { return new PaletteFromJascFile(init.World, this); }

		string IProvidesCursorPaletteInfo.Palette { get { return CursorPalette ? Name : null; } }

		ImmutablePalette IProvidesCursorPaletteInfo.ReadPalette(IReadOnlyFileSystem fileSystem)
		{
			var colors = new uint[Palette.Size];
			using (var s = fileSystem.Open(Filename))
			{
				using (var lines = s.ReadAllLines().GetEnumerator())
				{
					if (lines == null)
						return null;

					if (!lines.MoveNext() || (lines.Current != "GIMP Palette" && lines.Current != "JASC-PAL"))
						throw new InvalidDataException("File `{0}` is not a valid GIMP or JASC palette.".F(Filename));

					byte r, g, b, a;
					a = 255;
					var i = 0;

					while (lines.MoveNext() && i < Palette.Size)
					{
						// Skip until first color. Ignore # comments, Name/Columns and blank lines as well as JASC header values.
						if (string.IsNullOrEmpty(lines.Current) || !char.IsDigit(lines.Current.Trim()[0]) || lines.Current == "0100" || lines.Current == "256")
							continue;

						var rgba = lines.Current.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
						if (rgba.Length < 3)
							throw new InvalidDataException("Invalid RGB(A) triplet/quartet: ({0})".F(string.Join(" ", rgba)));

						if (!byte.TryParse(rgba[0], out r))
							throw new InvalidDataException("Invalid R value: {0}".F(rgba[0]));

						if (!byte.TryParse(rgba[1], out g))
							throw new InvalidDataException("Invalid G value: {0}".F(rgba[1]));

						if (!byte.TryParse(rgba[2], out b))
							throw new InvalidDataException("Invalid B value: {0}".F(rgba[2]));

						r = (byte)Math.Min(r + Gamma, 255);
						g = (byte)Math.Min(g + Gamma, 255);
						b = (byte)Math.Min(b + Gamma, 255);

						// Check if color has a (valid) alpha value.
						// Note: We can't throw on "rgba.Length > 3 but parse failed", because in GIMP palettes the 'invalid' value is probably a color name string.
						var noAlpha = rgba.Length > 3 ? !byte.TryParse(rgba[3], out a) : true;

						// Index 0 should always be completely transparent/background color
						if (i == 0)
							colors[i] = 0;
						else if (noAlpha)
							colors[i] = (uint)Color.FromArgb(r, g, b).ToArgb();
						else if (Premultiply)
							colors[i] = (uint)Color.FromArgb(a, r * a / 255, g * a / 255, b * a / 255).ToArgb();
						else
							colors[i] = (uint)Color.FromArgb(a, r, g, b).ToArgb();

						i++;
					}
				}
			}

			return new ImmutablePalette(colors);
		}
	}

	class PaletteFromJascFile : ILoadsPalettes, IProvidesAssetBrowserPalettes
	{
		readonly World world;
		readonly PaletteFromJascFileInfo info;
		public PaletteFromJascFile(World world, PaletteFromJascFileInfo info)
		{
			this.world = world;
			this.info = info;
		}

		public void LoadPalettes(WorldRenderer wr)
		{
			if (info.Tileset == null || info.Tileset.ToLowerInvariant() == world.Map.Tileset.ToLowerInvariant())
				wr.AddPalette(info.Name, ((IProvidesCursorPaletteInfo)info).ReadPalette(world.Map), info.AllowModifiers);
		}

		public IEnumerable<string> PaletteNames
		{
			get
			{
				// Only expose the palette if it is available for the shellmap's tileset (which is a requirement for its use).
				if (info.Tileset == null || info.Tileset == world.Map.Rules.TileSet.Id)
					yield return info.Name;
			}
		}
	}
}
