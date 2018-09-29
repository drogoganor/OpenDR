using System;
using System.Collections.Generic;
using System.Linq;
using Eluant;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Dr.Traits.Production
{
    public class SelfConstructingInfo : WithMakeAnimationInfo, ITraitInfo, Requires<ConditionManagerInfo>
    {
        [Desc("Number of make sequences.")]
        public readonly int Steps = 3;

        [Desc("Actor to turn into once build is complete.")]
        public readonly string Becomes;

        public new object Create(ActorInitializer init) { return new SelfConstructing(init, this); }
    }

    public class SelfConstructing : WithMakeAnimation, ITick, INotifyRemovedFromWorld, INotifyCreated
    {
        public readonly SelfConstructingInfo Info;

        private readonly WithSpriteBody wsb;

        private readonly ConditionManager conditionManager;
        private int token = ConditionManager.InvalidConditionToken;

        private BuilderItem productionItem;

        private List<int> healthSteps;
        private int healthStep = 0;
        private Health health;
        private World world;

        public SelfConstructing(ActorInitializer init, SelfConstructingInfo info) : base(init, info)
        {
            world = init.World;
            Info = info;
            wsb = init.Self.Trait<WithSpriteBody>();
            conditionManager = init.Self.Trait<ConditionManager>();

            if (!string.IsNullOrEmpty(Info.Condition) && token == ConditionManager.InvalidConditionToken)
                token = conditionManager.GrantCondition(init.Self, Info.Condition);

            if (init.Contains<PlaceBuildingInit>())
            {
                var productionQueue = init.Self.Owner.PlayerActor.TraitsImplementing<BuilderQueue>().First(q => q.AllItems().Contains(init.Self.Info));
                var valued = init.Self.Info.TraitInfoOrDefault<ValuedInfo>();
                productionItem = new BuilderItem(productionQueue, init.Self.Info.Name, null);
                productionQueue.BeginProduction(productionItem);

                wsb.CancelCustomAnimation(init.Self);
                wsb.PlayCustomAnimationRepeating(init.Self, "building");
            }
        }

        void INotifyCreated.Created(Actor self)
        {
            if (productionItem == null)
                return;

            health = self.Trait<Health>();

            healthSteps = new List<int>();
            for (var i = 0; i <= Info.Steps; i++)
                healthSteps.Add(health.MaxHP * (i + 1) / (Info.Steps + 1));

            health.InflictDamage(self, self, new Damage(health.MaxHP - healthSteps[0]), true);
        }

        private void OnComplete(Actor self)
        {
            var world = self.World;
            var rules = world.Map.Rules;
            if (token != ConditionManager.InvalidConditionToken)
                token = conditionManager.RevokeCondition(self, token);

            var building = self.TraitOrDefault<Building>();

            var actorName = Info.Becomes.ToLowerInvariant();
            var builtBuildingDef = rules.Actors[actorName];
            var newActor = CreateActor(self, actorName, true);

            world.Remove(self);
            self.Dispose();

            Game.Sound.PlayNotification(self.World.Map.Rules, self.Owner, "Speech", "ConstructionComplete", self.Owner.Faction.InternalName);

            // building.NotifyBuildingComplete(newActor);
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
                new PlaceBuildingInit()
            });

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