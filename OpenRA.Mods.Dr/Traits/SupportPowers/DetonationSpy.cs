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

using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Dr.Traits
{
	[Desc("Placed by a SpawnActorWarhead to spy on the destruction after a detonation.")]
	public class DetonationSpyInfo : ConditionalTraitInfo
	{
		[Desc("Stays for this many ticks.")]
		public readonly int StaysFor = 10;

		public override object Create(ActorInitializer init) { return new DetonationSpy(this); }
	}

	public class DetonationSpy : ConditionalTrait<DetonationSpyInfo>, ITick
	{
		readonly DetonationSpyInfo info;

		int ticks = 0;

		public DetonationSpy(DetonationSpyInfo info)
			: base(info)
		{
			this.info = info;
		}

		void ITick.Tick(Actor self)
		{
			ticks++;

			if (ticks > info.StaysFor)
			{
				self.QueueActivity(new RemoveSelf());
			}
		}
	}
}
