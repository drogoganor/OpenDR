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
	class RawMaskTilesOutputCommand : IUtilityCommand
	{
		private const string OutputFilename = "..\\..\\mods\\dr\\MASK-TILES.txt";

		string IUtilityCommand.Name { get { return "--mask-tiles"; } }
		bool IUtilityCommand.ValidateArguments(string[] args) { return ValidateArguments(args); }

		[Desc("FILENAME", "Raw mask tile output for selected tileset.")]
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
			var startIndex = 184;
			var templateStartIndex = 30;
			var numTiles = 160;

			var endIndex = startIndex + numTiles;

			for (var index = 0; index < numTiles; index++)
			{
				var templateIndex = templateStartIndex + index;
				var frameIndex = startIndex + index;
				sb.AppendLine($@"	Template@{templateIndex}:
		Id: {templateIndex}
		Images: {tilFile}
		Size: 1,1
		Frames: {frameIndex}
		Categories: Terrain
		Tiles:
			0: Clear");
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
