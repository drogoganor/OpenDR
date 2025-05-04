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
using System.Globalization;
using System.IO;
using System.Linq;
using OpenRA.FileSystem;
using OpenRA.Mods.Dr.FileFormats;
using OpenRA.Primitives;

namespace OpenRA.Mods.Dr.UtilityCommands
{
	class ConvertDrMapResourcesCommand : IUtilityCommand
	{
		string IUtilityCommand.Name { get { return "--convert-dr-map-resources"; } }
		bool IUtilityCommand.ValidateArguments(string[] args) { return ValidateArguments(args); }

		[Desc("Convert old actor-based DR map resources to the new plain old resources.")]
		void IUtilityCommand.Run(Utility utility, string[] args) { Run(utility); }

		public ModData ModData;
		public Map Map;
		public List<string> Players = new();
		int numMultiStarts = 0;
		protected bool skipActors = true;

		protected static bool ValidateArguments(string[] _)
		{
			return true;
		}

		protected void Run(Utility utility)
		{
			// HACK: The engine code assumes that Game.modData is set.
			Game.ModData = ModData = utility.ModData;

			const string TargetPath = "..\\..\\mods\\dr\\maps";

			// var packedMapFiles = Directory.GetFiles(targetPath, "*.oramap");
			var packedMapFiles = Directory.GetDirectories(TargetPath);

			foreach (var packedMapFile in packedMapFiles)
			{
				var package = new Folder(".").OpenPackage(packedMapFile, ModData.ModFiles);
				if (package == null)
				{
					Console.WriteLine("Couldn't find map file: " + packedMapFile);
					return;
				}

				Map = new Map(ModData, package);

				var duds = Map.ActorDefinitions.Where(x => !x.Value.Value.Contains("jungle")).ToArray();
				var resourceActors = Map.ActorDefinitions.Where(x => x.Value.Value == "water" || x.Value.Value == "taelon").ToArray();

				foreach (var resourceActor in resourceActors)
				{
					var locationNode = resourceActor.Value.Nodes.First(x => x.Key == "Location");
					var resourceLocations = locationNode.Value.Value.ToString().Split(',').Select(x => Convert.ToInt32(x, CultureInfo.InvariantCulture)).ToArray();
					var pos = new CPos(resourceLocations[0], resourceLocations[1]);

					var resourceType = resourceActor.Value.Value == "water" ? 1 : 2;
					Map.Resources[pos] = new ResourceTile((byte)resourceType, 255);
				}

				for (var i = 0; i < resourceActors.Length; i++)
				{
					// TODO: Broken in playtest-20241116
					// Map.ActorDefinitions.Remove(resourceActors[i]);
				}

				Map.Save(new Folder(packedMapFile));
				Console.WriteLine(packedMapFile + " saved.");
			}
		}

		/*
		static void SetBounds(Map map, int width, int height)
		{
			var tl = new PPos(1, 1);
			var br = new PPos(0 + width - 2, 0 + height - 2);
			map.SetBounds(tl, br);
		}
		*/

		// TODO: fix this -- will have bitrotted pretty badly.
		protected Dictionary<string, Color> namedColorMapping = new()
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
			var i = 0;
			foreach (var scnSection in file.Entries)
			{
				if (scnSection.Name != "SetStartLocation")
					continue;

				var x = Convert.ToInt32(scnSection.Values[0], CultureInfo.InvariantCulture);
				var y = Convert.ToInt32(scnSection.Values[1], CultureInfo.InvariantCulture);
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
