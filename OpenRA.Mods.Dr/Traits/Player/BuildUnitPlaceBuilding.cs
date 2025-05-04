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

using System.Linq;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Dr.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.Dr.Traits
{
	[TraitLocation(SystemActors.Player)]
	[Desc("Allows the player to execute build orders.", " Attach this to the player actor.")]
	public class BuildUnitPlaceBuildingInfo : TraitInfo
	{
		[Desc("Play NewOptionsNotification this many ticks after building placement.")]
		public readonly int NewOptionsNotificationDelay = 10;

		[NotificationReference("Speech")]
		[Desc("Speech notification to play after building placement if new construction options are available.")]
		public readonly string NewOptionsNotification = null;

		[Desc("Text notification to display after building placement if new construction options are available.")]
		public readonly string NewOptionsTextNotification = null;

		[NotificationReference("Speech")]
		[Desc("Speech notification to play if building placement is not possible.")]
		public readonly string CannotPlaceNotification = null;

		[Desc("Text notification to display if building placement is not possible.")]
		public readonly string CannotPlaceTextNotification = null;

		[Desc("Hotkey to toggle between PlaceBuildingVariants when placing a structure.")]
		public readonly HotkeyReference ToggleVariantKey = new();

		public override object Create(ActorInitializer init) { return new BuildUnitPlaceBuilding(this); }
	}

	// Copied from PlaceBuilding
	// Instead of placing the building, instead issues orders to the builder unit to proceed to the location and build it on site.
	public class BuildUnitPlaceBuilding : IResolveOrder, ITick
	{
		readonly BuildUnitPlaceBuildingInfo info;
		bool triggerNotification;
		int tick;

		public BuildUnitPlaceBuilding(BuildUnitPlaceBuildingInfo info)
		{
			this.info = info;
		}

		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			var os = order.OrderString;
			if (os != "BuildUnitPlaceBuilding")
				return;

			self.World.AddFrameEndTask(w =>
			{
				var targetActor = w.GetActorById(order.ExtraData);

				if (targetActor == null || targetActor.IsDead)
					return;

				var actorInfo = self.World.Map.Rules.Actors[order.TargetString];
				var queue = targetActor.TraitsImplementing<BuilderUnit>()
					.FirstOrDefault(q => q.CanBuild(actorInfo));

				if (queue == null)
					return;

				var producer = queue.MostLikelyProducer();
				var faction = producer.Trait != null ? producer.Trait.Faction : self.Owner.Faction.InternalName;
				var buildingInfo = actorInfo.TraitInfo<BuildingInfo>();

				var buildableInfo = actorInfo.TraitInfoOrDefault<BuildableInfo>();
				if (buildableInfo != null && buildableInfo.ForceFaction != null)
					faction = buildableInfo.ForceFaction;

				if (!self.World.CanPlaceBuilding(order.ExtraLocation, actorInfo, buildingInfo, targetActor))
					return;

				if (!order.Queued)
					targetActor.CancelActivity();

				var cell = self.World.Map.Clamp(self.World.Map.CellContaining(order.Target.CenterPosition));

				// Make the actor move to the location
				var moveActivity = new Move(targetActor, cell, WDist.FromCells(1), null, true, Primitives.Color.Green);
				var buildActivity = new BuildOnSite(w, order, faction, buildingInfo);

				targetActor.QueueActivity(moveActivity);
				targetActor.ShowTargetLines();
				targetActor.QueueActivity(buildActivity);
			});
		}

		void ITick.Tick(Actor self)
		{
			if (!triggerNotification)
				return;

			if (tick++ >= info.NewOptionsNotificationDelay)
				PlayNotification(self);
		}

		void PlayNotification(Actor self)
		{
			Game.Sound.PlayNotification(self.World.Map.Rules, self.Owner, "Speech", info.NewOptionsNotification, self.Owner.Faction.InternalName);
			TextNotificationsManager.AddTransientLine(self.Owner, info.NewOptionsTextNotification);

			triggerNotification = false;
			tick = 0;
		}

		/*
		static int GetNumBuildables(Player p)
		{
			// This only matters for local players.
			if (p != p.World.LocalPlayer)
				return 0;

			return p.World.ActorsWithTrait<ProductionQueue>()
				.Where(a => a.Actor.Owner == p)
				.SelectMany(a => a.Trait.BuildableItems()).Distinct().Count();
		}
		*/
	}
}
