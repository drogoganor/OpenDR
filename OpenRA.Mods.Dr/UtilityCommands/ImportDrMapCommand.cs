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
using OpenRA.Mods.Dr.FileFormats;

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

		private static string[] knownUnknownThings = new string[]
		{
			"wreck1", // aowrk000
			"wreck2",
			"wreck3",
			"water1", // aowtr00
			"water2",
			"water3",
			"brdh", // Civilian bridge overlay
			"brdv", // Same deal
		};

		private static string[] knownUnknownBuildings = new string[]
		{
			"cp", // Civilian factory
			"cc", // Civilian Commercial
			"civsub", // Civilian SubTransit
			"chf", // Civilian hydro farm
			"cgf", // Civilian Grain Farm
			"cf1", // Civilian Farmhouse
			"civshl", // Civilian Public Shelter
			"cr", // Civilian Rural
			"ce", // Civilian Entertainment Facility
			"civtcn", // Civilian Transit Centre
			"CivilianBridge", // nobrd1l0
			"CivilianVerticalBridge",
			"impmn", // Taelon resource
			"impww", // Water resource
			"SmallWall1",
			"SmallWall2",
			"LargeWall1",
			"LargeWall2",
			"impwr", // Imperium water research
			"SmallHorizontalBridge",
			"SmallVerticalBridge",
			"fh1_decoy",
			"ih1_decoy",
			"fu1_decoy",
			"iu1_decoy",
			"fc1_decoy",
			"ic1_decoy",
			"fgre_decoy",
			"impre_decoy",
			"fgho_decoy",
			"impho_decoy",
			"fgar_decoy",
			"impar_decoy",
		};

		private static string[] knownUnknownUnits = new string[]
		{
			"Prisoner",
			"JebRadec",
			"RowdyMaleCivilian",
			"FGundergtunnel",
			"Karoch", // a medic
			"IMPSuicideZombie", // Hostage taker output
			"ColonelMartel",
		};

		static Dictionary<string, string> thingNames = new Dictionary<string, string>
		{
			{ "tree1", "aotre000.spr" },
			{ "tree2", "aotre001.spr" },
			{ "tree3", "aotre002.spr" },
			{ "tree4", "aotre003.spr" },
			{ "tree5", "aotre004.spr" },
			{ "tree6", "aotre005.spr" },
			{ "rock1", "aoroc000.spr" },
			{ "rock2", "aoroc001.spr" },
			{ "rock3", "aoroc002.spr" },
			{ "rock4", "aoroc003.spr" },
			{ "rock5", "aoroc004.spr" },
			{ "rock6", "aoroc005.spr" },
			{ "plnt1", "aopln000.spr" },
			{ "plnt2", "aopln001.spr" },
			{ "plnt3", "aopln002.spr" },
			{ "clif1", "aoclf000.spr" },
			{ "clif2", "aoclf001.spr" },
			{ "clif3", "aoclf002.spr" },
			{ "clif4", "aoclf003.spr" },
			{ "clif5", "aoclf004.spr" },
			{ "clif6", "aoclf005.spr" },
			{ "special1", "aospc000.spr" },
			{ "rubble1", "aorub000.spr" },
			{ "rubble2", "aorub001.spr" },
			{ "rubble3", "aorub002.spr" },
			{ "misc1", "aomsc000.spr" },
			{ "misc2", "aomsc001.spr" },
			{ "misc3", "aomsc002.spr" },
			{ "smcrater", "aoctr000.spr" }, // Not sure if these are ever deliberately placed on a map
			{ "medcrater", "aoctr001.spr" },
			{ "bigcrater", "aoctr002.spr" },
			{ "largercrater", "aoctr003.spr" },
			{ "hugecrater1", "aoctr004.spr" },
			{ "hugecrater2", "aoctr005.spr" },
			{ "hugecrater3", "aoctr006.spr" },
			{ "hugecrater4", "aoctr007.spr" },
			{ "hugecrater5", "aoctr008.spr" },
			{ "hugecrater6", "aoctr009.spr" },
		};

		static Dictionary<string, string> unitNames = new Dictionary<string, string>
		{
			{ "FGConstructionCrew", "ConstructionRig" },
			{ "FGGroundTransporter", "Freighter" },
			{ "FGHoverTransporter", "HoverFreighter" },
			{ "FGFreedomFighter", "Raider" },
			{ "FGMercenary", "Mercenary" },
			{ "FGSniper", "Sniper" },
			{ "FGScout", "Scout" },
			{ "FGMedic", "Medic" },
			{ "FGSaboteur", "Saboteur" },
			{ "FGMechanic", "Mechanic" },
			{ "FGSuicideNuker", "Martyr" },
			{ "FGSpy", "Infiltrator" },
			{ "FGSpiderBike", "SpiderBike" },
			{ "FGIFV", "RAT" },
			{ "FGMediumTank", "SkirmishTank" },
			{ "FGTankHunterTank", "TankHunter" },
			{ "FGAmbushTank", "PhaseTank" },
			{ "FGConstructionMAD", "FlakJack" },
			{ "FGTripleRailHoverTank", "TripleRailHoverTank" },
			{ "FGSPA", "HellstormArtillery" },
			{ "FGSkyBike", "SkyBike" },
			{ "FGDualSkyBike", "Outrider" },
			{ "FGShockWave", "ShockWave" },
			{ "FGContaminator", "WaterContaminator" },

			// { "FGundergtunnel", "PhaseRunner" },
			{ "IMPConstructionCrew", "ConstructionRig" },
			{ "ImpGroundTransporter", "Freighter" },
			{ "ImpHoverTransporter", "HoverFreighter" },
			{ "IMPStrikeMarine", "Guardian" },
			{ "IMPFireSupportMarine", "Bion" },
			{ "IMPHoverMarine", "Exterminator" },
			{ "IMPSpy", "Infiltrator" },

			// { "IMPSuicideZombie", "SuicideZombie" },
			{ "IMPScoutTank", "ScoutRunner" },
			{ "IMPAssaultVehicle", "ITT" },
			{ "IMPPlasmaTank", "PlasmaTank" },
			{ "IMPAmper", "Amper" },
			{ "IMPMAD", "MAD" },
			{ "IMPReconSaucer", "ReconDrone" },
			{ "IMPShredder", "Shredder" },
			{ "IMPHostageTaker", "HostageTaker" },
			{ "IMPTachyonTank", "TachionTank" }, // Note spelling difference
			{ "IMPShieldedSPA", "SCARAB" },
			{ "IMPSPA", "SCARAB" },
			{ "ImpVTOL", "Cyclone" },
			{ "IMPSkyFortress", "SkyFortress" },
			{ "IMPContaminator", "WaterContaminator" },
			{ "CIVHoverTransporter", "DessicatorTransport" },
			{ "CivWheelTransporter", "CivWheelTransporter" },
		};

		private static Dictionary<string, string> buildingNames = new Dictionary<string, string>
		{
			{ "fgpp", "Power" },
			{ "imppp", "Power" },
			{ "fglp", "WaterLaunchPad" },
			{ "implp", "WaterLaunchPad" },
			{ "fh1", "HQ.human" },
			{ "fh2", "HQ.human" },
			{ "fh3", "HQ.human" },
			{ "ih1", "HQ.cyborg" },
			{ "ih2", "HQ.cyborg" },
			{ "ih3", "HQ.cyborg" },
			{ "fu1", "TrainingFacility.fguard" },
			{ "fu2", "TrainingFacility.fguard" },
			{ "iu1", "TrainingFacility.cyborg" },
			{ "iu2", "TrainingFacility.cyborg" },
			{ "fc1", "AssemblyPlant.human" },
			{ "fc2", "AssemblyPlant.human" },
			{ "ic1", "AssemblyPlant.cyborg" },
			{ "ic2", "AssemblyPlant.cyborg" },
			{ "impca", "CameraTower" },
			{ "fgca", "CameraTower" },
			{ "fg", "LaserTurret" },
			{ "ig", "PlasmaTurret" },
			{ "fs", "AntiAirTurret.human" },
			{ "is", "AntiAirTurret.cyborg" },
			{ "fa", "HeavyRailTurret" },
			{ "ia", "NeutronAccelerator" },
			{ "fgho", "Hospital.human" },
			{ "impho", "Hospital.cyborg" },
			{ "fgre", "Repair.human" },
			{ "impre", "Repair.cyborg" },
			{ "fgar", "RearmingDeck.human" },
			{ "impar", "RearmingDeck.cyborg" },
			{ "ft1", "PhasingFacility" },
			{ "ft2", "PhasingFacility" }, // Should be upgraded
			{ "it", "TemporalGate" },
			{ "itrc", "TemporalRiftCreator" },
			{ "fgth", "FreedomGuardTreatyHall" },
			{ "impdr", "ImperiumDesicator" },
			{ "rp", "RendezvousPoint" },
			{ "impmr", "ImperiumMedicalResearch" },
			{ "imphr", "ImperiumHoverResearch" },
		};

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
				stream.ReadInt32(); // Tileset num???
				var tilesetName = "BARREN";

				filename = filename.ToLowerInvariant();

				var scnFilename = filename.Replace(".map", ".scn");
				using (var scn = File.OpenRead(scnFilename))
				{
					var scnFile = new ScnFile(scn);
					foreach (var scnSection in scnFile.Entries)
					{
						if (scnSection.Name != "SetDefaultTerrain")
							continue;

						tilesetName = scnSection.ValuesStr.ToUpperInvariant();
					}
				}

				Map = new Map(ModData, ModData.DefaultTileSets[tilesetName], width + 2, height + 2)
				{
					Title = Path.GetFileNameWithoutExtension(filename),
					Author = "Dark Reign",
				};

				Map.RequiresMod = ModData.Manifest.Id;

				SetBounds(Map, width + 2, height + 2);

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

						var subindex = (byte)(byte1 / 64);
						byte variation = (byte)(subindex * (byte2 + 1));
						int tileType = byte1 % 64;

						if (tileType >= 16)
						{
							unknownTileTypeHash.Add(tileType);
							tileType = 1; // TODO: Handle edge sprites
						}

						Map.Tiles[new CPos(x + 1, y + 1)] = new TerrainTile((ushort)tileType, variation); // types[i, j], byte1
					}
				}

				// What's after the tiles? Water/Taelon?
				stream.ReadInt32(); // Always one
				stream.ReadInt32(); // Always 256
				int length = stream.ReadInt32(); // Byte length of remaining data

				byte1Hash = new HashSet<byte>();
				var byteList = new List<byte>();
				for (int i = 0; i < length; i++)
				{
					var byte1 = stream.ReadUInt8();
					if (!byte1Hash.Contains(byte1))
						byte1Hash.Add(byte1);

					byteList.Add(byte1);
				}

				using (var scn = File.OpenRead(scnFilename))
				{
					var scnFile = new ScnFile(scn);

					MapPlayers = new MapPlayers(Map.Rules, 0);
					SetMapPlayers(scnFile, Players, MapPlayers);

					// Place start locations
					int i = 0;
					foreach (var scnSection in scnFile.Entries)
					{
						if (scnSection.Name != "SetStartLocation")
							continue;

						int divisor = 24;
						int x = Convert.ToInt32(scnSection.Values[0]) / divisor;
						int y = Convert.ToInt32(scnSection.Values[1]) / divisor;
						if (x != 0 && y != 0)
						{
							var ar = new ActorReference("mpspawn")
							{
								new LocationInit(new CPos(x + 1, y + 1)),
								new OwnerInit("Neutral")
							};

							Map.ActorDefinitions.Add(new MiniYamlNode("Actor" + i++, ar.Save()));
						}
					}

					// Parse map thingies
					foreach (var scnSection in scnFile.Entries)
					{
						if (scnSection.Name != "AddThingAt")
							continue;

						string type = scnSection.Values[1];
						int x = Convert.ToInt32(scnSection.Values[2]) - 1; // Manual adjustment while our offsets are stuffed
						int y = Convert.ToInt32(scnSection.Values[3]) - 1; // Manual adjustment while our offsets are stuffed

						var matchingActor = string.Empty;

						if (thingNames.ContainsKey(type))
							matchingActor = thingNames[type];
						else if (!knownUnknownThings.Contains(type))
							throw new Exception("Unknown thing name: " + type);

						if (x != 0 && y != 0 && !string.IsNullOrEmpty(matchingActor))
						{
							var ar = new ActorReference(matchingActor)
							{
								new LocationInit(new CPos(x + 1, y + 1)),
								new OwnerInit("Neutral")
							};

							Map.ActorDefinitions.Add(new MiniYamlNode("Actor" + i++, ar.Save()));
						}
					}

					// Units
					int currentTeam = 0;
					foreach (var scnSection in scnFile.Entries)
					{
						if (scnSection.Name == "SetDefaultTeam")
						{
							currentTeam = Convert.ToInt32(scnSection.ValuesStr) + 2; // To skip creeps
							continue;
						}

						if (scnSection.Name != "PutUnitAt")
							continue;

						if (currentTeam > 3)
						{
							// throw new Exception("More than two teams on this map.");

							// Just add to creeps
							currentTeam = 1;
						}

						string type = scnSection.Values[1];
						int x = Convert.ToInt32(scnSection.Values[2]) - 1; // Manual adjustment while our offsets are stuffed
						int y = Convert.ToInt32(scnSection.Values[3]) - 1; // Manual adjustment while our offsets are stuffed

						var matchingActor = string.Empty;

						if (unitNames.ContainsKey(type))
							matchingActor = unitNames[type];
						else if (!knownUnknownUnits.Contains(type))
							throw new Exception("Unknown unit name: " + type);

						if (x != 0 && y != 0 && !string.IsNullOrEmpty(matchingActor))
						{
							var ar = new ActorReference(matchingActor)
							{
								new LocationInit(new CPos(x + 1, y + 1)),
								new OwnerInit(MapPlayers.Players.Keys.ToArray()[currentTeam])
							};

							Map.ActorDefinitions.Add(new MiniYamlNode("Actor" + i++, ar.Save()));
						}
					}

					// Do resources
					foreach (var scnSection in scnFile.Entries)
					{
						if (scnSection.Name != "AddBuildingAt")
							continue;

						string type = scnSection.Values[1];
						int x = Convert.ToInt32(scnSection.Values[2]);
						int y = Convert.ToInt32(scnSection.Values[3]);

						byte typeId = 0;
						switch (type)
						{
							case "impww":
								typeId = 1;
								break;
							case "impmn":
								typeId = 2;
								break;
						}

						if (typeId == 0)
							continue;

						var cell = new CPos(x + 1, y + 1);
						Map.Resources[cell] = new ResourceTile(typeId, 0);
					}

					// Do buildings
					foreach (var scnSection in scnFile.Entries)
					{
						if (scnSection.Name == "SetDefaultTeam")
						{
							currentTeam = Convert.ToInt32(scnSection.ValuesStr) + 2; // To skip creeps
							continue;
						}

						if (scnSection.Name != "AddBuildingAt")
							continue;

						if (currentTeam > 3)
						{
							// throw new Exception("More than two teams on this map.");

							// Just add to creeps
							currentTeam = 1;
						}

						string type = scnSection.Values[1];
						int x = Convert.ToInt32(scnSection.Values[2]);
						int y = Convert.ToInt32(scnSection.Values[3]);

						var matchingActor = string.Empty;

						if (buildingNames.ContainsKey(type))
							matchingActor = buildingNames[type];
						else if (!knownUnknownBuildings.Contains(type))
							throw new Exception("Unknown building name: " + type);

						if (x != 0 && y != 0 && !string.IsNullOrEmpty(matchingActor))
						{
							var ar = new ActorReference(matchingActor)
							{
								new LocationInit(new CPos(x + 1, y + 1)),
								new OwnerInit(MapPlayers.Players.Keys.ToArray()[currentTeam])
							};

							Map.ActorDefinitions.Add(new MiniYamlNode("Actor" + i++, ar.Save()));
						}
					}
				}

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

			// Overwrite default player definitions if needed
			if (!mapPlayers.Players.ContainsKey(section))
				mapPlayers.Players.Add(section, pr);
			else
				mapPlayers.Players[section] = pr;
		}

		public static void SetMapPlayers(ScnFile file, List<string> players, MapPlayers mapPlayers)
		{
			var section = "Neutral";
			var pr = new PlayerReference
			{
				Name = section,
				OwnsWorld = true,
				NonCombatant = true,
				Faction = "fguard",
				Color = namedColorMapping["white"]
			};

			bool isMulti = false; // file.Entries.Count(x => x.Name == "SetStartLocation") > 1;

			// Overwrite default player definitions if needed
			if (!mapPlayers.Players.ContainsKey(section))
				mapPlayers.Players.Add(section, pr);
			else
				mapPlayers.Players[section] = pr;

			int i = 0;
			if (isMulti)
			{
				foreach (var scnSection in file.Entries)
				{
					if (scnSection.Name != "SetStartLocation")
						continue;

					int x = Convert.ToInt32(scnSection.Values[0]);
					int y = Convert.ToInt32(scnSection.Values[1]);
					if (x != 0 && y != 0)
					{
						var multi = new PlayerReference
						{
							Name = "Multi" + i,
							Playable = true,
							Faction = "Random"
						};

						mapPlayers.Players.Add(multi.Name, multi);
						i++;
					}
				}
			}
			else
			{
				// Single player, examine sides
				foreach (var scnSection in file.Entries)
				{
					if (scnSection.Name != "SetTeamSide")
						continue;

					if (i > 1) // Only allow two sides
						break;

					int sideIndex = Convert.ToInt32(scnSection.Values[0]);
					PlayerReference newPlayer;

					if (sideIndex == 0)
					{
						newPlayer = new PlayerReference
						{
							Name = "Freedom Guard",
							Faction = "fguard",
							Color = HSLColor.FromRGB(234, 189, 25),
							Enemies = new[] { "Imperium" } // This will have a problem if we have Imp vs Imp or FG vs FG.
						};
					}
					else
					{
						newPlayer = new PlayerReference
						{
							Name = "Imperium",
							Faction = "imperium",
							Color = HSLColor.FromRGB(124, 60, 234),
							Enemies = new[] { "Freedom Guard" }
						};
					}

					if (i == 0)
					{
						// First is always playable faction
						newPlayer.Playable = true;
						newPlayer.AllowBots = false;
						newPlayer.Required = true;
						newPlayer.LockSpawn = true;
						newPlayer.LockTeam = true;
					}
					else if (i == 1)
					{
						// Check we don't have the same faction name
						if (mapPlayers.Players.ContainsKey(newPlayer.Name))
						{
							// Create a new name for this faction and alter enemies accordingly
							var player = mapPlayers.Players.Values.ToArray()[2];
							var newName = newPlayer.Name + " Foe";
							newPlayer.Color = HSLColor.FromRGB(234, 32, 59); // Give it a red color
							newPlayer.Name = newName;
							newPlayer.Enemies = new[] { player.Name };
							player.Enemies = new[] { newName };
						}
					}

					mapPlayers.Players.Add(newPlayer.Name, newPlayer);
					i++;
				}
			}
		}
	}
}
