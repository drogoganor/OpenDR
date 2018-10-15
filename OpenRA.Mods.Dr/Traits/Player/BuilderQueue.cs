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
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
    [Desc("Attach this to an actor (usually a building) to let it construct buildings.",
		"If one builds another actor of this type, he will get a separate queue to create two actors",
		"at the same time. Will only work together with the Production: trait.")]
	public class BuilderQueueInfo : ITraitInfo
	{
		[FieldLoader.Require]
		[Desc("What kind of production will be added (e.g. Building, Infantry, Vehicle, ...)")]
		public readonly string Type = null;

		[Desc("Group queues from separate buildings together into the same tab.")]
		public readonly string Group = null;

		[Desc("Only enable this queue for certain factions.")]
		public readonly HashSet<string> Factions = new HashSet<string>();

		[Desc("Should the prerequisite remain enabled if the owner changes?")]
		public readonly bool Sticky = true;

		[Desc("Should right clicking on the icon instantly cancel the production instead of putting it on hold?")]
		public readonly bool DisallowPaused = false;

		[Desc("This percentage value is multiplied with actor cost to translate into build time (lower means faster).")]
		public readonly int BuildDurationModifier = 100;

		[Desc("Maximum number of a single actor type that can be queued (0 = infinite).")]
		public readonly int ItemLimit = 999;

		[Desc("Maximum number of items that can be queued across all actor types (0 = infinite).")]
		public readonly int QueueLimit = 0;

		[Desc("The build time is multiplied with this value on low power.")]
		public readonly int LowPowerSlowdown = 3;

		[Desc("Notification played when production is complete.",
			"The filename of the audio is defined per faction in notifications.yaml.")]
		public readonly string ReadyAudio = "UnitReady";

		[Desc("Notification played when you can't train another actor",
			"when the build limit exceeded or the exit is jammed.",
			"The filename of the audio is defined per faction in notifications.yaml.")]
		public readonly string BlockedAudio = "NoBuild";

		[Desc("Notification played when you can't queue another actor",
			"when the queue length limit is exceeded.",
			"The filename of the audio is defined per faction in notifications.yaml.")]
		public readonly string LimitedAudio = null;

		[Desc("Notification played when user clicks on the build palette icon.",
			"The filename of the audio is defined per faction in notifications.yaml.")]
		public readonly string QueuedAudio = "Training";

		[Desc("Notification played when player right-clicks on the build palette icon.",
			"The filename of the audio is defined per faction in notifications.yaml.")]
		public readonly string OnHoldAudio = "OnHold";

		[Desc("Notification played when player right-clicks on a build palette icon that is already on hold.",
			"The filename of the audio is defined per faction in notifications.yaml.")]
		public readonly string CancelledAudio = "Cancelled";

		public virtual object Create(ActorInitializer init) { return new BuilderQueue(init, init.Self, this); }

		public void RulesetLoaded(Ruleset rules, ActorInfo ai)
		{
			if (LowPowerSlowdown <= 0)
				throw new YamlException("Production queue must have LowPowerSlowdown of at least 1.");
		}
	}

    // Like ProductionQueue but simply ticks all items every tick and does not deplete credits.
	public class BuilderQueue : IResolveOrder, ITick, ITechTreeElement, INotifyOwnerChanged, INotifyKilled, INotifySold, ISync, INotifyTransform, INotifyCreated
	{
		public readonly BuilderQueueInfo Info;
		readonly Actor self;

		// A list of things we could possibly build
		readonly Dictionary<ActorInfo, ProductionState> producible = new Dictionary<ActorInfo, ProductionState>();
		readonly List<BuilderItem> queue = new List<BuilderItem>();
		readonly IEnumerable<ActorInfo> allProducibles;
		readonly IEnumerable<ActorInfo> buildableProducibles;

		Production[] productionTraits;

		// Will change if the owner changes
		protected DeveloperMode developerMode;

		public Actor Actor { get { return self; } }

		[Sync] public int QueueLength { get { return queue.Count; } }
		[Sync] public int CurrentRemainingTime { get { return QueueLength == 0 ? 0 : queue[0].RemainingTime; } }
		[Sync] public bool CurrentDone { get { return QueueLength != 0 && queue[0].Done; } }
		[Sync] public bool Enabled { get; protected set; }

		public string Faction { get; private set; }
		[Sync] public bool IsValidFaction { get; private set; }

		public BuilderQueue(ActorInitializer init, Actor playerActor, BuilderQueueInfo info)
		{
			self = init.Self;
			Info = info;
			developerMode = playerActor.Trait<DeveloperMode>();

			Faction = init.Contains<FactionInit>() ? init.Get<FactionInit, string>() : self.Owner.Faction.InternalName;
			IsValidFaction = !info.Factions.Any() || info.Factions.Contains(Faction);
			Enabled = IsValidFaction;

			CacheProducibles(playerActor);
			allProducibles = producible.Where(a => a.Value.Buildable || a.Value.Visible).Select(a => a.Key);
			buildableProducibles = producible.Where(a => a.Value.Buildable).Select(a => a.Key);
		}

		void INotifyCreated.Created(Actor self)
		{
			// Special case handling is required for the Player actor.
			// Created is called before Player.PlayerActor is assigned,
			// so we must query other player traits from self, knowing that
			// it refers to the same actor as self.Owner.PlayerActor
			productionTraits = self.TraitsImplementing<Production>().ToArray();
		}

		protected void ClearQueue()
		{
			if (queue.Count == 0)
				return;

			queue.Clear();
		}

		void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
		{
			ClearQueue();

			developerMode = newOwner.PlayerActor.Trait<DeveloperMode>();

			if (!Info.Sticky)
			{
				Faction = self.Owner.Faction.InternalName;
				IsValidFaction = !Info.Factions.Any() || Info.Factions.Contains(Faction);
			}

			// Regenerate the producibles and tech tree state
			oldOwner.PlayerActor.Trait<TechTree>().Remove(this);
			CacheProducibles(newOwner.PlayerActor);
			newOwner.PlayerActor.Trait<TechTree>().Update();
		}

		void INotifyKilled.Killed(Actor killed, AttackInfo e) { if (killed == self) { ClearQueue(); Enabled = false; } }
		void INotifySold.Selling(Actor self) { ClearQueue(); Enabled = false; }
		void INotifySold.Sold(Actor self) { }

		void INotifyTransform.BeforeTransform(Actor self) { ClearQueue(); Enabled = false; }
		void INotifyTransform.OnTransform(Actor self) { }
		void INotifyTransform.AfterTransform(Actor self) { }

		void CacheProducibles(Actor playerActor)
		{
			producible.Clear();
			if (!Enabled)
				return;

			var ttc = playerActor.Trait<TechTree>();

			foreach (var a in AllBuildables(Info.Type))
			{
				var bi = a.TraitInfo<BuildableInfo>();

				producible.Add(a, new ProductionState());
				ttc.Add(a.Name, bi.Prerequisites, bi.BuildLimit, this);
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
			producible[self.World.Map.Rules.Actors[key]].Buildable = true;
		}

		public void PrerequisitesUnavailable(string key)
		{
			producible[self.World.Map.Rules.Actors[key]].Buildable = false;
		}

		public void PrerequisitesItemHidden(string key)
		{
			producible[self.World.Map.Rules.Actors[key]].Visible = false;
		}

		public void PrerequisitesItemVisible(string key)
		{
			producible[self.World.Map.Rules.Actors[key]].Visible = true;
		}

		public virtual IEnumerable<BuilderItem> AllQueued()
		{
			return queue;
		}

		public virtual IEnumerable<ActorInfo> AllItems()
		{
			if (developerMode.AllTech)
				return producible.Keys;

			return allProducibles;
		}

		public virtual IEnumerable<ActorInfo> BuildableItems()
		{
			if (!Enabled)
				return Enumerable.Empty<ActorInfo>();

			if (developerMode.AllTech)
				return producible.Keys;

			return buildableProducibles;
		}

		public bool CanBuild(ActorInfo actor)
		{
			ProductionState ps;
			if (!producible.TryGetValue(actor, out ps))
				return false;

            return ps.Buildable || developerMode.AllTech;
		}

		void ITick.Tick(Actor self)
		{
			Tick(self);
		}

		protected virtual void Tick(Actor self)
		{
			Enabled = IsValidFaction;

			TickInner(self, true);
		}

		protected virtual void TickInner(Actor self, bool allProductionPaused)
		{
			if (queue.Count > 0)
            {
                for (int i = 0; i < queue.Count; i++)
                {
                    queue[i].Tick();
                }
            }
		}

		public bool CanQueue(ActorInfo actor, out string notificationAudio)
		{
			notificationAudio = Info.BlockedAudio;

			var bi = actor.TraitInfoOrDefault<BuildableInfo>();
			if (bi == null)
				return false;

			if (!developerMode.AllTech)
			{
			    if (Info.QueueLimit > 0 && queue.Count >= Info.QueueLimit)
			    {
				    notificationAudio = Info.LimitedAudio;
				    return false;
			    }

			    var queueCount = queue.Count(i => i.Item == actor.Name);
			    if (Info.ItemLimit > 0 && queueCount >= Info.ItemLimit)
			    {
				    notificationAudio = Info.LimitedAudio;
				    return false;
			    }

			    if (bi.BuildLimit > 0)
			    {
				    var owned = self.Owner.World.ActorsHavingTrait<Buildable>()
					    .Count(a => a.Info.Name == actor.Name && a.Owner == self.Owner);
				    if (queueCount + owned >= bi.BuildLimit)
					    return false;
			    }
			}

			notificationAudio = Info.QueuedAudio;
			return true;
		}

		public void ResolveOrder(Actor self, Order order)
		{
			if (!Enabled)
				return;

			var rules = self.World.Map.Rules;
			switch (order.OrderString)
			{
				case "StartProduction":
					var unit = rules.Actors[order.TargetString];
					var bi = unit.TraitInfo<BuildableInfo>();

					// Not built by this queue
					if (!bi.Queue.Contains(Info.Type))
						return;

					// You can't build that
					if (BuildableItems().All(b => b.Name != order.TargetString))
						return;

					var time = GetBuildTime(unit, bi);

					var hasPlayedSound = false;
					BeginProduction(new BuilderItem(this, order.TargetString, () => self.World.AddFrameEndTask(_ =>
					{
						var isBuilding = unit.HasTraitInfo<BuildingInfo>();

						if (isBuilding && !hasPlayedSound)
							hasPlayedSound = Game.Sound.PlayNotification(rules, self.Owner, "Speech", Info.ReadyAudio, self.Owner.Faction.InternalName);
						else if (!isBuilding)
						{
							if (BuildUnit(unit))
								Game.Sound.PlayNotification(rules, self.Owner, "Speech", Info.ReadyAudio, self.Owner.Faction.InternalName);
							else if (!hasPlayedSound && time > 0)
								hasPlayedSound = Game.Sound.PlayNotification(rules, self.Owner, "Speech", Info.BlockedAudio, self.Owner.Faction.InternalName);
						}
					})));

					break;
			}
		}

		public virtual int GetBuildTime(ActorInfo unit, BuildableInfo bi)
		{
			if (developerMode.FastBuild)
				return 0;

			var time = bi.BuildDuration;
			if (time == -1)
			{
				time = 0;
			}

			time = time * bi.BuildDurationModifier * Info.BuildDurationModifier / 10000;
			return time;
		}

		protected void CancelProduction(string itemName)
		{
            CancelProductionInner(itemName);
		}

		bool CancelProductionInner(string itemName)
		{
			var lastIndex = queue.FindLastIndex(a => a.Item == itemName);

			if (lastIndex > 0)
				queue.RemoveAt(lastIndex);
			else if (lastIndex == 0)
			{
				var item = queue[0];
			}
			else
				return false;

			return true;
		}

		public void BeginProduction(BuilderItem item)
        {
            var rules = self.World.Map.Rules;
            item.OnComplete = () =>
            {
                Game.Sound.PlayNotification(rules, self.Owner, "Speech", Info.ReadyAudio, self.Owner.Faction.InternalName);
            };

            queue.Add(item);
		}

		public void EndProduction(BuilderItem item)
		{
			queue.Remove(item);
		}

		// Returns the actor/trait that is most likely (but not necessarily guaranteed) to produce something in this queue
		public virtual TraitPair<Production> MostLikelyProducer()
		{
			var traits = productionTraits.Where(p => !p.IsTraitDisabled && p.Info.Produces.Contains(Info.Type));
			var unpaused = traits.FirstOrDefault(a => !a.IsTraitPaused);
			return new TraitPair<Production>(self, unpaused != null ? unpaused : traits.FirstOrDefault());
		}

		// Builds a unit from the actor that holds this queue (1 queue per building)
		// Returns false if the unit can't be built
		protected virtual bool BuildUnit(ActorInfo unit)
		{
			var mostLikelyProducerTrait = MostLikelyProducer().Trait;

			// Cannot produce if I'm dead or trait is disabled
			if (!self.IsInWorld || self.IsDead || mostLikelyProducerTrait == null)
			{
				CancelProduction(unit.Name);
				return false;
			}

			var inits = new TypeDictionary
			{
				new OwnerInit(self.Owner),
				new FactionInit(BuildableInfo.GetInitialFaction(unit, Faction))
			};

			var bi = unit.TraitInfo<BuildableInfo>();
            var type = developerMode.AllTech ? Info.Type : (bi.BuildAtProductionType ?? Info.Type);

            if (!mostLikelyProducerTrait.IsTraitPaused && mostLikelyProducerTrait.Produce(self, unit, type, inits))
			{
                CancelProduction(unit.Name);
				return true;
			}

			return false;
		}
	}
}
