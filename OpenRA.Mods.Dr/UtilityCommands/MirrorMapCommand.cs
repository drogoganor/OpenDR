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
using System.Text.RegularExpressions;
using OpenRA.FileSystem;

namespace OpenRA.Mods.Dr.UtilityCommands
{
	enum MirrorType
	{
		Horizontal,
		Vertical,
		HorizontalAndVertical
	}

	class TileTransform
	{
		static Dictionary<ushort, ushort> horizontalDictionary = new Dictionary<ushort, ushort>()
		{
			{ 24, 21 }, // LR
			{ 21, 24 },
			{ 19, 23 }, // TLRI
			{ 23, 19 },
			{ 16, 17 }, // BLRI
			{ 17, 16 },
			{ 26, 22 }, // TLRO
			{ 22, 26 },
			{ 29, 28 }, // BLRO
			{ 28, 29 },
		};

		static Dictionary<ushort, ushort> verticalDictionary = new Dictionary<ushort, ushort>()
		{
			{ 27, 18 }, // TB
			{ 18, 27 },
			{ 16, 23 }, // TBRI
			{ 23, 16 },
			{ 19, 17 }, // TBLI
			{ 17, 19 },
			{ 29, 22 }, // TBRO
			{ 22, 29 },
			{ 26, 28 }, // TBLO
			{ 28, 26 },
		};

		static Dictionary<ushort, ushort> horizontalAndVerticalDictionary = new Dictionary<ushort, ushort>()
		{
			{ 24, 21 }, // LR
			{ 21, 24 },
			{ 27, 18 }, // TB
			{ 18, 27 },
			{ 17, 23 }, // TRBLI
			{ 23, 17 },
			{ 19, 16 }, // TLBRI
			{ 16, 19 },
			{ 28, 22 }, // TRBLO
			{ 22, 28 },
			{ 26, 29 }, // TLBRO
			{ 29, 26 },
		};

		public MirrorType MirrorType;
		public TerrainTile Tile;
		public CPos Position;

		public IEnumerable<TileTransform> GetTransforms(Map map)
		{
			var horizontalTransform = new TileTransform()
			{
				Position = new CPos(map.MapSize.X - Position.X - 1, Position.Y),
				Tile = GetFlippedTerrainTile(MirrorType.Horizontal)
			};

			var verticalTransform = new TileTransform()
			{
				Position = new CPos(Position.X, map.MapSize.Y - Position.Y - 1),
				Tile = GetFlippedTerrainTile(MirrorType.Vertical)
			};

			var horizontalAndVerticalTransform = new TileTransform()
			{
				Position = new CPos(map.MapSize.X - Position.X - 1, map.MapSize.Y - Position.Y - 1),
				Tile = GetFlippedTerrainTile(MirrorType.HorizontalAndVertical)
			};

			switch (MirrorType)
			{
				case MirrorType.Horizontal:
					yield return horizontalTransform;
					break;
				case MirrorType.Vertical:
					yield return verticalTransform;
					break;
				case MirrorType.HorizontalAndVertical:
					yield return horizontalTransform;
					yield return verticalTransform;
					yield return horizontalAndVerticalTransform;
					break;
			}
		}

		TerrainTile GetFlippedTerrainTile(MirrorType mirrorType)
		{
			switch (mirrorType)
			{
				case MirrorType.Horizontal:
					if (horizontalDictionary.ContainsKey(Tile.Type))
						return new TerrainTile(horizontalDictionary[Tile.Type], Tile.Index);
					break;
				case MirrorType.Vertical:
					if (verticalDictionary.ContainsKey(Tile.Type))
						return new TerrainTile(verticalDictionary[Tile.Type], Tile.Index);
					break;
				case MirrorType.HorizontalAndVertical:
					if (horizontalAndVerticalDictionary.ContainsKey(Tile.Type))
						return new TerrainTile(horizontalAndVerticalDictionary[Tile.Type], Tile.Index);
					break;
			}

			return Tile;
		}
	}

	class ResourceTransform
	{
		public MirrorType MirrorType;
		public ResourceTile Tile;
		public CPos Position;

		public IEnumerable<ResourceTransform> GetTransforms(Map map)
		{
			var horizontalTransform = new ResourceTransform()
			{
				Position = new CPos(map.MapSize.X - Position.X - 1, Position.Y),
				Tile = Tile
			};

			var verticalTransform = new ResourceTransform()
			{
				Position = new CPos(Position.X, map.MapSize.Y - Position.Y - 1),
				Tile = Tile
			};

			var horizontalAndVerticalTransform = new ResourceTransform()
			{
				Position = new CPos(map.MapSize.X - Position.X - 1, map.MapSize.Y - Position.Y - 1),
				Tile = Tile
			};

			switch (MirrorType)
			{
				case MirrorType.Horizontal:
					yield return horizontalTransform;
					break;
				case MirrorType.Vertical:
					yield return verticalTransform;
					break;
				case MirrorType.HorizontalAndVertical:
					yield return horizontalTransform;
					yield return verticalTransform;
					yield return horizontalAndVerticalTransform;
					break;
			}
		}
	}

	class ActorTransform
	{
		static Dictionary<string, int2> mirrorOffsets = new Dictionary<string, int2>()
		{
			{ "mpspawn", new int2(3, 3) },
			{ "power", new int2(2, 3) },
			{ "waterlaunchpad", new int2(3, 2) },
			{ "hq.human", new int2(3, 3) },
			{ "hq.cyborg", new int2(3, 3) },
			{ "hq.togran", new int2(3, 3) },
			{ "trainingfacility.fguard", new int2(4, 3) },
			{ "trainingfacility.cyborg", new int2(4, 3) },
			{ "assemblyplant.human", new int2(4, 4) },
			{ "assemblyplant.cyborg", new int2(4, 4) },
			{ "laserturret", new int2(1, 1) },
			{ "plasmaturret", new int2(1, 1) },
			{ "antiairturret.human", new int2(1, 1) },
			{ "heavyrailturret", new int2(2, 2) },
			{ "neutronaccelerator", new int2(2, 2) },
			{ "hospital.human", new int2(3, 2) },
			{ "hospital.cyborg", new int2(3, 2) },
			{ "repair.human", new int2(3, 2) },
			{ "repair.cyborg", new int2(3, 2) },
			{ "phasingfacility", new int2(3, 2) },
			{ "rearmingdeck.human", new int2(2, 2) },
			{ "rearmingdeck.cyborg", new int2(2, 2) },
			{ "temporalgate", new int2(2, 1) },
			{ "temporalriftcreator", new int2(3, 3) },
			{ "trainingfacility.xenite", new int2(4, 3) },
			{ "matrix", new int2(3, 3) },
			{ "aotre000", new int2(0, -1) },
			{ "aotre001", new int2(0, -1) },
			{ "aotre002", new int2(0, -1) },
			{ "aotre003", new int2(0, -1) },
			{ "aotre005", new int2(0, -1) },
			{ "aoroc003", new int2(1, 2) },
			{ "aoroc004", new int2(1, 1) },
			{ "aoroc005", new int2(1, 1) },
			{ "aoclf000", new int2(1, 2) },
			{ "aoclf001", new int2(1, 2) },
			{ "aoclf002", new int2(1, 2) },
			{ "aoclf003", new int2(1, 3) },
			{ "aoclf004", new int2(1, 3) },
			{ "aoclf005", new int2(3, 3) },
			{ "aotre000.snow", new int2(0, -1) },
			{ "aotre001.snow", new int2(0, -1) },
			{ "aotre002.snow", new int2(0, -1) },
			{ "aotre003.snow", new int2(0, -1) },
			{ "aotre005.snow", new int2(0, -1) },
			{ "aoroc003.snow", new int2(1, 2) },
			{ "aoroc004.snow", new int2(1, 1) },
			{ "aoroc005.snow", new int2(1, 1) },
			{ "aoclf000.snow", new int2(1, 2) },
			{ "aoclf001.snow", new int2(1, 2) },
			{ "aoclf002.snow", new int2(1, 2) },
			{ "aoclf003.snow", new int2(1, 3) },
			{ "aoclf004.snow", new int2(1, 3) },
			{ "aoclf005.snow", new int2(3, 3) },
			{ "aotre000.jungle", new int2(0, -1) },
			{ "aotre001.jungle", new int2(0, -1) },
			{ "aotre002.jungle", new int2(0, -1) },
			{ "aotre003.jungle", new int2(0, -1) },
			{ "aotre005.jungle", new int2(0, -1) },
			{ "aoroc003.jungle", new int2(1, 2) },
			{ "aoroc004.jungle", new int2(1, 1) },
			{ "aoroc005.jungle", new int2(1, 1) },
			{ "aoclf000.jungle", new int2(1, 2) },
			{ "aoclf001.jungle", new int2(1, 2) },
			{ "aoclf002.jungle", new int2(1, 2) },
			{ "aoclf003.jungle", new int2(1, 3) },
			{ "aoclf004.jungle", new int2(1, 3) },
			{ "aoclf005.jungle", new int2(3, 3) },
			{ "aotre000.base", new int2(0, -1) },
			{ "aotre001.base", new int2(0, -1) },
			{ "aotre002.base", new int2(0, -1) },
			{ "aotre003.base", new int2(0, -1) },
			{ "aotre005.base", new int2(0, -1) },
			{ "aoroc003.base", new int2(1, 2) },
			{ "aoroc004.base", new int2(1, 1) },
			{ "aoroc005.base", new int2(1, 1) },
			{ "aoclf000.base", new int2(1, 2) },
			{ "aoclf001.base", new int2(1, 2) },
			{ "aoclf002.base", new int2(1, 2) },
			{ "aoclf003.base", new int2(1, 3) },
			{ "aoclf004.base", new int2(1, 3) },
			{ "aoclf005.base", new int2(3, 3) },
		};

		public MirrorType MirrorType;
		public ActorReference Actor;
		public CPos Position;

		public IEnumerable<ActorTransform> GetTransforms(Map map)
		{
			var offset = int2.Zero;
			if (mirrorOffsets.ContainsKey(Actor.Type))
				offset = mirrorOffsets[Actor.Type];

			var horizontalTransform = new ActorTransform()
			{
				Position = new CPos(map.MapSize.X - Position.X - offset.X - 1, Position.Y),
				Actor = Actor
			};

			var verticalTransform = new ActorTransform()
			{
				Position = new CPos(Position.X, map.MapSize.Y - Position.Y - offset.Y - 1),
				Actor = Actor
			};

			var horizontalAndVerticalTransform = new ActorTransform()
			{
				Position = new CPos(map.MapSize.X - Position.X - offset.X - 1, map.MapSize.Y - Position.Y - offset.Y - 1),
				Actor = Actor
			};

			switch (MirrorType)
			{
				case MirrorType.Horizontal:
					yield return horizontalTransform;
					break;
				case MirrorType.Vertical:
					yield return verticalTransform;
					break;
				case MirrorType.HorizontalAndVertical:
					yield return horizontalTransform;
					yield return verticalTransform;
					yield return horizontalAndVerticalTransform;
					break;
			}
		}
	}

	class MirrorMapCommand : IUtilityCommand
	{
		string IUtilityCommand.Name { get { return "--mirror-map"; } }
		bool IUtilityCommand.ValidateArguments(string[] args) { return ValidateArguments(args); }

		[Desc("FILENAME", "Mirror an OpenRA Dark Reign map.")]
		void IUtilityCommand.Run(Utility utility, string[] args) { Run(utility, args); }

		public ModData ModData;
		public Map Map;
		int actorIndex = 0;

		protected bool ValidateArguments(string[] args)
		{
			return args.Length >= 2;
		}

		protected void Run(Utility utility, string[] args)
		{
			// HACK: The engine code assumes that Game.modData is set.
			Game.ModData = ModData = utility.ModData;

			var filename = args[1];
			var flag = args[2];
			if (string.IsNullOrWhiteSpace(flag))
				flag = "VH";

			var flipHorizontal = flag.Contains('H');
			var flipVertical = flag.Contains('V');

			var mirrorType = MirrorType.Horizontal;
			if (flipVertical)
				mirrorType = MirrorType.Vertical;
			if (flipHorizontal && flipVertical)
				mirrorType = MirrorType.HorizontalAndVertical;

			var targetPath = "..\\mods\\dr\\maps";

			var package = new Folder(targetPath).OpenPackage(filename, ModData.ModFiles);
			if (package == null)
			{
				Console.WriteLine("Couldn't find map file: " + filename);
				return;
			}

			Map = new Map(ModData, package);
			var size = Map.MapSize;
			switch (mirrorType)
			{
				case MirrorType.Horizontal:
					size = size.WithX(size.X / 2);
					break;
				case MirrorType.Vertical:
					size = size.WithY(size.Y / 2);
					break;
				case MirrorType.HorizontalAndVertical:
					size = size / 2;
					break;
			}

			// Tiles
			for (int x = 0; x < size.X; x++)
			{
				for (int y = 0; y < size.Y; y++)
				{
					var pos = new CPos(x, y);
					var transformTile = new TileTransform()
					{
						Tile = Map.Tiles[pos],
						MirrorType = mirrorType,
						Position = pos
					};

					foreach (var tt in transformTile.GetTransforms(Map))
					{
						var newPos = tt.Position;
						Map.Tiles[newPos] = tt.Tile;
					}
				}
			}

			// Actors
			actorIndex = GetHighestActorIndex();
			int multiCount = 0;

			var actorDefs = new List<ActorReference>();
			var removeActors = new List<MiniYamlNode>();
			foreach (var a in Map.ActorDefinitions)
			{
				var existing = new ActorReference(a.Value.Value, a.Value.ToDictionary());
				var pos = existing.GetOrDefault<LocationInit>().Value;
				var owner = existing.Get<OwnerInit>();

				if (pos.X < 0 || pos.X >= size.X ||
					pos.Y < 0 || pos.Y >= size.Y)
				{
					removeActors.Add(a);
					continue;
				}

				var actor = new ActorTransform()
				{
					Actor = existing,
					Position = pos,
					MirrorType = mirrorType,
				};

				if (actor.Actor.Type == "mpspawn")
					multiCount++;

				foreach (var at in actor.GetTransforms(Map))
				{
					var ar = new ActorReference(actor.Actor.Type)
					{
						new LocationInit(at.Position),
						owner
					};

					actorDefs.Add(ar);

					if (at.Actor.Type == "mpspawn")
						multiCount++;
				}
			}

			foreach (var a in actorDefs)
			{
				Map.ActorDefinitions.Add(new MiniYamlNode("Actor" + ++actorIndex, a.Save()));
			}

			foreach (var a in removeActors)
			{
				Map.ActorDefinitions.Remove(a);
			}

			if (multiCount > 0)
			{
				var mapPlayers = new MapPlayers(Map.Rules, multiCount);
				Map.PlayerDefinitions = mapPlayers.ToMiniYaml();
			}

			// Resources
			for (int x = 0; x < size.X; x++)
			{
				for (int y = 0; y < size.Y; y++)
				{
					var pos = new CPos(x, y);
					var resource = new ResourceTransform()
					{
						Tile = Map.Resources[pos],
						MirrorType = mirrorType,
						Position = pos
					};

					foreach (var rt in resource.GetTransforms(Map))
					{
						var newPos = rt.Position;
						Map.Resources[newPos] = rt.Tile;
					}
				}
			}

			var dest = Path.Combine(targetPath, Path.GetFileNameWithoutExtension(filename) + ".oramap");

			Map.Save(ZipFileLoader.Create(dest));
			Console.WriteLine(dest + " saved.");
		}

		int GetHighestActorIndex()
		{
			var pattern = new Regex(@"Actor(?<actorId>\d+)");

			return Map.ActorDefinitions
				.Select(x => pattern.Match(x.Key).Groups["actorId"])
				.Where(x => !string.IsNullOrEmpty(x.Value))
				.Select(x => int.Parse(x.Value))
				.Max();
		}
	}
}
