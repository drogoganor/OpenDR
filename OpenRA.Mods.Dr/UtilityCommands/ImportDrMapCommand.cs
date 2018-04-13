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
using System.Reflection.Emit;
using System.Text;
using OpenRA.FileSystem;
using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.FileFormats;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.UtilityCommands;
using OpenRA.Primitives;

namespace OpenRA.Mods.Dr.UtilityCommands
{
	class ImportDrMapCommand : IUtilityCommand
	{
		string IUtilityCommand.Name { get { return "--import-dr-map"; } }
		bool IUtilityCommand.ValidateArguments(string[] args) { return ValidateArguments(args); }

		[Desc("FILENAME", "Convert a legacy Dark Reign  map to the OpenRA format.")]
		void IUtilityCommand.Run(Utility utility, string[] args) { Run(utility, args); }
		
		public ModData ModData;
		public Map Map;
		public List<string> Players = new List<string>();
		public MapPlayers MapPlayers;

		protected bool ValidateArguments(string[] args)
		{
			return args.Length >= 2;
		}

		protected void Run(Utility utility, string[] args)
		{
			// HACK: The engine code assumes that Game.modData is set.
			Game.ModData = ModData = utility.ModData;

			var filename = args[1];
			using (var stream = File.OpenRead(filename))
			{
				var headerString = stream.ReadASCII(4);
				var headerVers = stream.ReadInt32();

				if (headerString != "MAP_")
				{
					throw new ArgumentException("Map file did not start with MAP_");
				}

				if (headerVers < 0x300)
				{
					throw new ArgumentException("Map version was too low.");
				}

				var width = stream.ReadInt32();
				var height = stream.ReadInt32();
				var tilesetNum = stream.ReadInt32();

				var tilesetName = "BARREN";
				switch (tilesetNum)
				{
					case 2:
						tilesetName = "JUNGLE";
						break;
					case 1:
						tilesetName = "BARREN";
						break;
					case 0: // confirmed
						tilesetName = "SNOW";
						break;
					case 3:
						tilesetName = "ALIEN";
						break;
					case 4:
						tilesetName = "AUST";
						break;
					case 5:
						tilesetName = "AURALIEN";
						break;
					case 6:
						tilesetName = "ASTEROID";
						break;
					case 7:
						tilesetName = "VOLCANIC";
						break;
				}

				Map = new Map(ModData, ModData.DefaultTileSets[tilesetName], width, height)
				{
					Title = Path.GetFileNameWithoutExtension(filename),
					Author = "Dark Reign",
				};

				Map.RequiresMod = ModData.Manifest.Id;

				SetBounds(Map, width, height);
				
				var byte1Hash = new HashSet<byte>();
				var byte2Hash = new HashSet<byte>();
				var byte3Hash = new HashSet<byte>();
				var byte4Hash = new HashSet<byte>();
				var byte5Hash = new HashSet<byte>();
				var byte6Hash = new HashSet<byte>();
				var unknownTileTypeHash = new HashSet<int>();

				for (int y = 0; y < height; y++)
				{
					for (int x = 0; x < width; x++)
					{
						var byte1 = stream.ReadUInt8(); // Tile type 0-63, with art variations repeated 1-4
						var byte2 = stream.ReadUInt8(); // Which art variation to use. 0 = 1-4, 1 = 5-8
						var byte3 = stream.ReadUInt8(); // Base elevation, defaults to 2.
						var byte4 = stream.ReadUInt8(); // Unknown, defaults to 36. Seems to be elevation related.
						var byte5 = stream.ReadUInt8(); // Unknown, defaults to 73. Seems to be elevation related.
						var byte6 = stream.ReadUInt8(); // Unknown, defaults to 146. Seems to be elevation related.

						if (!byte1Hash.Contains(byte1))
							byte1Hash.Add(byte1);
						if (!byte2Hash.Contains(byte2))
							byte2Hash.Add(byte2);
						if (!byte3Hash.Contains(byte3))
							byte3Hash.Add(byte3);
						if (!byte4Hash.Contains(byte4))
							byte4Hash.Add(byte4);
						if (!byte5Hash.Contains(byte5))
							byte5Hash.Add(byte5);
						if (!byte6Hash.Contains(byte6))
							byte6Hash.Add(byte6);

						var subindex = (byte)(byte1/64);
						byte variation = (byte)(subindex*(byte2+1));

						int tileType = byte1%64;

						if (tileType >= 16)
						{
							unknownTileTypeHash.Add(tileType);
							tileType = 15; // TODO: Handle edge sprites
						}
						
						Map.Tiles[new CPos(x, y)] = new TerrainTile((ushort)tileType, variation); // types[i, j], byte1
					}
				}

				MapPlayers = new MapPlayers(Map.Rules, 0);
				SetMapPlayersDefault(Players, MapPlayers);

				Map.PlayerDefinitions = MapPlayers.ToMiniYaml();
			}

			Map.FixOpenAreas();

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
		
		// TODO: fix this -- will have bitrotted pretty badly.
		static Dictionary<string, HSLColor> namedColorMapping = new Dictionary<string, HSLColor>()
		{
			{ "gold", HSLColor.FromRGB(246, 214, 121) },
			{ "blue", HSLColor.FromRGB(226, 230, 246) },
			{ "red", HSLColor.FromRGB(255, 20, 0) },
			{ "neutral", HSLColor.FromRGB(238, 238, 238) },
			{ "orange", HSLColor.FromRGB(255, 230, 149) },
			{ "teal", HSLColor.FromRGB(93, 194, 165) },
			{ "salmon", HSLColor.FromRGB(210, 153, 125) },
			{ "green", HSLColor.FromRGB(160, 240, 140) },
			{ "white", HSLColor.FromRGB(255, 255, 255) },
			{ "black", HSLColor.FromRGB(80, 80, 80) },
		};
		
		public static void SetMapPlayersDefault(List<string> players, MapPlayers mapPlayers, string section = "Neutral", string faction = "fguard", string color = "white")
		{
			var pr = new PlayerReference
			{
				Name = "Neutral",
				OwnsWorld = true,
				NonCombatant = true,
				Faction = faction,
				Color = namedColorMapping[color]
			};

			var neutral = new[] { "Neutral" };

			// Overwrite default player definitions if needed
			if (!mapPlayers.Players.ContainsKey(section))
				mapPlayers.Players.Add(section, pr);
			else
				mapPlayers.Players[section] = pr;
		}
		
	}
}
