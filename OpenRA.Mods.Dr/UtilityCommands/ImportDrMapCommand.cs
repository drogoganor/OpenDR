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

		[Desc("FILENAME", "Convert a Dark Reign map to the OpenRA format.")]
		void IUtilityCommand.Run(Utility utility, string[] args) { Run(utility, args); }

		public ModData ModData;
		public Map Map;
		public List<string> Players = new List<string>();
		public MapPlayers MapPlayers;
		private int numMultiStarts = 0;

		private static string[] knownUnknownThings = new string[]
		{
		};

		private static string[] knownUnknownBuildings = new string[]
		{
			"impmn", // Taelon resource
			"impww", // Water resource
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
		};

		static Dictionary<string, string> thingNames = new Dictionary<string, string>
		{
			{ "tree1", "aotre000" },
			{ "tree2", "aotre001" },
			{ "tree3", "aotre002" },
			{ "tree4", "aotre003" },
			{ "tree5", "aotre004" },
			{ "tree6", "aotre005" },
			{ "rock1", "aoroc000" },
			{ "rock2", "aoroc001" },
			{ "rock3", "aoroc002" },
			{ "rock4", "aoroc003" },
			{ "rock5", "aoroc004" },
			{ "rock6", "aoroc005" },
			{ "plnt1", "aopln000" },
			{ "plnt2", "aopln001" },
			{ "plnt3", "aopln002" },
			{ "clif1", "aoclf000" },
			{ "clif2", "aoclf001" },
			{ "clif3", "aoclf002" },
			{ "clif4", "aoclf003" },
			{ "clif5", "aoclf004" },
			{ "clif6", "aoclf005" },
			{ "special1", "aospc000" },
			{ "rubble1", "aorub000" },
			{ "rubble2", "aorub001" },
			{ "rubble3", "aorub002" },
			{ "misc1", "aomsc000" },
			{ "misc2", "aomsc001" },
			{ "misc3", "aomsc002" },
			{ "smcrater", "aoctr000" }, // Not sure if these are ever deliberately placed on a map
			{ "medcrater", "aoctr001" },
			{ "bigcrater", "aoctr002" },
			{ "largercrater", "aoctr003" },
			{ "hugecrater1", "aoctr004" },
			{ "hugecrater2", "aoctr005" },
			{ "hugecrater3", "aoctr006" },
			{ "hugecrater4", "aoctr007" },
			{ "hugecrater5", "aoctr008" },
			{ "hugecrater6", "aoctr009" },
			{ "water1", "aowtr000" },
			{ "water2", "aowtr001" },
			{ "water3", "aowtr002" },
			{ "brdh", "DirtBridge" },
			{ "brdv", "DirtBridge" }, // Allow it to face the other way
			{ "wreck1", "aowrk000" },
			{ "wreck2", "aowrk001" },
			{ "wreck3", "aowrk002" },
			{ "watercrater", "eowcocr0" },
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
			{ "RowdyMaleCivilian", "RowdyCivilian" },
			{ "Prisoner", "Civilian" },
			{ "FGundergtunnel", "PhaseRunner" },
			{ "ColonelMartel", "ColonelMartel" },
			{ "JebRadec", "JebRadec" },
			{ "Karoch", "Karoch" },
			{ "IMPSuicideZombie", "Martyr" }, // Hostage taker output
			{ "TVTOL", "Cyclone" }, // Togran cyclone
			{ "TSkyFortress", "SkyFortress" },
			{ "TPlasmaTank", "PlasmaTank" },
			{ "TShredder", "Shredder" },
			{ "TMechanic", "Mechanic" },
			{ "TMediumTank", "SkirmishTank" },
			{ "TMedic", "Medic" },
			{ "TFireSupportMarine", "Bion" },
			{ "TMercenary", "Mercenary" },
			{ "TTankHunterTank", "TankHunter" },
			{ "TConstructionCrew", "ConstructionRig" }
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
			{ "impwr", "ImperiumWaterResearch" },
			{ "cp", "CivilianFactory" },
			{ "cc", "CivilianCommercial" },
			{ "civsub", "CivilianSubTransit" },
			{ "chf", "CivilianHydroFarm" },
			{ "cgf", "CivilianGrainFarm" },
			{ "cf1", "CivilianFarmhouse" },
			{ "civshl", "CivilianPublicShelter" },
			{ "cr", "CivilianRural" },
			{ "ce", "CivilianEntertainmentFacility" },
			{ "civtcn", "CivilianTransitCentre" },
			{ "CivilianBridge", "CivilianBridge" },
			{ "CivilianVerticalBridge", "CivilianVerticalBridge" },
			{ "SmallWall1", "SmallWall1" },
			{ "SmallWall2", "SmallWall2" },
			{ "LargeWall1", "LargeWall1" },
			{ "LargeWall2", "LargeWall2" },
			{ "SmallHorizontalBridge", "SmallSHHorizontalBridge" },
			{ "SmallVerticalBridge", "SmallSHVerticalBridge" },
			{ "tfgpp", "Power" }, // Togran
			{ "tfg", "LaserTurret" },
			{ "tfa", "HeavyRailTurret" },
			{ "tfglp", "WaterLaunchPad" },
			{ "tfgre", "Repair.human" },
			{ "tfgca", "CameraTower" },
			{ "tfs", "AntiAirTurret.human" },
			{ "tih1", "HQ.cyborg" },
			{ "timpre", "Repair.cyborg" }
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
						int x = Convert.ToInt32(scnSection.Values[2]);
						int y = Convert.ToInt32(scnSection.Values[3]);

						var matchingActor = string.Empty;

						if (thingNames.ContainsKey(type))
							matchingActor = thingNames[type];
						else if (!knownUnknownThings.Contains(type))
							throw new Exception("Unknown thing name: " + type);

						if (x >= 0 && y >= 0 && !string.IsNullOrEmpty(matchingActor))
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
							currentTeam = Convert.ToInt32(scnSection.ValuesStr);
							continue;
						}

						if (scnSection.Name != "PutUnitAt")
							continue;

						int playerIndex = GetMatchingPlayerIndex(currentTeam); // to skip creeps and neutral if necessary

						string type = scnSection.Values[1];
						int x = Convert.ToInt32(scnSection.Values[2]);
						int y = Convert.ToInt32(scnSection.Values[3]);

						var matchingActor = string.Empty;

						if (unitNames.ContainsKey(type))
							matchingActor = unitNames[type];
						else if (!knownUnknownUnits.Contains(type))
							throw new Exception("Unknown unit name: " + type);

						if (x >= 0 && y >= 0 && !string.IsNullOrEmpty(matchingActor))
						{
							var ar = new ActorReference(matchingActor)
							{
								new LocationInit(new CPos(x + 1, y + 1)),
								new OwnerInit(MapPlayers.Players.Values.First(p => p.Team == playerIndex).Name)
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
							currentTeam = Convert.ToInt32(scnSection.ValuesStr);
							continue;
						}

						if (scnSection.Name != "AddBuildingAt")
							continue;

						int playerIndex = GetMatchingPlayerIndex(currentTeam); // to skip creeps and neutral if necessary

						string type = scnSection.Values[1];
						int x = Convert.ToInt32(scnSection.Values[2]);
						int y = Convert.ToInt32(scnSection.Values[3]);

						var matchingActor = string.Empty;

						if (buildingNames.ContainsKey(type))
							matchingActor = buildingNames[type];
						else if (!knownUnknownBuildings.Contains(type))
							throw new Exception("Unknown building name: " + type);

						if (x >= 0 && y >= 0 && !string.IsNullOrEmpty(matchingActor))
						{
							var ar = new ActorReference(matchingActor)
							{
								new LocationInit(new CPos(x + 1, y + 1)),
								new OwnerInit(MapPlayers.Players.Values.First(p => p.Team == playerIndex).Name)
							};

							Map.ActorDefinitions.Add(new MiniYamlNode("Actor" + i++, ar.Save()));
						}
					}
				}

				// Reset teams var
				foreach (var playersValue in MapPlayers.Players.Values)
				{
					playersValue.Team = 0;
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
		protected Dictionary<string, HSLColor> namedColorMapping = new Dictionary<string, HSLColor>()
		{
			{ "blue", HSLColor.FromRGB(46, 92, 244) },
			{ "red", HSLColor.FromRGB(255, 20, 0) },
			{ "green", HSLColor.FromRGB(160, 240, 140) },
			{ "teal", HSLColor.FromRGB(93, 194, 165) },
			{ "salmon", HSLColor.FromRGB(210, 153, 125) },
			{ "black", HSLColor.FromRGB(80, 80, 80) },
			{ "gold", HSLColor.FromRGB(246, 214, 121) },
			{ "pink", HSLColor.FromRGB(232, 85, 212) },
			{ "orange", HSLColor.FromRGB(255, 230, 149) },
			{ "lime", HSLColor.FromRGB(0, 255, 0) },
		};

		protected void SetNeutralPlayer(MapPlayers mapPlayers)
		{
			var section = "Neutral";
			var pr = new PlayerReference
			{
				Name = section,
				OwnsWorld = true,
				NonCombatant = true,
				Faction = "fguard",
				Color = HSLColor.FromRGB(255, 255, 255)
			};

			// Overwrite default player definitions if needed
			if (!mapPlayers.Players.ContainsKey(section))
				mapPlayers.Players.Add(section, pr);
			else
				mapPlayers.Players[section] = pr;
		}

		protected virtual int GetMatchingPlayerIndex(int index)
		{
			if (index > numMultiStarts)
			{
				// Just add to neutral
				return 0;
			}

			return index + 2;
		}

		protected virtual void SetMapPlayers(ScnFile file, List<string> players, MapPlayers mapPlayers)
		{
			int i = 0;
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
						Team = i + 2,
						Playable = true,
						Faction = "Random"
					};

					mapPlayers.Players.Add(multi.Name, multi);
					i++;
				}
			}

			numMultiStarts = i;
		}
	}
}
