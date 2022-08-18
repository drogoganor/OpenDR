//#region Copyright & License Information
///*
// * Copyright 2007-2019 The OpenRA Developers (see AUTHORS)
// * This file is part of OpenRA, which is free software. It is made
// * available to you under the terms of the GNU General Public License
// * as published by the Free Software Foundation, either version 3 of
// * the License, or (at your option) any later version. For more
// * information, see COPYING.
// */
//#endregion

//using System.Collections.Generic;
//using System.Linq;
//using OpenRA.Activities;
//using OpenRA.Mods.Common.Activities;
//using OpenRA.Mods.Common.Traits;
//using OpenRA.Mods.Dr.Traits;
//using OpenRA.Primitives;
//using OpenRA.Traits;

//namespace OpenRA.Mods.Dr.Activities
//{
//	public class DrHarvestResource : Activity
//	{
//		readonly Freighter harv;
//		readonly FreighterInfo harvInfo;
//		readonly IFacing facing;
//		readonly ResourceClaimLayer claimLayer;

//		readonly BodyOrientation body;
//		readonly IMove move;
//		readonly CPos targetCell;
//		readonly INotifyFreighterAction[] notifyHarvesterActions;

//		public DrHarvestResource(Actor self, CPos targetCell)
//		{
//			harv = self.Trait<Freighter>();
//			harvInfo = self.Info.TraitInfo<FreighterInfo>();
//			facing = self.Trait<IFacing>();
//			body = self.Trait<BodyOrientation>();
//			move = self.Trait<IMove>();
//			claimLayer = self.World.WorldActor.Trait<ResourceClaimLayer>();

//			this.targetCell = targetCell;
//			notifyHarvesterActions = self.TraitsImplementing<INotifyFreighterAction>().ToArray();
//		}

//		protected override void OnFirstRun(Actor self)
//		{
//			// We can safely assume the claim is successful, since this is only called in the
//			// same actor-tick as the targetCell is selected. Therefore no other harvester
//			// would have been able to claim.
//			claimLayer.TryClaimCell(self, targetCell);
//		}

//		public override bool Tick(Actor self)
//		{
//			if (IsCanceling || harv.IsFull)
//				return true;

//			// Move towards the target cell
//			if (self.Location != targetCell)
//			{
//				foreach (var n in notifyHarvesterActions)
//					n.MovingToResources(self, targetCell);

//				QueueChild(move.MoveTo(targetCell, 2));
//				return false;
//			}

//			if (!harv.CanHarvestCell(self, self.Location))
//				return true;

//			// Turn to one of the harvestable facings
//			if (harvInfo.HarvestFacings != 0)
//			{
//				var current = facing.Facing;
//				var desired = body.QuantizeFacing(current, harvInfo.HarvestFacings);
//				if (desired != current)
//				{
//					QueueChild(new Turn(self, desired));
//					return false;
//				}
//			}

//			var resource = Harvest(self, self.Location);
//			if (resource == null)
//				return true;

//			harv.AcceptResource(self, resource);

//			foreach (var t in notifyHarvesterActions)
//				t.Harvested(self, resource);

//			QueueChild(new Wait(harvInfo.BaleLoadDelay));
//			return false;
//		}

//		protected DrResourceType Harvest(Actor self, CPos location)
//		{
//			var firstResourceActor = self.World.Actors.FirstOrDefault(actor => harv.Info.Resources.Contains(actor.Info.Name) && self.World.Map.CellContaining(actor.CenterPosition) == location);
//			if (firstResourceActor == null)
//				return null;

//			return firstResourceActor.Trait<DrResourceType>();
//		}

//		protected override void OnLastRun(Actor self)
//		{
//			claimLayer.RemoveClaim(self);
//		}

//		public override void Cancel(Actor self, bool keepQueue = false)
//		{
//			foreach (var n in notifyHarvesterActions)
//				n.MovementCancelled(self);

//			base.Cancel(self, keepQueue);
//		}

//		public override IEnumerable<TargetLineNode> TargetLineNodes(Actor self)
//		{
//			yield return new TargetLineNode(Target.FromCell(self.World, targetCell), Color.Crimson);
//		}
//	}
//}
