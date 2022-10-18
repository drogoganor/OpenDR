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
using System.IO;
using OpenRA.FileSystem;

namespace OpenRA.Mods.Dr.UtilityCommands
{
	class StripManualSystemTilesCommand : IUtilityCommand
	{
		string IUtilityCommand.Name { get { return "--strip-system-tiles"; } }
		bool IUtilityCommand.ValidateArguments(string[] args) { return ValidateArguments(args); }

		[Desc("FILENAME", "Strip manually-placed system tiles from all maps")]
		void IUtilityCommand.Run(Utility utility, string[] _) { Run(utility); }

		public ModData ModData;

		protected bool ValidateArguments(string[] args)
		{
			return args.Length >= 0;
		}

		protected void Run(Utility utility)
		{
			// HACK: The engine code assumes that Game.modData is set.
			Game.ModData = ModData = utility.ModData;
			var targetPath = "..\\..\\mods\\dr\\maps";
			var unpackedMapFiles = Directory.GetDirectories(targetPath);
			foreach (var unpackedMapFile in unpackedMapFiles)
			{
				var package = new Folder(".").OpenPackage(unpackedMapFile, ModData.ModFiles);
				if (package == null)
				{
					Console.WriteLine("Couldn't find map file: " + unpackedMapFile);
					return;
				}

				var map = ProcessMap(package);

				map.Save(new Folder(unpackedMapFile));
				Console.WriteLine(unpackedMapFile + " saved.");
			}

			var packedMapFiles = Directory.GetFiles(targetPath, "*.oramap");
			foreach (var packedMapFile in packedMapFiles)
			{
				ZipFileLoader.TryParseReadWritePackage(packedMapFile, out var package);
				if (package == null)
				{
					Console.WriteLine("Couldn't find map file: " + packedMapFile);
					return;
				}

				var map = ProcessMap(package);

				map.Save(package);
				Console.WriteLine(packedMapFile + " saved.");
			}

			Console.WriteLine("Complete.");
		}

		Map ProcessMap(IReadOnlyPackage package)
		{
			var map = new Map(ModData, package);
			foreach (var cell in map.AllCells)
			{
				var tile = map.Tiles[cell];
				if (tile.Type > 15)
				{
					map.Tiles[cell] = new TerrainTile(0, (byte)Game.CosmeticRandom.Next(8));
				}
			}

			return map;
		}
	}
}
