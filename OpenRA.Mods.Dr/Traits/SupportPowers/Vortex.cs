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
	[Desc(".")]
	public class VortexInfo : ConditionalTraitInfo
	{
		[Desc("Projectile to spawn.")]
		public readonly string WeaponName = string.Empty;

		[Desc("Delay between projectile spawns.")]
		public readonly int BurstDelay = 18;

		[Desc("Number of bursts total.")]
		public readonly int BurstTotal = 10;

		[Desc("Rotation rate.")]
		public readonly int RotationRate = 15;

		public override object Create(ActorInitializer init) { return new Vortex(this); }
	}

	public class Vortex : ConditionalTrait<VortexInfo>, ITick
	{
		readonly VortexInfo info;
		readonly WVec targetVec = new WVec(8192, 0, 0);
		int rotation = 0;
		int ticks = 0;

		public Vortex(VortexInfo info)
			: base(info)
		{
			this.info = info;
		}

		void ITick.Tick(Actor self)
		{
			ticks++;

			if (ticks > info.BurstDelay * info.BurstTotal)
			{
				if (self.CurrentActivity != null)
					self.CancelActivity();

				self.QueueActivity(new RemoveSelf());
				return;
			}

			if (ticks % info.BurstDelay != 0)
				return;

			var rotatedVec = targetVec.Rotate(WRot.FromFacing(rotation));

			var newTarget = self.CenterPosition + rotatedVec;
			var tar = Target.FromPos(newTarget);

			if (self.CurrentActivity != null)
				self.CancelActivity();

			self.QueueActivity(new Attack(self, tar, true, true));

			rotation -= info.RotationRate;
		}
	}
}
