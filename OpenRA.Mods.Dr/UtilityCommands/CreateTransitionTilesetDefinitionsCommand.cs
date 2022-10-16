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
using System.Text;

namespace OpenRA.Mods.Dr.UtilityCommands
{
	class CreateTransitionTilesetDefinitionsCommand : IUtilityCommand
	{
		string IUtilityCommand.Name { get { return "--create-transition-tiles"; } }
		bool IUtilityCommand.ValidateArguments(string[] args) { return ValidateArguments(args); }

		[Desc("Create transition tiles for tileset.")]
		void IUtilityCommand.Run(Utility utility, string[] args) { Run(utility); }

		public ModData ModData;

		protected bool ValidateArguments(string[] args)
		{
			return args.Length >= 0;
		}

		protected void Run(Utility utility)
		{
			// HACK: The engine code assumes that Game.modData is set.
			Game.ModData = ModData = utility.ModData;

			var startId = 30;
			var startFrame = 248;
			var numberOfTransitionTiles = 14;
			var numberOfValidTiles = 14;

			var targetPath = "..\\";

			var sb = new StringBuilder();
			for (int i = 0; i < (numberOfTransitionTiles * numberOfValidTiles); i++)
			{
				var id = startId + i;
				var frame = startFrame + i;

				sb.AppendLine($"\tTemplate@{id}:");
				sb.AppendLine($"\t\tId: {id}");
				sb.AppendLine("\t\tImages: BARREN/BARREN.TIL");
				sb.AppendLine("\t\tSize: 1,1");
				sb.AppendLine($"\t\tFrames: {frame}");
				sb.AppendLine("\t\tCategories: System");
				sb.AppendLine("\t\tTiles:");
				sb.AppendLine("\t\t\t0: Clear");
			}

			var outFile = "TRANSITION-TILES-OUT.txt";
			var outPath = Path.Combine(targetPath, outFile);
			using (var outputFile = new StreamWriter(outPath))
			{
				outputFile.Write(sb);
			}

			Console.WriteLine(outFile + "Saved.");
		}
	}
}
