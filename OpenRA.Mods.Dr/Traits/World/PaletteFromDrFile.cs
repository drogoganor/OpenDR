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
using OpenRA.FileSystem;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Dr.Traits
{
	[Desc("Load a Dark Reign .PAL palette file. Index 0 is hardcoded to be fully transparent/invisible.")]
	class PaletteFromDrFileInfo : TraitInfo, IProvidesCursorPaletteInfo
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

		[Desc("Map listed index to shadow. Ignores previous color.")]
		public readonly int ShadowIndex = 47;

		public readonly bool AllowModifiers = true;

		[Desc("Whether this palette is available for cursors.")]
		public readonly bool CursorPalette = false;

		[Desc("Multiply palette values by this ( < 160).")]
		public readonly int StandardPaletteMultiplier = 4;

		[Desc("Multiply terrain palette values by this ( >= 160).")]
		public readonly int TerrainPaletteMultiplier = 4;

		[Desc("Increase all RGB values by this amount.")]
		public readonly int Gamma = 0;

		public override object Create(ActorInitializer init) { return new PaletteFromDrFile(init.World, this); }

		string IProvidesCursorPaletteInfo.Palette { get { return CursorPalette ? Name : null; } }

		ImmutablePalette IProvidesCursorPaletteInfo.ReadPalette(IReadOnlyFileSystem fileSystem)
		{
			using (var s = fileSystem.Open(Filename))
			{
				return PaletteFromDrFile.PaletteFromStream(s, this);
			}
		}
	}

	class PaletteFromDrFile : ILoadsPalettes, IProvidesAssetBrowserPalettes
	{
		readonly World world;
		readonly PaletteFromDrFileInfo info;
		public PaletteFromDrFile(World world, PaletteFromDrFileInfo info)
		{
			this.world = world;
			this.info = info;
		}

		public static ImmutablePalette PaletteFromStream(Stream s, PaletteFromDrFileInfo info)
		{
			var colors = new uint[Palette.Size];
			var headerName = s.ReadASCII(4);
			var headerVersion = s.ReadInt32();

			if (headerName != "PALS")
			{
				throw new InvalidDataException("Palette header was not PALS");
			}

			if (headerVersion != 0x0102)
			{
				throw new InvalidDataException("Palette version `{0}` was incorrect (expected `0x0102`)".F(headerVersion));
			}

			// Data is made up of 3x256 bytes, each ranging 0-63. Data is grouped by channel.
			var list = new List<byte>();
			for (int i = 0; i < Palette.Size * 6; i++)
			{
				list.Add(s.ReadUInt8());
			}

			var rList = list.Take(256).ToList();
			var gList = list.Skip(256).Take(256).ToList();
			var bList = list.Skip(512).Take(256).ToList();

			for (int i = 0; i < Palette.Size; i++)
			{
				// Index 0 should always be completely transparent/background color
				if (i == 0)
					colors[i] = 0;
				else if (i < 160 || i == 255)
					colors[i] = (uint)Color.FromArgb(
						Math.Min((rList[i] * info.StandardPaletteMultiplier) + info.Gamma, 255),
						Math.Min((gList[i] * info.StandardPaletteMultiplier) + info.Gamma, 255),
						Math.Min((bList[i] * info.StandardPaletteMultiplier) + info.Gamma, 255)).ToArgb();
				else
					colors[i] = (uint)Color.FromArgb(
						Math.Min((rList[i] * info.TerrainPaletteMultiplier) + info.Gamma, 255),
						Math.Min((gList[i] * info.TerrainPaletteMultiplier) + info.Gamma, 255),
						Math.Min((bList[i] * info.TerrainPaletteMultiplier) + info.Gamma, 255)).ToArgb();
			}

			// Shadow hack
			colors[info.ShadowIndex] = (uint)Color.FromArgb(112, 0, 0, 0).ToArgb();

			return new ImmutablePalette(colors);
		}

		public void LoadPalettes(WorldRenderer wr)
		{
			Stream s;
			if (!world.Map.TryOpen(info.Filename, out s))
			{
				// throw new FileNotFoundException("Couldn't find palette file: " + info.Filename);
				return;
			}

			var newPal = PaletteFromStream(s, info);

			if (info.Tileset == null || info.Tileset.ToLowerInvariant() == world.Map.Tileset.ToLowerInvariant())
				wr.AddPalette(info.Name, newPal, info.AllowModifiers);

			s.Close();
			s.Dispose();
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
