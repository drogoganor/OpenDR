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
using System.Linq;
using OpenRA.Mods.Dr.FileFormats;
using OpenRA.Primitives;

namespace OpenRA.Mods.Dr.UtilityCommands
{
    class ImportDrCampaignMapCommand : ImportDrMapCommand, IUtilityCommand
    {
        string IUtilityCommand.Name { get { return "--import-dr-campaign-map"; } }
        bool IUtilityCommand.ValidateArguments(string[] args) { return ValidateArguments(args); }

        [Desc("FILENAME", "Convert a Dark Reign campaign map to the OpenRA format.")]
        void IUtilityCommand.Run(Utility utility, string[] args)
        {
            skipActors = false;
            Run(utility, args);
        }

        protected override int GetMatchingPlayerIndex(int index)
        {
            if (index == 8)
            {
                // Just add to neutral
                return 0;
            }

            return index + 2; // to skip creeps and neutral
        }

        protected override void SetMapPlayers(ScnFile file, List<string> players, MapPlayers mapPlayers)
        {
            var teamHasUnits = new Func<int, bool>(playerIndex =>
            {
                bool correctPlayer = false;
                foreach (var scnSection in file.Entries)
                {
                    if (scnSection.Name == "SetDefaultTeam")
                    {
                        int defaultTeam = Convert.ToInt32(scnSection.Values[0]);
                        correctPlayer = defaultTeam == playerIndex;
                        continue;
                    }

                    if (correctPlayer && (scnSection.Name == "AddBuildingAt" || scnSection.Name == "PutUnitAt"))
                    {
                        return true;
                    }
                }

                return false;
            });

            // Single player, examine sides
            int teamIndex = 0;
            int sideIndex = 0;
            int[] allianceArray = new[] { 0, 0, 0, 0, 0, 0, 0, 0 };
            var nameFactionDict = new Dictionary<string, string>()
            {
                { "Freedom Guard", "fguard" },
                { "Imperium", "imperium" },
            };

            var factionColors = new[] { Color.FromArgb(234, 189, 25), Color.FromArgb(124, 60, 234) };
            var newPlayers = new List<PlayerReference>();

            // Add players
            foreach (var scnSection in file.Entries)
            {
                if (scnSection.Name == "SetTeam")
                {
                    teamIndex = Convert.ToInt32(scnSection.Values[0]); // to skip creeps and normal
                    continue;
                }

                if (scnSection.Name == "SetTeamSide")
                {
                    sideIndex = Convert.ToInt32(scnSection.Values[0]);
                    if (sideIndex > 1) // No togran yet.
                        sideIndex = 0;

                    continue;
                }

                if (scnSection.Name == "SetAlliance" && teamHasUnits(teamIndex))
                {
                    if (teamIndex == 8)
                        continue; // It's just creeps

                    for (int allianceI = 0; allianceI < 8; allianceI++)
                    {
                        allianceArray[allianceI] = Convert.ToInt32(scnSection.Values[allianceI]);
                    }

                    var enemyIndices = new List<int>();
                    var allyIndices = new List<int>();
                    for (int ei = 0; ei < 8; ei++)
                    {
                        if (allianceArray[ei] == 0)
                            enemyIndices.Add(ei);
                        else if (ei != teamIndex && allianceArray[ei] == 2)
                            allyIndices.Add(ei);
                    }

                    // Create player at this point
                    PlayerReference newPlayer = new PlayerReference
                    {
                        Team = teamIndex,
                        Name = sideIndex.ToString(),
                        Faction = nameFactionDict.Values.ToArray()[sideIndex],
                        Color = factionColors[sideIndex],
                        Enemies = enemyIndices.Select(x => x.ToString()).ToArray(),
                        Allies = allyIndices.Select(x => x.ToString()).ToArray(),
                    };

                    if (teamIndex == 0)
                    {
                        // First is always playable faction
                        newPlayer.Playable = true;
                        newPlayer.AllowBots = false;
                        newPlayer.Required = true;
                        newPlayer.LockSpawn = true;
                        newPlayer.LockTeam = true;
                    }

                    newPlayers.Add(newPlayer);
                }
            }

            // Ensure names are unique
            var factionCountIndex = new Dictionary<string, int>()
            {
                { "fguard", 0 },
                { "imperium", 0 }
            };

            foreach (var newPlayer in newPlayers)
            {
                // Sort out unique names
                sideIndex = Convert.ToInt32(newPlayer.Name);
                var sideName = nameFactionDict.Keys.ToArray()[sideIndex];
                int currentFactionCount = factionCountIndex[newPlayer.Faction];
                if (currentFactionCount > 0)
                {
                    sideName += " " + currentFactionCount;
                }

                currentFactionCount++;
                factionCountIndex[newPlayer.Faction] = currentFactionCount;
                newPlayer.Name = sideName;

                // And unique colors
                if (newPlayer.Team > 1 && newPlayer.Team != 8)
                {
                    int newColorIndex = newPlayer.Team - 2;
                    newPlayer.Color = namedColorMapping.Values.ToArray()[newColorIndex];
                }
            }

            foreach (var newPlayer in newPlayers)
            {
                // Sort out alliances
                teamIndex = newPlayer.Team;
                var allyNames = new List<string>();
                var enemyNames = new List<string>();
                foreach (var allyOrEnemy in newPlayers)
                {
                    if (allyOrEnemy.Team == teamIndex)
                        continue;

                    if (newPlayer.Allies.Contains(allyOrEnemy.Team.ToString()))
                        allyNames.Add(allyOrEnemy.Name);
                    else if (newPlayer.Enemies.Contains(allyOrEnemy.Team.ToString()))
                        enemyNames.Add(allyOrEnemy.Name);
                }

                newPlayer.Allies = allyNames.ToArray();
                newPlayer.Enemies = enemyNames.ToArray();
            }

            // Increase the team indices by two to skip creeps and neutral.
            foreach (var newPlayer in newPlayers)
            {
                newPlayer.Team += 2;
                mapPlayers.Players.Add(newPlayer.Name, newPlayer);
            }
        }
    }
}
