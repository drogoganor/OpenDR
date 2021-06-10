using System;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Dr.Traits
{
	public class AcceptsFreshWaterInfo : TraitInfo
	{
		[Desc("Maximum holding capacity of the water refinery before it is sold automatically.")]
		public readonly int WaterCapacity = 3000;

		[Desc("Maximum holding capacity of taelon before it is discarded.")]
		public readonly int TaelonCapacity = 1000;

		[Desc("Multiplier from sale of water to credits.")]
		public readonly float WaterSaleMultiplier = 1f;

		[Desc("Ticks to wait before stored credits will be sold automatically.")]
		public readonly long WaterSaleTimeoutTicks = 10000;

		public override object Create(ActorInitializer init) { return new AcceptsFreshWater(init.Self, this); }
	}

	public class AcceptsFreshWater : ISync, INotifyResourceAccepted
	{
		private readonly AcceptsFreshWaterInfo info;
		private readonly PlayerResources resources;
		private readonly Player owner;

		public AcceptsFreshWater(Actor self, AcceptsFreshWaterInfo info)
		{
			this.info = info;
			owner = self.Owner;
			resources = self.Trait<PlayerResources>();
		}

		[Sync]
		public int Water;

		public int WaterPercentage => (int)(((float)Water / info.WaterCapacity) * 100f);

		void INotifyResourceAccepted.OnResourceAccepted(Actor self, Actor refinery, string resourceType, int count, int value)
		{
			if (value >= 0)
			{
				// Reverse the cash we've just granted the player
				value = resources.ChangeCash(-value);

				if (Water < int.MaxValue)
				{
					try
					{
						checked
						{
							Water += value;
						}
					}
					catch (OverflowException)
					{
						Water = int.MaxValue;
					}
				}
			}

			if (Water >= info.WaterCapacity)
			{
				var total = (int)(Water * info.WaterSaleMultiplier);
				Water = 0;
				resources.GiveCash(total);
				Game.Sound.PlayNotification(owner.World.Map.Rules, owner, "Sounds", "CreditsReceived", null);
				Game.AddSystemLine($"Sold credits: ${total.ToString()}");
			}
		}
	}
}
