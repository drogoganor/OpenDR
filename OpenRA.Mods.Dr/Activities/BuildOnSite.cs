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

using System.Collections.Generic;
using System.Linq;
using OpenRA.Activities;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Dr.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Dr.Activities
{
	// Activity to move to a location and construct a building there.
	public class BuildOnSite : Activity
	{
		readonly World world;
		readonly CPos target;
		readonly IMove move;
		readonly Order order;
		readonly TraitPair<BuilderUnit> producer;
		readonly string faction;
		readonly BuildingInfo buildingInfo;
		readonly PlayerResources playerResources;
		readonly ActorInfo buildingActor;
        bool buildingPlaced = false;

		public BuildOnSite(World world, Actor self, Order order, TraitPair<BuilderUnit> producer, string faction, BuildingInfo buildingInfo)
		{
			move = self.Trait<IMove>();
			this.buildingInfo = buildingInfo;
			this.world = world;
			this.order = order;
			this.producer = producer;
			this.faction = faction;
			target = order.TargetLocation;
			playerResources = order.Player.PlayerActor.Trait<PlayerResources>();
			buildingActor = world.Map.Rules.Actors.FirstOrDefault(x => x.Key == order.TargetString).Value;
		}

		public override Activity Tick(Actor self)
		{
			if (IsCanceled)
				return NextActivity;

			if (self.Location == target)
			{
				if (!world.CanPlaceBuilding(target, buildingActor, buildingInfo, self))
				{
					// Try clear the area
					foreach (var order in ClearBlockersOrders(self, world, target))
						world.IssueOrder(order);

					Game.Sound.PlayNotification(world.Map.Rules, self.Owner, "Speech", "BuildingCannotPlaceAudio", faction);
					return NextActivity;
				}

				// Try deduct cost
				var cost = buildingActor.TraitInfo<ValuedInfo>().Cost;
				if (!playerResources.TakeCash(cost, true))
				{
					Game.Sound.PlayNotification(world.Map.Rules, self.Owner, "Speech", "InsufficientFunds", faction);
					return NextActivity;
				}

				self.World.AddFrameEndTask(w =>
				{
					var building = w.CreateActor(true, order.TargetString, new TypeDictionary
						{
							new LocationInit(order.TargetLocation),
							new OwnerInit(order.Player),
							new FactionInit(faction),
                            new PlaceBuildingInit()
                        });

					Game.Sound.PlayNotification(self.World.Map.Rules, order.Player, "Speech", "Building", faction);
                });

                buildingPlaced = true;

                return new RemoveSelf();
            }

            return ActivityUtils.SequenceActivities(
				move.MoveTo(target, 2),
				this);
		}

		// Copied from PlaceBuildingOrderGenerator, triplicated in BuildOnSite and BuilderUnitBuildingOrderGenerator
		IEnumerable<Order> ClearBlockersOrders(Actor ownerActor, World world, CPos topLeft)
		{
			var allTiles = buildingInfo.Tiles(topLeft).ToArray();
			var neightborTiles = Util.ExpandFootprint(allTiles, true).Except(allTiles)
				.Where(world.Map.Contains).ToList();

			var blockers = allTiles.SelectMany(world.ActorMap.GetActorsAt)
				.Where(a => a.Owner == ownerActor.Owner && a.IsIdle && a != ownerActor)
				.Select(a => new TraitPair<Mobile>(a, a.TraitOrDefault<Mobile>()));

			foreach (var blocker in blockers.Where(x => x.Trait != null))
			{
				var availableCells = neightborTiles.Where(t => blocker.Trait.CanEnterCell(t)).ToList();
				if (availableCells.Count == 0)
					continue;

				yield return new Order("Move", blocker.Actor, Target.FromCell(world, blocker.Actor.ClosestCell(availableCells)), false)
				{
					SuppressVisualFeedback = true
				};
			}
		}
	}
}