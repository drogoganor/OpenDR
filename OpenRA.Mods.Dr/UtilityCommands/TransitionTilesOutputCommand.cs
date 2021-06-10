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
using System.Text;
using System.Text.RegularExpressions;
using OpenRA.FileSystem;
using OpenRA.Graphics;
using OpenRA.Mods.Dr.FileFormats;
using static System.Console;

namespace OpenRA.Mods.Dr.UtilityCommands
{
	class TransitionTilesOutputCommand : IUtilityCommand
	{
		private const string OutputFilename = "..\\..\\mods\\dr\\TRANSITION-TILES.txt";

		string IUtilityCommand.Name { get { return "--transition-tiles"; } }
		bool IUtilityCommand.ValidateArguments(string[] args) { return ValidateArguments(args); }

		[Desc("FILENAME", "Transition tile output for selected tileset.")]
		void IUtilityCommand.Run(Utility utility, string[] args) { Run(utility, args); }

		protected ModData modData;

		protected bool ValidateArguments(string[] args)
		{
			return args.Length >= 0;
		}

		protected void Run(Utility utility, string[] args)
		{
			// HACK: The engine code assumes that Game.modData is set.
			Game.ModData = modData = utility.ModData;

			var sb = new StringBuilder();

			var tilFile = "BARREN/BARREN.TIL";
			var startIndex = 364;
			var templateStartIndex = 200;
			var numFramesPer = 4;
			var numGroups = 16;

			var totalFrames = numGroups * numFramesPer;
			var endIndex = startIndex + totalFrames;

			for (var groupIndex = 0; groupIndex < numGroups; groupIndex++)
			{
				var index = groupIndex * numFramesPer;
				var templateIndex = templateStartIndex + groupIndex;
				var frames = Enumerable.Range(startIndex + index, numFramesPer);
				var frameStr = string.Join(", ", frames);
				sb.AppendLine($@"	Template@{templateIndex}:
		Id: {templateIndex}
		Images: {tilFile}
		Size: 1,1
		Frames: {frameStr}
		Categories: Terrain
		PickAny: True
		Tiles:");

				for (var i = 0; i < numFramesPer; i++)
				{
					sb.AppendLine($"			{i}: Clear");
				}
			}

			try
			{
				using (var sw = new StreamWriter(OutputFilename))
				{
					sw.Write(sb.ToString());
				}
			}
			catch (Exception ex)
			{
				WriteLine("Couldn't write destination file.", ex);
				throw;
			}

			WriteLine($"Wrote file: {OutputFilename}");
		}
	}
}
