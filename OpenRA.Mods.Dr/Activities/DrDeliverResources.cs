#region Copyright & License Information
/*
 * Copyright 2007-2021 The OpenRA Developers (see AUTHORS)
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
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Dr.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Dr.Activities
{
	public class DrDeliverResources : Activity
	{
		private readonly IMove movement;
		private readonly Freighter freighter;
		private readonly Actor targetActor;
		private readonly INotifyFreighterAction[] notifyFreighterActions;

		private Actor waterRefinery;

		public DrDeliverResources(Actor self, Actor targetActor = null)
		{
			movement = self.Trait<IMove>();
			freighter = self.Trait<Freighter>();
			this.targetActor = targetActor;
			notifyFreighterActions = self.TraitsImplementing<INotifyFreighterAction>().ToArray();
		}

		protected override void OnFirstRun(Actor self)
		{
			if (targetActor != null && targetActor.IsInWorld)
				freighter.LinkProc(self, targetActor);
		}

		public override bool Tick(Actor self)
		{
			if (IsCanceling)
				return true;

			// Find the nearest best refinery if not explicitly ordered to a specific refinery:
			if (freighter.LinkedProc == null || !freighter.LinkedProc.IsInWorld)
				freighter.ChooseNewProc(self, null);

			// No refineries exist; check again after delay defined in Harvester.
			if (freighter.LinkedProc == null)
			{
				QueueChild(new Wait(freighter.Info.SearchForDeliveryBuildingDelay));
				return false;
			}

			waterRefinery = freighter.LinkedProc;
			var iao = waterRefinery.Trait<IAcceptDrResources>();

			if (self.Location != waterRefinery.Location + iao.DeliveryOffset)
			{
				foreach (var n in notifyFreighterActions)
					n.MovingToRefinery(self, waterRefinery);

				QueueChild(movement.MoveTo(waterRefinery.Location + iao.DeliveryOffset, 0));
				return false;
			}

			QueueChild(new Wait(10));
			iao.OnDock(self, this);
			return true;
		}

		public override void Cancel(Actor self, bool keepQueue = false)
		{
			foreach (var n in notifyFreighterActions)
				n.MovementCancelled(self);

			base.Cancel(self, keepQueue);
		}

		public override IEnumerable<TargetLineNode> TargetLineNodes(Actor self)
		{
			if (waterRefinery != null)
				yield return new TargetLineNode(Target.FromActor(waterRefinery), Color.Green);
			else
				yield return new TargetLineNode(Target.FromActor(freighter.LinkedProc), Color.Green);
		}
	}
}
