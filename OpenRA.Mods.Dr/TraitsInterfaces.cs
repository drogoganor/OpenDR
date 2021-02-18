#region Copyright & License Information
/*
 * Copyright 2007-2019 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Dr.Activities;
using OpenRA.Traits;

namespace OpenRA.Mods.Dr.Traits
{
	public interface IAcceptDrResourcesInfo : ITraitInfoInterface { }
	public interface IAcceptDrResources
	{
		void OnDock(Actor harv, DrDeliverResources dockOrder);
		void GiveResource(int amount);
		bool CanGiveResource(int amount);
		CVec DeliveryOffset { get; }
		bool AllowDocking { get; }
	}

	public interface INotifyFreighterAction
	{
		void MovingToResources(Actor self, CPos targetCell);
		void MovingToRefinery(Actor self, Actor refineryActor);
		void MovementCancelled(Actor self);
		void Harvested(Actor self, DrResourceType resource);
		void Docked();
		void Undocked();
	}
}
