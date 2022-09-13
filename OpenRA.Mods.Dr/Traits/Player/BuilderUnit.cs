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

using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Dr.Traits
{
	[Desc("Attach this to an actor (a builder unit) to let it select buildings to construct.")]
	public class BuilderUnitInfo : TraitInfo
	{
		[FieldLoader.Require]
		[Desc("What kind of production will be added (e.g. Building, Infantry, Vehicle, ...)")]
		public readonly string Type = null;

		[Desc("Group queues from separate buildings together into the same tab.")]
		public readonly string Group = null;

		[Desc("Only enable this queue for certain factions.")]
		public readonly HashSet<string> Factions = new HashSet<string>();

		[Desc("Notification played when you can't train another actor",
			"when the build limit exceeded or the exit is jammed.",
			"The filename of the audio is defined per faction in notifications.yaml.")]
		public readonly string BlockedAudio = "NoBuild";

		[NotificationReference("Speech")]
		[Desc("Notification played when you can't place a building.",
			"Overrides PlaceBuilding.CannotPlaceNotification for this queue.",
			"The filename of the audio is defined per faction in notifications.yaml.")]
		public readonly string CannotPlaceAudio = null;

		public override object Create(ActorInitializer init) { return new BuilderUnit(init, init.Self.Owner.PlayerActor, this); }

		public void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
		}
	}

	// Copied from ProductionQueue
	public class BuilderUnit : IResolveOrder, ITick, ITechTreeElement, INotifyKilled, INotifySold, ISync, INotifyTransform, INotifyCreated
	{
		public readonly BuilderUnitInfo Info;
		readonly Actor self;

		// A list of things we could possibly build
		protected readonly Dictionary<ActorInfo, ProductionState> Producible = new Dictionary<ActorInfo, ProductionState>();
		readonly IEnumerable<ActorInfo> allProducibles;
		readonly IEnumerable<ActorInfo> buildableProducibles;

		protected BuilderUnit[] productionTraits;

		protected DeveloperMode developerMode;
		protected TechTree techTree;

		public Actor Actor { get { return self; } }

		[Sync]
		public bool Enabled { get; protected set; }

		public string Faction { get; private set; }
		[Sync]
		public bool IsValidFaction { get; private set; }

		public BuilderUnit(ActorInitializer init, Actor playerActor, BuilderUnitInfo info)
		{
			self = init.Self;
			Info = info;

			Faction = init.GetValue<FactionInit, string>(self.Owner.Faction.InternalName);
			IsValidFaction = !info.Factions.Any() || info.Factions.Contains(Faction);
			Enabled = IsValidFaction;

			allProducibles = Producible.Where(a => a.Value.Buildable || a.Value.Visible).Select(a => a.Key);
			buildableProducibles = Producible.Where(a => a.Value.Buildable).Select(a => a.Key);
		}

		void INotifyCreated.Created(Actor self)
		{
			developerMode = self.Owner.PlayerActor.Trait<DeveloperMode>();
			techTree = self.Owner.PlayerActor.Trait<TechTree>();

			productionTraits = self.TraitsImplementing<BuilderUnit>().ToArray();
			CacheProducibles(self.Owner.PlayerActor);
		}

		void INotifyKilled.Killed(Actor killed, AttackInfo e) { if (killed == self) { Enabled = false; } }
		void INotifySold.Selling(Actor self) { Enabled = false; }
		void INotifySold.Sold(Actor self) { }

		void INotifyTransform.BeforeTransform(Actor self) { Enabled = false; }
		void INotifyTransform.OnTransform(Actor self) { }
		void INotifyTransform.AfterTransform(Actor self) { }

		void CacheProducibles(Actor playerActor)
		{
			Producible.Clear();
			if (!Enabled)
				return;

			foreach (var a in AllBuildables(Info.Type))
			{
				var bi = a.TraitInfo<BuildableInfo>();

				Producible.Add(a, new ProductionState());
				techTree.Add(a.Name, bi.Prerequisites, bi.BuildLimit, this);
			}
		}

		IEnumerable<ActorInfo> AllBuildables(string category)
		{
			return self.World.Map.Rules.Actors.Values
				.Where(x =>
					x.Name[0] != '^' &&
					x.HasTraitInfo<BuildableInfo>() &&
					x.TraitInfo<BuildableInfo>().Queue.Contains(category));
		}

		public void PrerequisitesAvailable(string key)
		{
			Producible[self.World.Map.Rules.Actors[key]].Buildable = true;
		}

		public void PrerequisitesUnavailable(string key)
		{
			Producible[self.World.Map.Rules.Actors[key]].Buildable = false;
		}

		public void PrerequisitesItemHidden(string key)
		{
			Producible[self.World.Map.Rules.Actors[key]].Visible = false;
		}

		public void PrerequisitesItemVisible(string key)
		{
			Producible[self.World.Map.Rules.Actors[key]].Visible = true;
		}

		public virtual IEnumerable<ActorInfo> AllItems()
		{
			if (developerMode.AllTech)
				return Producible.Keys;

			return allProducibles;
		}

		public virtual IEnumerable<ActorInfo> BuildableItems()
		{
			if (!Enabled)
				return Enumerable.Empty<ActorInfo>();
			if (developerMode.AllTech)
				return Producible.Keys;

			return buildableProducibles;
		}

		public bool CanBuild(ActorInfo actor)
		{
			ProductionState ps;
			if (!Producible.TryGetValue(actor, out ps))
				return false;

			return ps.Buildable || developerMode.AllTech;
		}

		void ITick.Tick(Actor self)
		{
			Tick(self);
		}

		protected virtual void Tick(Actor self)
		{
		}

		protected virtual void TickInner(Actor self, bool allProductionPaused)
		{
		}

		public void ResolveOrder(Actor self, Order order)
		{
			if (!Enabled)
				return;
		}

		// Returns the actor/trait that is most likely (but not necessarily guaranteed) to produce something in this queue
		public virtual TraitPair<BuilderUnit> MostLikelyProducer()
		{
			return new TraitPair<BuilderUnit>(self, productionTraits.FirstOrDefault());
		}
	}
}
