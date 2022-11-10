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
using System.Linq;
using OpenRA.FileSystem;
using OpenRA.Mods.Dr.FileFormats;
using OpenRA.Primitives;

namespace OpenRA.Mods.Dr.UtilityCommands
{
	class ImportDrMapCommand : IUtilityCommand
	{
		string IUtilityCommand.Name { get { return "--import-dr-maps"; } }
		bool IUtilityCommand.ValidateArguments(string[] args) { return ValidateArguments(args); }

		[Desc("FILENAME", "Convert a DR map to the OpenRA format.")]
		void IUtilityCommand.Run(Utility utility, string[] args) { Run(utility, args); }

		public ModData ModData;
		public Map Map;
		public List<string> Players = new List<string>();
		public MapPlayers MapPlayers;
		int numMultiStarts = 0;
		protected bool skipActors = true;

		static readonly string[] KnownUnknownThings = new string[]
		{
			"smcrater", // Not sure if these are ever deliberately placed on a map
			"medcrater",
			"bigcrater",
			"largercrater",
			"hugecrater1",
			"hugecrater2", // Rubble and ruins
			"hugecrater3",
			"hugecrater4",
			"hugecrater5",
			"hugecrater6",
			"animtree1",
			"animtree2",
			"animtree3",
			"animtree4",
			"animtree5",
			"animtree6"
		};

		static readonly string[] KnownUnknownBuildings = new string[]
		{
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
			"impww",
			"impmn",
		};

		static readonly string[] KnownUnknownUnits = Array.Empty<string>();

		static readonly Dictionary<string, string> ThingNames = new Dictionary<string, string>()
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

		static readonly Dictionary<string, string> UnitNames = new Dictionary<string, string>
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

		static readonly Dictionary<string, string> BuildingNames = new Dictionary<string, string>
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
			{ "timpre", "Repair.cyborg" },
		};

		protected bool ValidateArguments(string[] args)
		{
			return args.Length >= 2;
		}

		protected void Run(Utility utility, string[] args)
		{
			// HACK: The engine code assumes that Game.modData is set.
			Game.ModData = ModData = utility.ModData;

			var rootPath = $"..\\..\\{args[1]}\\";

			var drMapDirs = Directory.GetDirectories(rootPath);
			foreach (var drMapDir in drMapDirs)
			{
				var dirName = new DirectoryInfo(drMapDir).Name;
				var targetMapFile = $"{dirName}\\{dirName}.map";
				ConvertMap(rootPath, targetMapFile);
			}
		}

		void ConvertMap(string rootPath, string mapFilename)
		{
			var filename = rootPath + mapFilename;
			Console.WriteLine($"Processing {filename}");

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

				if (width == 0 || height == 0)
					return;

				var tilesetNum = stream.ReadInt32(); // Tileset num???
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

				// Change Snow and Alien to something else
				if (tilesetName == "SNOW")
				{
					tilesetName = "AURALIEN";
				}
				else if (tilesetName == "ALIEN")
				{
					tilesetName = "ASTEROID";
				}

				if (!ModData.DefaultTerrainInfo.TryGetValue(tilesetName, out var terrainInfo))
					throw new InvalidDataException($"Unknown tileset {tilesetName}");

				Map = new Map(ModData, terrainInfo, width + 2, height + 2)
				{
					Title = Path.GetFileNameWithoutExtension(filename),
					Author = "OpenDR",
					RequiresMod = ModData.Manifest.Id
				};

				SetBounds(Map, width + 2, height + 2);

				var unknownTileTypeHash = new HashSet<int>();

				for (var y = 0; y < height; y++)
				{
					for (var x = 0; x < width; x++)
					{
						var byte1 = stream.ReadUInt8(); // Tile type 0-63, with art variations repeated 1-4
						var byte2 = stream.ReadUInt8(); // Which art variation to use. 0 = 1-4, 1 = 5-8
						var byte3 = stream.ReadUInt8(); // Base elevation, defaults to 2.
						var byte4 = stream.ReadUInt8(); // Unknown, defaults to 36. Seems to be elevation related.
						var byte5 = stream.ReadUInt8(); // Unknown, defaults to 73. Seems to be elevation related.
						var byte6 = stream.ReadUInt8(); // Unknown, defaults to 146. Seems to be elevation related.

						var subindex = (byte)(byte1 / 64);
						var variation = (byte)(subindex * (byte2 + 1));
						var tileType = byte1 % 16;

						tileType--;
						if (tileType < 0)
						{
							tileType = 15;
						}

						var tilePos = new CPos(x + 1, y + 1);
						Map.Tiles[tilePos] = new TerrainTile((ushort)tileType, variation);
						Map.Height[tilePos] = byte3;
					}
				}

				// What's after the tiles? Water/Taelon?
				stream.ReadInt32(); // Always one
				stream.ReadInt32(); // Always 256
				var length = stream.ReadInt32(); // Byte length of remaining data

				var byte1Hash = new HashSet<byte>();
				var byteList = new List<byte>();
				for (var i = 0; i < length; i++)
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
					var i = 0;
					foreach (var scnSection in scnFile.Entries)
					{
						if (scnSection.Name != "SetStartLocation")
							continue;

						var divisor = 24;
						var x = Convert.ToInt32(scnSection.Values[0]) / divisor;
						var y = Convert.ToInt32(scnSection.Values[1]) / divisor;
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

						var type = scnSection.Values[1];
						var x = Convert.ToInt32(scnSection.Values[2]);
						var y = Convert.ToInt32(scnSection.Values[3]);

						var matchingActor = string.Empty;

						if (ThingNames.ContainsKey(type))
							matchingActor = ThingNames[type];
						else if (!KnownUnknownThings.Contains(type))
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

					var currentTeam = 0;

					if (!skipActors)
					{
						// Units
						foreach (var scnSection in scnFile.Entries)
						{
							if (scnSection.Name == "SetDefaultTeam")
							{
								currentTeam = Convert.ToInt32(scnSection.ValuesStr);
								continue;
							}

							if (scnSection.Name != "PutUnitAt")
								continue;

							var playerIndex = GetMatchingPlayerIndex(currentTeam); // to skip creeps and neutral if necessary

							var type = scnSection.Values[1];
							var x = Convert.ToInt32(scnSection.Values[2]);
							var y = Convert.ToInt32(scnSection.Values[3]);

							var matchingActor = string.Empty;

							if (UnitNames.ContainsKey(type))
								matchingActor = UnitNames[type];
							else if (!KnownUnknownUnits.Contains(type))
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

							var playerIndex = GetMatchingPlayerIndex(currentTeam); // to skip creeps and neutral if necessary

							var type = scnSection.Values[1];
							var x = Convert.ToInt32(scnSection.Values[2]);
							var y = Convert.ToInt32(scnSection.Values[3]);

							var matchingActor = string.Empty;

							if (BuildingNames.ContainsKey(type))
								matchingActor = BuildingNames[type];
							else if (!KnownUnknownBuildings.Contains(type))
								throw new Exception("Unknown building name: " + type);

							// Resources
							var ownerName = MapPlayers.Players.Values.First(p => p.Team == playerIndex).Name;
							if (x >= 0 && y >= 0 && !string.IsNullOrEmpty(matchingActor))
							{
								var ar = new ActorReference(matchingActor)
								{
									new LocationInit(new CPos(x, y)),
									new OwnerInit(ownerName)
								};

								Map.ActorDefinitions.Add(new MiniYamlNode("Actor" + i++, ar.Save()));
							}
						}
					}

					foreach (var scnSection in scnFile.Entries)
					{
						if (scnSection.Name != "AddBuildingAt")
							continue;

						var type = scnSection.Values[1];
						var x = Convert.ToInt32(scnSection.Values[2]);
						var y = Convert.ToInt32(scnSection.Values[3]);

						if (type == "impww")
						{
							// Place water well
							Map.Resources[new CPos(x, y)] = new ResourceTile(1, 255);
						}
						else if (type == "impmn")
						{
							// Place taelon
							Map.Resources[new CPos(x, y)] = new ResourceTile(2, 255);
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

			var dest = Path.Combine("..\\..\\mods\\dr\\maps", Path.GetFileNameWithoutExtension(mapFilename).ToLowerInvariant() + ".oramap");

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
		protected Dictionary<string, Color> namedColorMapping = new Dictionary<string, Color>()
		{
			{ "blue", Color.FromArgb(46, 92, 244) },
			{ "red", Color.FromArgb(255, 20, 0) },
			{ "green", Color.FromArgb(160, 240, 140) },
			{ "teal", Color.FromArgb(93, 194, 165) },
			{ "salmon", Color.FromArgb(210, 153, 125) },
			{ "black", Color.FromArgb(80, 80, 80) },
			{ "gold", Color.FromArgb(246, 214, 121) },
			{ "pink", Color.FromArgb(232, 85, 212) },
			{ "orange", Color.FromArgb(255, 230, 149) },
			{ "lime", Color.FromArgb(0, 255, 0) },
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
				Color = Color.FromArgb(255, 255, 255)
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

				var x = Convert.ToInt32(scnSection.Values[0]);
				var y = Convert.ToInt32(scnSection.Values[1]);
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
