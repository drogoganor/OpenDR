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

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Activities;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Effects;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.Dr.Activities;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Dr.Traits
{
	public class DrRefineryInfo : TraitInfo, Requires<WithSpriteBodyInfo>, IAcceptDrResourcesInfo
	{
		[Desc("Actual harvester facing when docking, 0-255 counter-clock-wise.")]
		public readonly WAngle DockAngle = WAngle.Zero;

		[Desc("Docking cell relative to top-left cell.")]
		public readonly CVec DockOffset = CVec.Zero;

		[Desc("Does the refinery require the harvester to be dragged in?")]
		public readonly bool IsDragRequired = false;

		[Desc("Vector by which the harvester will be dragged when docking.")]
		public readonly WVec DragOffset = WVec.Zero;

		[Desc("In how many steps to perform the dragging?")]
		public readonly int DragLength = 0;

		[Desc("Store resources in silos. Adds cash directly without storing if set to false.")]
		public readonly bool UseStorage = true;

		[Desc("Discard resources once silo capacity has been reached.")]
		public readonly bool DiscardExcessResources = false;

		public readonly bool ShowTicks = true;
		public readonly int TickLifetime = 30;
		public readonly int TickVelocity = 2;
		public readonly int TickRate = 10;

		public override object Create(ActorInitializer init) { return new DrRefinery(init.Self, this); }
	}

	public class DrRefinery : INotifyCreated, ITick, IAcceptDrResources, INotifySold, INotifyCapture,
		INotifyOwnerChanged, ISync, INotifyActorDisposing
	{
		readonly Actor self;
		readonly DrRefineryInfo info;
		PlayerResources playerResources;
		RefineryResourceMultiplier[] resourceMultipliers;

		int currentDisplayTick = 0;
		int currentDisplayValue = 0;

		[Sync]
		Actor dockedHarv = null;

		[Sync]
		bool preventDock = false;

		public bool AllowDocking { get { return !preventDock; } }
		public CVec DeliveryOffset { get { return info.DockOffset; } }
		public WAngle DeliveryAngle { get { return info.DockAngle; } }
		public bool IsDragRequired { get { return info.IsDragRequired; } }
		public WVec DragOffset { get { return info.DragOffset; } }
		public int DragLength { get { return info.DragLength; } }

		public DrRefinery(Actor self, DrRefineryInfo info)
		{
			this.self = self;
			this.info = info;
			playerResources = self.Owner.PlayerActor.Trait<PlayerResources>();
			currentDisplayTick = info.TickRate;
		}

		void INotifyCreated.Created(Actor self)
		{
			resourceMultipliers = self.TraitsImplementing<RefineryResourceMultiplier>().ToArray();
		}

		public virtual Activity DockSequence(Actor harv, Actor self)
		{
			return new SpriteFreighterDockSequence(harv, self, DeliveryAngle, IsDragRequired, DragOffset, DragLength);
		}

		public IEnumerable<TraitPair<Freighter>> GetLinkedHarvesters()
		{
			return self.World.ActorsWithTrait<Freighter>()
				.Where(a => a.Trait.LinkedProc == self);
		}

		public bool CanGiveResource(int amount) { return !info.UseStorage || info.DiscardExcessResources || playerResources.CanGiveResources(amount); }

		public void GiveResource(int amount)
		{
			amount = Util.ApplyPercentageModifiers(amount, resourceMultipliers.Select(m => m.GetModifier()));

			if (info.UseStorage)
			{
				if (info.DiscardExcessResources)
					amount = Math.Min(amount, playerResources.ResourceCapacity - playerResources.Resources);

				playerResources.GiveResources(amount);
			}
			else
				amount = playerResources.ChangeCash(amount);

			foreach (var notify in self.World.ActorsWithTrait<INotifyResourceAccepted>())
			{
				if (notify.Actor.Owner != self.Owner)
					continue;

				notify.Trait.OnResourceAccepted(notify.Actor, self, amount);
			}

			if (info.ShowTicks)
				currentDisplayValue += amount;
		}

		void CancelDock(Actor self)
		{
			preventDock = true;
		}

		void ITick.Tick(Actor self)
		{
			// Harvester was killed while unloading
			if (dockedHarv != null && dockedHarv.IsDead)
				dockedHarv = null;

			if (info.ShowTicks && currentDisplayValue > 0 && --currentDisplayTick <= 0)
			{
				var temp = currentDisplayValue;
				if (self.Owner.IsAlliedWith(self.World.RenderPlayer))
					self.World.AddFrameEndTask(w => w.Add(new FloatingText(self.CenterPosition, self.Owner.Color, FloatingText.FormatCashTick(temp), 30)));
				currentDisplayTick = info.TickRate;
				currentDisplayValue = 0;
			}
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			CancelDock(self);
			foreach (var harv in GetLinkedHarvesters())
				harv.Trait.UnlinkProc(harv.Actor, self);
		}

		public void OnDock(Actor harv, DrDeliverResources dockOrder)
		{
			if (!preventDock)
			{
				dockOrder.QueueChild(new CallFunc(() => dockedHarv = harv, false));
				dockOrder.QueueChild(DockSequence(harv, self));
				dockOrder.QueueChild(new CallFunc(() => dockedHarv = null, false));
			}
		}

		void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
		{
			// Unlink any harvesters
			foreach (var harv in GetLinkedHarvesters())
				harv.Trait.UnlinkProc(harv.Actor, self);

			playerResources = newOwner.PlayerActor.Trait<PlayerResources>();
		}

		void INotifyCapture.OnCapture(Actor self, Actor captor, Player oldOwner, Player newOwner, BitSet<CaptureType> captureTypes)
		{
			// Steal any docked harv too
			if (dockedHarv != null)
			{
				dockedHarv.ChangeOwner(newOwner);

				// Relink to this refinery
				dockedHarv.Trait<Freighter>().LinkProc(dockedHarv, self);
			}
		}

		void INotifySold.Selling(Actor self) { CancelDock(self); }
		void INotifySold.Sold(Actor self)
		{
			foreach (var harv in GetLinkedHarvesters())
				harv.Trait.UnlinkProc(harv.Actor, self);
		}
	}
}
