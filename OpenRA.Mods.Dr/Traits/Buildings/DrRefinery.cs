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
	public class DrRefineryInfo : RefineryInfo
	{
		public override object Create(ActorInitializer init) { return new DrRefinery(init.Self, this); }
	}

	public class DrRefinery : Refinery, INotifyCreated, IAcceptResources, INotifyOwnerChanged
	{
		readonly DrRefineryInfo info;
		PlayerResources playerResources;
		IEnumerable<int> resourceValueModifiers;
		DrPlayerResources drPlayerResources;

		public DrRefinery(Actor self, DrRefineryInfo info)
            : base(self, info)
		{
			this.info = info;
			playerResources = self.Owner.PlayerActor.Trait<PlayerResources>();
			drPlayerResources = self.Owner.PlayerActor.Trait<DrPlayerResources>();
		}

		void INotifyCreated.Created(Actor self)
		{
			resourceValueModifiers = self.TraitsImplementing<IResourceValueModifier>().ToArray().Select(m => m.GetResourceValueModifier());
		}

		void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
		{
			// Unlink any harvesters
			foreach (var harv in GetLinkedHarvesters())
				harv.Trait.UnlinkProc(harv.Actor, self);

			drPlayerResources = newOwner.PlayerActor.Trait<DrPlayerResources>();
			playerResources = newOwner.PlayerActor.Trait<PlayerResources>();
		}

		int IAcceptResources.AcceptResources(string resourceType, int count)
		{
			if (!playerResources.Info.ResourceValues.TryGetValue(resourceType, out var resourceValue))
				return 0;

			var value = Util.ApplyPercentageModifiers(count * resourceValue, resourceValueModifiers);

			if (info.UseStorage)
			{
				var storageLimit = Math.Max(playerResources.ResourceCapacity - playerResources.Resources, 0);
				if (!info.DiscardExcessResources)
				{
					// Reduce amount if needed until it will fit the available storage
					while (value > storageLimit)
						value = Util.ApplyPercentageModifiers(--count * resourceValue, resourceValueModifiers);
				}
				else
					value = Math.Min(value, playerResources.ResourceCapacity - playerResources.Resources);

				drPlayerResources.AddWater(value);
			}
			else
				drPlayerResources.AddWater(value);

			// foreach (var notify in self.World.ActorsWithTrait<INotifyResourceAccepted>())
			// {
			// 	if (notify.Actor.Owner != self.Owner)
			// 		continue;

			// 	notify.Trait.OnResourceAccepted(notify.Actor, self, resourceType, count, value);
			// }
			return count;
		}
	}
}
