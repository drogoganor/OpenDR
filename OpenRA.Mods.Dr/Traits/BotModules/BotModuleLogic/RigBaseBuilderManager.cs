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
using System.Linq;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Dr.Traits
{
	public enum BuildingTypeDr { HQ, Building, Defense, Refinery }

	class RigBuildOrder
	{
		public Actor Rig { get; set; }
		public ActorInfo Building { get; set; }
	}

	class RigBaseBuilderManager
	{
		readonly RigBaseBuilderBotModule baseBuilder;
		readonly World world;
		readonly Player player;
		readonly PowerManager playerPower;
		readonly IResourceLayer resourceLayer;
		readonly int minimumExcessPower;

		int waitTicks;
		Actor[] playerBuildings;

		readonly Dictionary<uint, RigBuildOrder> rigBuildOrders = new Dictionary<uint, RigBuildOrder>();

		public RigBaseBuilderManager(RigBaseBuilderBotModule baseBuilder, Player p, PowerManager pm, IResourceLayer rl)
		{
			this.baseBuilder = baseBuilder;
			world = p.World;
			player = p;
			playerPower = pm;
			resourceLayer = rl;
			minimumExcessPower = baseBuilder.Info.MinimumExcessPower;
		}

		public void Tick(IBot bot)
		{
			// Only update once per second or so
			if (--waitTicks > 0)
				return;

			playerBuildings = world.ActorsHavingTrait<Building>().Where(a => a.Owner == player).ToArray();

			// Check list for idle or dead rigs
			var removedOrders = new List<uint>();
			foreach (var order in rigBuildOrders)
			{
				var rig = order.Value.Rig;

				if (!rig.IsInWorld || rig.IsDead || rig.CurrentActivity == null)
				{
					removedOrders.Add(order.Key);
				}
			}

			foreach (var key in removedOrders)
			{
				rigBuildOrders.Remove(key);
			}

			var idleRigs = GetIdleRigs();
			foreach (var rig in idleRigs)
			{
				var builder = rig.TraitsImplementing<BuilderUnit>().FirstEnabledTraitOrDefault();
				var building = ChooseBuildingToBuild(builder);
				if (building != null)
				{
					var type = BuildingTypeDr.Building;
					if (world.Map.Rules.Actors[building.Name].HasTraitInfo<BaseBuildingInfo>())
						type = BuildingTypeDr.HQ;
					else if (world.Map.Rules.Actors[building.Name].HasTraitInfo<AttackBaseInfo>())
						type = BuildingTypeDr.Defense;
					else if (baseBuilder.Info.RefineryTypes.Contains(world.Map.Rules.Actors[building.Name].Name))
						type = BuildingTypeDr.Refinery;

					var location = ChooseBuildLocation(building.Name, true, type);
					if (location == null) continue;

					var order = new Order("BuildUnitPlaceBuilding", player.PlayerActor, Target.FromCell(world, location.Value), false)
					{
						TargetString = building.Name,
						ExtraLocation = location.Value,
						ExtraData = rig.ActorID,
						SuppressVisualFeedback = true
					};

					rigBuildOrders.Add(rig.ActorID, new RigBuildOrder()
					{
						Rig = rig,
						Building = building
					});

					bot.QueueOrder(order);
				}
			}

			// Add a random factor so not every AI produces at the same tick early in the game.
			// Minimum should not be negative as delays in HackyAI could be zero.
			var randomFactor = world.LocalRandom.Next(0, baseBuilder.Info.StructureProductionRandomBonusDelay);

			waitTicks = baseBuilder.Info.StructureProductionActiveDelay + randomFactor;
		}

		public List<Actor> GetIdleRigs()
		{
			var rigs = world.ActorsHavingTrait<BuilderUnit>().Where(a => a.Owner == player)
					.Where(a => !rigBuildOrders.ContainsKey(a.ActorID));

			return rigs.ToList();
		}

		public int NumBuildingsOrdered(ActorInfo building)
		{
			var ordered = rigBuildOrders.Values.Count(o => o.Building.Name == building.Name);
			return ordered;
		}

		public int NumBuildingsBuildingOrOrdered(ActorInfo building)
		{
			var nameComparisons = new HashSet<string>() { building.Name.ToLowerInvariant() };
			var numBuildings = AIUtils.CountBuildingByCommonName(nameComparisons, player);

			return numBuildings + NumBuildingsOrdered(building);
		}

		public int NumBuildingsBuiltBuildingOrOrdered(ActorInfo building)
		{
			var nameComparisons = new HashSet<string>() { building.Name.ToLowerInvariant(), building.Name.ToLowerInvariant().Replace(".constructing", string.Empty) };
			var numBuildings = AIUtils.CountBuildingByCommonName(nameComparisons, player);

			return numBuildings + NumBuildingsOrdered(building);
		}

		ActorInfo GetProducibleBuilding(HashSet<string> actors, IEnumerable<ActorInfo> buildables, Func<ActorInfo, int> orderBy = null)
		{
			var available = buildables.Where(actor =>
			{
				// Are we able to build this?
				if (!actors.Contains(actor.Name))
					return false;

				if (!baseBuilder.Info.BuildingLimits.ContainsKey(actor.Name) && !baseBuilder.Info.BuildingAliases.ContainsKey(actor.Name))
					return true;

				// TODO: This is a DR hack, and the above: && !baseBuilder.Info.BuildingAliases.ContainsKey(actor.Name)
				if (baseBuilder.Info.BuildingAliases.ContainsKey(actor.Name))
				{
					var aliases = baseBuilder.Info.BuildingAliases[actor.Name];
					return playerBuildings.Count(a => aliases.Contains(a.Info.Name)) < baseBuilder.Info.BuildingLimits[actor.Name];
				}

				return playerBuildings.Count(a => a.Info.Name == actor.Name) < baseBuilder.Info.BuildingLimits[actor.Name];
			});

			if (orderBy != null)
				return available.MaxByOrDefault(orderBy);

			return available.RandomOrDefault(world.LocalRandom);
		}

		bool HasSufficientPowerForActor(ActorInfo actorInfo)
		{
			return playerPower == null || (actorInfo.TraitInfos<PowerInfo>().Where(i => i.EnabledByDefault)
				.Sum(p => p.Amount) + playerPower.ExcessPower) >= baseBuilder.Info.MinimumExcessPower;
		}

		ActorInfo ChooseBuildingToBuild(BuilderUnit queue)
		{
			var buildableThings = queue.BuildableItems();

			// This gets used quite a bit, so let's cache it here
			var power = GetProducibleBuilding(baseBuilder.Info.PowerTypes, buildableThings,
				a => a.TraitInfos<PowerInfo>().Where(i => i.EnabledByDefault).Sum(p => p.Amount));
			var hq = GetProducibleBuilding(baseBuilder.Info.HQTypes, buildableThings);
			var water = GetProducibleBuilding(baseBuilder.Info.RefineryTypes, buildableThings);
			var barracks = GetProducibleBuilding(baseBuilder.Info.BarracksTypes, buildableThings);
			var vehicles = GetProducibleBuilding(baseBuilder.Info.VehiclesFactoryTypes, buildableThings);

			// First priority is to get an HQ
			if (hq != null && NumBuildingsBuiltBuildingOrOrdered(hq) == 0)
			{
				AIUtils.BotDebug("AI: {0} decided to build {1}: Priority override (no HQ)", queue.Actor.Owner, hq.Name);
				return hq;
			}

			// Second is to get out of a low power situation
			if (power != null)
			{
				if (NumBuildingsBuiltBuildingOrOrdered(power) == 0)
				{
					AIUtils.BotDebug("AI: {0} decided to build {1}: Priority override (low power)", queue.Actor.Owner, power.Name);
					return power;
				}

				if (playerPower.ExcessPower < minimumExcessPower && NumBuildingsBuildingOrOrdered(power) == 0)
				{
					AIUtils.BotDebug("AI: {0} decided to build {1}: Increase power overhead", queue.Actor.Owner, power.Name);
					return power;
				}
			}

			// Next is to build up a strong economy
			if (water != null)
			{
				if (NumBuildingsBuiltBuildingOrOrdered(water) < 2)
				{
					AIUtils.BotDebug("AI: {0} decided to build {1}: Priority override (not enough water launch pads)", queue.Actor.Owner, water.Name);
					return water;
				}
			}

			if (barracks != null)
			{
				if (NumBuildingsBuiltBuildingOrOrdered(barracks) < 1)
				{
					AIUtils.BotDebug("AI: {0} decided to build {1}: Priority override (not enough training facilities)", queue.Actor.Owner, barracks.Name);
					return barracks;
				}

				if (vehicles != null)
				{
					if (NumBuildingsBuiltBuildingOrOrdered(vehicles) < 1)
					{
						AIUtils.BotDebug("AI: {0} decided to build {1}: Priority override (not enough assembly plants)", queue.Actor.Owner, vehicles.Name);
						return vehicles;
					}
				}

				if (NumBuildingsBuiltBuildingOrOrdered(barracks) < 2)
				{
					AIUtils.BotDebug("AI: {0} decided to build {1}: Priority override (not enough training facilities)", queue.Actor.Owner, barracks.Name);
					return barracks;
				}

				if (vehicles != null)
				{
					if (NumBuildingsBuiltBuildingOrOrdered(vehicles) < 2)
					{
						AIUtils.BotDebug("AI: {0} decided to build {1}: Priority override (not enough assembly plants)", queue.Actor.Owner, vehicles.Name);
						return vehicles;
					}
				}
			}

			// Build everything else
			foreach (var frac in baseBuilder.Info.BuildingFractions.Shuffle(world.LocalRandom))
			{
				var name = frac.Key;

				// Does this building have initial delay, if so have we passed it?
				if (baseBuilder.Info.BuildingDelays != null &&
					baseBuilder.Info.BuildingDelays.ContainsKey(name) &&
					baseBuilder.Info.BuildingDelays[name] > world.WorldTick)
					continue;

				// Can we build this structure?
				if (!buildableThings.Any(b => b.Name == name))
					continue;

				// Do we want to build this structure?
				var count = playerBuildings.Count(a => a.Info.Name == name);
				if (count * 100 > frac.Value * playerBuildings.Length)
					continue;

				// TODO: This is a DR hack
				if (baseBuilder.Info.BuildingAliases.ContainsKey(name))
				{
					var aliases = baseBuilder.Info.BuildingAliases[name];
					if (playerBuildings.Count(a => aliases.Contains(a.Info.Name)) >= baseBuilder.Info.BuildingLimits[name])
						continue;
				}

				if (baseBuilder.Info.BuildingLimits.ContainsKey(name) && baseBuilder.Info.BuildingLimits[name] <= count)
					continue;

				var actor = world.Map.Rules.Actors[name];

				// Do we already have one under construction or ordered?
				if (NumBuildingsBuildingOrOrdered(actor) > 0)
				{
					continue;
				}

				// Will this put us into low power?
				if (playerPower != null && (playerPower.ExcessPower < minimumExcessPower || !HasSufficientPowerForActor(actor)))
				{
					// Try building a power plant instead
					if (power != null && power.TraitInfos<PowerInfo>().Where(i => i.EnabledByDefault).Sum(pi => pi.Amount) > 0)
					{
						if (playerPower.PowerOutageRemainingTicks > 0)
							AIUtils.BotDebug("{0} decided to build {1}: Priority override (is low power)", queue.Actor.Owner, power.Name);
						else
							AIUtils.BotDebug("{0} decided to build {1}: Priority override (would be low power)", queue.Actor.Owner, power.Name);

						return power;
					}
				}

				// Lets build this
				AIUtils.BotDebug("{0} decided to build {1}: Desired is {2} ({3} / {4}); current is {5} / {4}",
					queue.Actor.Owner, name, frac.Value, frac.Value * playerBuildings.Length, playerBuildings.Length, count);
				return actor;
			}

			// Too spammy to keep enabled all the time, but very useful when debugging specific issues.
			// AIUtils.BotDebug("{0} couldn't decide what to build for queue {1}.", queue.Actor.Owner, queue.Info.Group);
			return null;
		}

		CPos? ChooseBuildLocation(string actorType, bool distanceToBaseIsImportant, BuildingTypeDr type)
		{
			var actorInfo = world.Map.Rules.Actors[actorType];
			var bi = actorInfo.TraitInfoOrDefault<BuildingInfo>();
			if (bi == null)
				return null;

			// Find the buildable cell that is closest to pos and centered around center
			Func<CPos, CPos, int, int, CPos?> findPos = (center, target, minRange, maxRange) =>
			{
				var cells = world.Map.FindTilesInAnnulus(center, minRange, maxRange);

				// Sort by distance to target if we have one
				if (center != target)
					cells = cells.OrderBy(c => (c - target).LengthSquared);
				else
					cells = cells.Shuffle(world.LocalRandom);

				foreach (var cell in cells)
				{
					if (!world.CanPlaceBuilding(cell, actorInfo, bi, null))
						continue;

					if (distanceToBaseIsImportant && !bi.IsCloseEnoughToBase(world, player, actorInfo, cell))
						continue;

					return cell;
				}

				return null;
			};

			var baseCenter = player.HomeLocation;

			switch (type)
			{
				case BuildingTypeDr.HQ:

					return baseCenter;

				case BuildingTypeDr.Defense:

					// Build near the closest enemy structure
					var closestEnemy = world.ActorsHavingTrait<Building>().Where(a => !a.Disposed && !player.IsAlliedWith(a.Owner))
						.ClosestTo(world.Map.CenterOfCell(baseBuilder.DefenseCenter));

					var targetCell = closestEnemy != null ? closestEnemy.Location : baseCenter;
					return findPos(baseBuilder.DefenseCenter, targetCell, baseBuilder.Info.MinimumDefenseRadius, baseBuilder.Info.MaximumDefenseRadius);

				case BuildingTypeDr.Refinery:

					// Try and place the refinery near a resource field
					var nearbyResources = world.Map.FindTilesInAnnulus(baseCenter, baseBuilder.Info.MinBaseRadius, baseBuilder.Info.MaxBaseRadius)
						.Where(a => resourceLayer.GetResource(a).Type != null)
						.Shuffle(world.LocalRandom).Take(baseBuilder.Info.MaxResourceCellsToCheck);

					foreach (var r in nearbyResources)
					{
						var found = findPos(baseCenter, r, baseBuilder.Info.MinBaseRadius, baseBuilder.Info.MaxBaseRadius);
						if (found != null)
							return found;
					}

					// Try and find a free spot somewhere else in the base
					return findPos(baseCenter, baseCenter, baseBuilder.Info.MinBaseRadius, baseBuilder.Info.MaxBaseRadius);

				case BuildingTypeDr.Building:
					return findPos(baseCenter, baseCenter, baseBuilder.Info.MinBaseRadius,
						distanceToBaseIsImportant ? baseBuilder.Info.MaxBaseRadius : world.Map.Grid.MaximumTileSearchRange);
			}

			// Can't find a build location
			return null;
		}
	}
}
