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

namespace OpenRA.Mods.Dr.FileFormats
{
	public class ScnFile
	{
		const string SpecialForcesStr = "DefineSpecialForces";
		readonly string[] acceptableEntries =
			new[]
			{
				"SetStartLocation", "AddThingAt", "PutUnitAt", "SetTeam", "SetTeamSide", "SetAlliance", "SetDefaultTerrain", "AddBuildingAt",
				"SetDefaultTeam"
			};
		bool skipNextBlock = false;

		public IEnumerable<ScnSection> Entries
		{
			get { return entries; }
		}

		readonly List<ScnSection> entries = new();

		public ScnFile(Stream s)
		{
			using (s)
				Load(s);
		}

		public ScnFile(params Stream[] streams)
		{
			foreach (var s in streams)
				Load(s);
		}

		public void Load(Stream s)
		{
			try
			{
				var reader = new StreamReader(s);

				while (!reader.EndOfStream)
				{
					var line = reader.ReadLine();
					if (line == null)
					{
						break;
					}

					if (line.Length == 0) continue;

					switch (line[0])
					{
						case ';':
						case '{':
							break;
						case '}':
							skipNextBlock = false;
							break;
						default:
							ProcessEntry(line);
							break;
					}
				}
			}
			catch (Exception)
			{
				throw;
			}
		}

		bool ProcessEntry(string line)
		{
			if (skipNextBlock)
				return false;

			var comment = line.IndexOf(';');
			if (comment >= 0)
				line = line[..comment];

			line = line.Trim();
			if (line.Length == 0)
				return false;

			var scnEntry = new ScnSection(line);

			if (acceptableEntries.Any(x => line.StartsWith(x, StringComparison.InvariantCultureIgnoreCase)))
				entries.Add(scnEntry);

			if (line.StartsWith(SpecialForcesStr, StringComparison.InvariantCultureIgnoreCase))
			{
				skipNextBlock = true;
				return false;
			}

			return true;
		}
	}

	public class ScnSection
	{
		public string Name { get; }
		public string ValuesStr { get; }
		public string[] Values { get; }

		public ScnSection(string raw)
		{
			var openBracketIndex = raw.IndexOf("(", StringComparison.InvariantCulture);
			var closeBracketIndex = raw.IndexOf(")", StringComparison.InvariantCulture);
			Name = raw[..openBracketIndex];
			ValuesStr = raw.Substring(openBracketIndex + 1, closeBracketIndex - openBracketIndex - 1);

			Values = ValuesStr.Split(new[] { ' ' });
		}
	}
}
