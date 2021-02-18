#region Copyright & License Information
/*
 * Copyright 2007-2018 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using OpenRA.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits.Render
{
	[Desc("Renders an overlay when the actor is taking heavy damage.")]
	public class WithFlamesOverlayInfo : TraitInfo, Requires<RenderSpritesInfo>
	{
		public readonly string Image = "eosmlfl0";

		[SequenceReference("Image")]
		public readonly string Sequence = "idle";

		[Desc("Damage types that this should be used for (defined on the warheads).",
			"Leave empty to disable all filtering.")]
		public readonly BitSet<DamageType> DamageTypes = default(BitSet<DamageType>);

		public readonly WVec Offset = WVec.Zero;

		[Desc("Trigger when Undamaged, Light, Medium, Heavy, Critical or Dead.")]
		public readonly DamageState MinimumDamageState = DamageState.Heavy;
		public readonly DamageState MaximumDamageState = DamageState.Dead;

		public override object Create(ActorInitializer init) { return new WithFlamesOverlay(init.Self, this); }
	}

	public class WithFlamesOverlay : ITick
	{
		readonly WithFlamesOverlayInfo info;
		readonly Animation anim;

		bool isBurning;

		public WithFlamesOverlay(Actor self, WithFlamesOverlayInfo info)
		{
			this.info = info;

			var rs = self.Trait<RenderSprites>();

			anim = new Animation(self.World, info.Image);
			rs.Add(new AnimationWithOffset(anim, () => info.Offset, () => !isBurning));
			anim.PlayRepeating(info.Sequence);
		}

		void ITick.Tick(Actor self)
		{
			var dmgState = self.GetDamageState();
			isBurning = dmgState > info.MinimumDamageState && dmgState <= info.MaximumDamageState;
		}
	}
}
