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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace OpenRA.Mods.Dr.FileFormats
{
	public class ScnFile
	{
		public IEnumerable<ScnSection> Entries { get { return entries; } }

		List<ScnSection> entries = new List<ScnSection>();

		const string PlayerStartStr = "SetStartLocation";
		const string AddThingStr = "AddThingAt";
		const string SetTeamSideStr = "SetTeamSide";
		const string SetDefaultTerrainStr = "SetDefaultTerrain";
		const string AddBuildingAtStr = "AddBuildingAt";

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
			var reader = new StreamReader(s);
			string lastEntry = null;

			while (!reader.EndOfStream)
			{
				var line = reader.ReadLine();

				if (line.Length == 0) continue;

				switch (line[0])
				{
					case ';':
						break;
					case '{':
					case '}':
						break;
					default:
						ProcessEntry(line);
						break;
				}
			}
		}
		
		bool ProcessEntry(string line)
		{
			var comment = line.IndexOf(';');
			if (comment >= 0)
				line = line.Substring(0, comment);

			line = line.Trim();
			if (line.Length == 0)
				return false;
			
			var scnEntry = new ScnSection(line);


			if (line.StartsWith(PlayerStartStr))
				entries.Add(scnEntry);
			if (line.StartsWith(AddThingStr))
				entries.Add(scnEntry);
			if (line.StartsWith(SetTeamSideStr))
				entries.Add(scnEntry);
			if (line.StartsWith(SetDefaultTerrainStr))
				entries.Add(scnEntry);
			if (line.StartsWith(AddBuildingAtStr))
				entries.Add(scnEntry);

			//var key = line;
			//var value = "";
			//var eq = line.IndexOf('=');
			//if (eq >= 0)
			//{
			//	key = line.Substring(0, eq).Trim();
			//	value = line.Substring(eq + 1, line.Length - eq - 1).Trim();
			//}

			//if (currentSection == null)
			//	throw new InvalidOperationException("No current INI section");

			//if (!currentSection.Contains(key))
			//	currentSection.Add(key, value);


			return true;
		}
	}

	public class ScnSection
	{
		public string Name { get; private set; }
		public string ValuesStr { get; private set; }
		public string[] Values { get; private set; }
		string rawValue { get; set; }

		public ScnSection(string raw)
		{
			rawValue = raw;

			var openBracketIndex = raw.IndexOf("(");
			var closeBracketIndex = raw.IndexOf(")");
			Name = raw.Substring(0, openBracketIndex);
			ValuesStr = raw.Substring(openBracketIndex + 1, (closeBracketIndex - openBracketIndex) - 1);

			Values = ValuesStr.Split(new[] {' '});
		}
	}
}
