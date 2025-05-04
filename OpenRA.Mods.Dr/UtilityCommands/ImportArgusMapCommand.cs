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
using OpenRA.FileSystem;
using OpenRA.Primitives;

namespace OpenRA.Mods.Dr.UtilityCommands
{
	class ImportArgusMapCommand : IUtilityCommand
	{
		string IUtilityCommand.Name { get { return "--import-argus-map"; } }
		bool IUtilityCommand.ValidateArguments(string[] args) { return ValidateArguments(args); }

		[Desc("FILENAME", "Convert an Argus map to the OpenRA format.")]
		void IUtilityCommand.Run(Utility utility, string[] args) { Run(utility, args); }

		public ModData ModData;
		public Map Map;
		public List<string> Players = new();
		protected bool skipActors = true;

		protected static bool ValidateArguments(string[] args)
		{
			return args.Length >= 2;
		}

		protected void Run(Utility utility, string[] args)
		{
			// HACK: The engine code assumes that Game.modData is set.
			Game.ModData = ModData = utility.ModData;

			const string RootPath = "..\\..\\ArgusMaps\\";

			var filename = args[1];
			using (var stream = File.OpenRead(RootPath + filename))
			{
				var headerString = stream.ReadASCII(4);

				if (headerString != "TYPE")
				{
					throw new ArgumentException("Map file did not start with TYPE");
				}

				var sectionLength = stream.ReadUInt32();

				stream.Seek(sectionLength, SeekOrigin.Current);
				headerString = stream.ReadASCII(4);

				if (headerString != "VER ")
				{
					throw new ArgumentException("Map section did not start with VER ");
				}

				sectionLength = stream.ReadUInt32();

				var versionShort = stream.ReadUInt16();

				var isNewVersion = versionShort == 0x13;

				headerString = stream.ReadASCII(4);

				if (headerString != "DESC")
				{
					throw new ArgumentException("Map section did not start with DESC");
				}

				sectionLength = stream.ReadUInt32();

				stream.Seek(sectionLength, SeekOrigin.Current);
				headerString = stream.ReadASCII(4);

				if (headerString != "OWNR")
				{
					throw new ArgumentException("Map section did not start with OWNR");
				}

				sectionLength = stream.ReadUInt32();

				stream.Seek(sectionLength, SeekOrigin.Current);
				headerString = stream.ReadASCII(4);

				if (headerString != "ERA ")
				{
					throw new ArgumentException("Map section did not start with ERA ");
				}

				sectionLength = stream.ReadUInt32();

				stream.Seek(sectionLength, SeekOrigin.Current);

				if (isNewVersion)
				{
					headerString = stream.ReadASCII(4);

					if (headerString != "ERAX")
					{
						throw new ArgumentException("Map section did not start with ERAX");
					}

					sectionLength = stream.ReadUInt32();

					stream.Seek(sectionLength, SeekOrigin.Current);
				}

				headerString = stream.ReadASCII(4);

				if (headerString != "DIM ")
				{
					throw new ArgumentException("Map section did not start with DIM ");
				}

				sectionLength = stream.ReadUInt32();

				var width = stream.ReadUInt16();
				var height = stream.ReadUInt16();
				const string TilesetName = "BARREN";

				headerString = stream.ReadASCII(4);

				if (headerString != "UDTA")
				{
					throw new ArgumentException("Map section did not start with UDTA");
				}

				sectionLength = stream.ReadUInt32();

				stream.Seek(sectionLength, SeekOrigin.Current);
				headerString = stream.ReadASCII(4);

				if (headerString == "ALOW")
				{
					sectionLength = stream.ReadUInt32();

					stream.Seek(sectionLength, SeekOrigin.Current);
					headerString = stream.ReadASCII(4);
				}

				if (headerString != "UGRD")
				{
					throw new ArgumentException("Map section did not start with UGRD");
				}

				sectionLength = stream.ReadUInt32();

				stream.Seek(sectionLength, SeekOrigin.Current);
				headerString = stream.ReadASCII(4);

				if (headerString != "SIDE")
				{
					throw new ArgumentException("Map section did not start with SIDE");
				}

				sectionLength = stream.ReadUInt32();

				stream.Seek(sectionLength, SeekOrigin.Current);
				headerString = stream.ReadASCII(4);

				if (headerString != "SGLD")
				{
					throw new ArgumentException("Map section did not start with SGLD");
				}

				sectionLength = stream.ReadUInt32();

				stream.Seek(sectionLength, SeekOrigin.Current);
				headerString = stream.ReadASCII(4);

				if (headerString != "SLBR")
				{
					throw new ArgumentException("Map section did not start with SLBR");
				}

				sectionLength = stream.ReadUInt32();

				stream.Seek(sectionLength, SeekOrigin.Current);
				headerString = stream.ReadASCII(4);

				if (headerString != "SOIL")
				{
					throw new ArgumentException("Map section did not start with SOIL");
				}

				sectionLength = stream.ReadUInt32();

				stream.Seek(sectionLength, SeekOrigin.Current);
				headerString = stream.ReadASCII(4);

				if (headerString != "AIPL")
				{
					throw new ArgumentException("Map section did not start with AIPL");
				}

				sectionLength = stream.ReadUInt32();

				stream.Seek(sectionLength, SeekOrigin.Current);
				headerString = stream.ReadASCII(4);

				if (headerString != "MTXM")
				{
					throw new ArgumentException("Map section did not start with MTXM");
				}

				sectionLength = stream.ReadUInt32();

				filename = filename.ToLowerInvariant();

				if (!ModData.DefaultTerrainInfo.TryGetValue(TilesetName, out var terrainInfo))
					throw new InvalidDataException($"Unknown tileset {TilesetName}");

				Map = new Map(ModData, terrainInfo, width + 2, height + 2)
				{
					Title = Path.GetFileNameWithoutExtension(filename),
					Author = "Dark Reign",
					RequiresMod = ModData.Manifest.Id
				};

				SetBounds(Map, width + 2, height + 2);

				var unknownTileTypeHash = new HashSet<int>();

				for (var y = 0; y < height; y++)
				{
					for (var x = 0; x < width; x++)
					{
						var tile = stream.ReadUInt16();

						// TODO: Map tile correctly
						Map.Tiles[new CPos(x + 1, y + 1)] = new TerrainTile(tile, 0);
					}
				}
			}

			var dest = Path.GetFileNameWithoutExtension(args[1]) + ".oramap";

			Map.Save(ZipFileLoader.Create(dest));
			Console.WriteLine(dest + " saved.");
		}

		static void SetBounds(Map map, int width, int height)
		{
			var tl = new PPos(1, 1);
			var br = new PPos(0 + width - 2, 0 + height - 2);
			map.SetBounds(tl, br);
		}

		protected static void SetNeutralPlayer(MapPlayers mapPlayers)
		{
			const string Section = "Neutral";
			var pr = new PlayerReference
			{
				Name = Section,
				OwnsWorld = true,
				NonCombatant = true,
				Faction = "fguard",
				Color = Color.FromArgb(255, 255, 255)
			};

			// Overwrite default player definitions if needed
			if (!mapPlayers.Players.TryAdd(Section, pr))
				mapPlayers.Players[Section] = pr;
		}
	}
}
