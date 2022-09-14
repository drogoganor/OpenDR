using System;
using System.Collections.Generic;
using System.Linq;
using Eluant;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Dr.Traits.Production
{
	public class SelfConstructingInfo : TraitInfo, Requires<WithSpriteBodyInfo>
	{
		[SequenceReference]
		[Desc("Sequence name to use.")]
		public readonly string Sequence = "make";

		[GrantedConditionReference]
		[Desc("The condition to grant to self while the make animation is playing.")]
		public readonly string Condition = null;

		[Desc("Number of make sequences.")]
		public readonly int Steps = 3;

		[Desc("Actor to turn into once build is complete.")]
		public readonly string Becomes;

		public override object Create(ActorInitializer init) { return new SelfConstructing(init, this); }
	}

	public class SelfConstructing : ITick, INotifyRemovedFromWorld, INotifyCreated
	{
		public readonly SelfConstructingInfo Info;

		readonly WithSpriteBody wsb;

		int token = Actor.InvalidConditionToken;

		ProductionItem productionItem;

		List<int> healthSteps;
		int healthStep = 0;
		Health health;

		public SelfConstructing(ActorInitializer init, SelfConstructingInfo info)
		{
			Info = info;
			wsb = init.Self.Trait<WithSpriteBody>();

			if (token == Actor.InvalidConditionToken)
				token = init.Self.GrantCondition(info.Condition);
		}

		void INotifyCreated.Created(Actor self)
		{
			var valued = self.Info.TraitInfoOrDefault<ValuedInfo>();
			var cost = valued != null ? valued.Cost : 0;
			var pm = self.Owner.PlayerActor.TraitOrDefault<PowerManager>();

			var productionQueue = self.TraitsImplementing<BuilderQueue>().First(q => q.AllItems().Contains(self.Info));
			productionItem = new ProductionItem(productionQueue, self.Info.Name, cost, pm, null);
			productionQueue.BeginProduction(productionItem);

			if (productionItem == null)
				return;

			health = self.Trait<Health>();

			healthSteps = new List<int>();
			for (var i = 0; i <= Info.Steps; i++)
				healthSteps.Add(health.MaxHP * (i + 1) / (Info.Steps + 1));

			health.InflictDamage(self, self, new Damage(health.MaxHP - healthSteps[0]), true);

			wsb.CancelCustomAnimation(self);
			wsb.PlayCustomAnimationRepeating(self, "building");
		}

		void OnComplete(Actor self)
		{
			var world = self.World;
			var rules = world.Map.Rules;
			if (token != Actor.InvalidConditionToken)
				token = self.RevokeCondition(token);

			var actorName = Info.Becomes.ToLowerInvariant();
			var builtBuildingDef = rules.Actors[actorName];
			CreateActor(self, actorName, true);

			world.Remove(self);
			self.Dispose();

			Game.Sound.PlayNotification(self.World.Map.Rules, self.Owner, "Speech", "ConstructionComplete", self.Owner.Faction.InternalName);
		}

		Actor CreateActor(Actor self, string actorType, bool addToWorld)
		{
			Player owner = self.Owner;
			ActorInfo ai;
			if (!owner.World.Map.Rules.Actors.TryGetValue(actorType, out ai))
				throw new LuaException("Unknown actor type '{0}'".F(actorType));

			var actor = self.World.CreateActor(addToWorld, actorType, new TypeDictionary
			{
				new LocationInit(self.Location),
				new OwnerInit(owner),
				new PlaceBuildingInit(),
			});

			// Copy rally point
			var oldRallyPoint = self.TraitOrDefault<RallyPoint>();
			if (oldRallyPoint != null)
			{
				var oldRallyPointTarget = Target.FromCell(self.World, oldRallyPoint.Path[0]);
				self.Owner.World.IssueOrder(new Order("SetRallyPoint", actor, oldRallyPointTarget, false));
			}

			return actor;
		}

		void ITick.Tick(Actor self)
		{
			if (productionItem == null)
				return;

			if (productionItem.Done)
			{
				productionItem.Queue.EndProduction(productionItem);
				productionItem = null;
				OnComplete(self);
				return;
			}

			var progress = Math.Min(Info.Steps * (productionItem.TotalTime - productionItem.RemainingTime) / Math.Max(1, productionItem.TotalTime), Info.Steps - 1);
			if (healthStep != progress)
			{
				healthStep = progress;
				health.InflictDamage(self, self, new Damage(healthSteps[Math.Max(0, progress - 1)] - healthSteps[progress]), true);
			}
		}

		void INotifyRemovedFromWorld.RemovedFromWorld(Actor self)
		{
			if (productionItem != null)
				productionItem.Queue.EndProduction(productionItem);
		}
	}
}
