#region Copyright & License Information
/*
 * Copyright 2007-2020 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using OpenRA.Mods.Common.Scripting;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Dr.Traits
{
	public class DrPlayerResourcesInfo : TraitInfo
	{
		[Desc("Maximum holding capacity of the water refinery before it is sold automatically.")]
		public readonly int WaterCapacity = 3000;

		[Desc("Maximum holding capacity of taelon before it is discarded.")]
		public readonly int TaelonCapacity = 1000;

		[Desc("Multiplier from sale of water to credits.")]
		public readonly float WaterSaleMultiplier = 1f;

		[Desc("Ticks to wait before stored credits will be sold automatically.")]
		public readonly long WaterSaleTimeoutTicks = 10000;

		public override object Create(ActorInitializer init) { return new DrPlayerResources(init.Self, this); }
	}

	public class DrPlayerResources : ISync
	{
		private readonly DrPlayerResourcesInfo info;
		private readonly PlayerResources resources;
		private readonly Player owner;

		public DrPlayerResources(Actor self, DrPlayerResourcesInfo info)
		{
			this.info = info;
			owner = self.Owner;
			resources = self.Trait<PlayerResources>();
		}

		[Sync]
		public int Water;

		public int WaterPercentage => (int)(((float)Water / info.WaterCapacity) * 100f);

		public int AddWater(int amount)
		{
			if (amount >= 0)
			{
				if (Water < int.MaxValue)
				{
					try
					{
						checked
						{
							Water += amount;
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
				TextNotificationsManager.AddTransientLine($"Sold credits: ${total.ToString()}", owner);
			}

			return amount;
		}
	}
}
