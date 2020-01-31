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

using System.Linq;
using OpenRA.Activities;
using OpenRA.Mods.Dr.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.Render
{
	public class WithDrHarvestAnimationInfo : ITraitInfo, Requires<WithSpriteBodyInfo>, Requires<FreighterInfo>
	{
		[SequenceReference]
		[Desc("Displayed while harvesting.")]
		public readonly string HarvestSequence = "harvest";

		[Desc("Which sprite body to play the animation on.")]
		public readonly string Body = "body";

		public object Create(ActorInitializer init) { return new WithDrHarvestAnimation(init, this); }
	}

	public class WithDrHarvestAnimation : INotifyFreighterAction
	{
		public readonly WithDrHarvestAnimationInfo Info;
		readonly WithSpriteBody wsb;

		public WithDrHarvestAnimation(ActorInitializer init, WithDrHarvestAnimationInfo info)
		{
			Info = info;
			wsb = init.Self.TraitsImplementing<WithSpriteBody>().Single(w => w.Info.Name == Info.Body);
		}

		void INotifyFreighterAction.Harvested(Actor self, DrResourceType resource)
		{
			var sequence = wsb.NormalizeSequence(self, Info.HarvestSequence);
			if (wsb.DefaultAnimation.HasSequence(sequence) && wsb.DefaultAnimation.CurrentSequence.Name != sequence)
				wsb.PlayCustomAnimation(self, sequence);
		}

		void INotifyFreighterAction.Docked() { }
		void INotifyFreighterAction.Undocked() { }
		void INotifyFreighterAction.MovingToResources(Actor self, CPos targetCell) { }
		void INotifyFreighterAction.MovingToRefinery(Actor self, Actor refineryActor) { }
		void INotifyFreighterAction.MovementCancelled(Actor self) { }
	}
}
