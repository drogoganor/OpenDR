#region Copyright & License Information
/*
 * Copyright 2015- OpenRA.Mods.AS Developers (see AUTHORS)
 * This file is a part of a third-party plugin for OpenRA, which is
 * free software. It is made available to you under the terms of the
 * GNU General Public License as published by the Free Software
 * Foundation. For more information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenRA.GameRules;
using OpenRA.Graphics;
using OpenRA.Mods.Dr.Effects;
using OpenRA.Traits;

namespace OpenRA.Mods.Dr.Projectiles
{
    public class VortexProjectileArgs : ProjectileArgs
    {
        public WVec Normal;
        public WVec VecNormalized;
    }

    [Desc("Detonates all warheads attached to Weapon each ExplosionInterval ticks.")]
	public class VortexProjectileInfo : IProjectileInfo, IRulesetLoaded<WeaponInfo>
	{
		[Desc("Projectile speed in WDist / tick, two values indicate variable velocity.")]
		public readonly WDist[] Speed = { new WDist(17) };

		[Desc("Maximum inaccuracy offset.")]
		public readonly WDist Inaccuracy = WDist.Zero;

		[Desc("How many ticks will pass between explosions.")]
		public readonly int ExplosionInterval = 8;

		[FieldLoader.Require]
		[WeaponReference]
		[Desc("Weapon that's detonated every interval.")]
		public readonly string Weapon = null;

		public WeaponInfo WeaponInfo { get; private set; }

		[Desc("If it's true then weapon won't continue firing past the target.")]
		public readonly bool KillProjectilesWhenReachedTargetLocation = false;

		[Desc("Where shall the bullets fly after instantiating? Possible values are Spread, Line and Focus")]
		public readonly FireMode FireMode = FireMode.Spread;

		[Desc("Interval in ticks between each spawned Trail animation.")]
		public readonly int TrailInterval = 2;

		[Desc("Image to display.")]
		public readonly string Image = null;

		[Desc("Loop a randomly chosen sequence of Image from this list while this projectile is moving.")]
		[SequenceReference("Image")]
		public readonly string[] Sequences = { "idle" };

		[Desc("The palette used to draw this projectile.")]
		[PaletteReference]
		public readonly string Palette = "effect";

		[Desc("Does this projectile have a shadow?")]
		public readonly bool Shadow = false;

		[Desc("Palette to use for this projectile's shadow if Shadow is true.")]
		[PaletteReference]
		public readonly string ShadowPalette = "shadow";

		[Desc("Trail animation.")]
		public readonly string TrailImage = null;

		[Desc("Loop a randomly chosen sequence of TrailImage from this list while this projectile is moving.")]
		[SequenceReference("TrailImage")]
		public readonly string[] TrailSequences = { "idle" };

		[Desc("Delay in ticks until trail animation is spawned.")]
		public readonly int TrailDelay = 1;

		[Desc("Palette used to render the trail sequence.")]
		[PaletteReference("TrailUsePlayerPalette")]
		public readonly string TrailPalette = "effect";

		[Desc("Use the Player Palette to render the trail sequence.")]
		public readonly bool TrailUsePlayerPalette = false;

		public readonly int ContrailLength = 0;
		public readonly int ContrailZOffset = 2047;
		public readonly Color ContrailColor = Color.White;
		public readonly bool ContrailUsePlayerColor = false;
		public readonly int ContrailDelay = 1;
		public readonly WDist ContrailWidth = new WDist(64);

		[Desc("Altitude where this bullet should explode when reached.",
			"Negative values allow this bullet to pass cliffs and terrain bumps.")]
		public readonly WDist ExplodeUnderThisAltitude = new WDist(-1536);

		[Desc("Is this blocked by actors with BlocksProjectiles trait.")]
		public readonly bool Blockable = true;

		[Desc("Width of projectile (used for finding blocking actors).")]
		public readonly WDist Width = new WDist(1);

		[Desc("If projectile touches an actor with one of these stances during or after the first bounce, trigger explosion.")]
		public readonly Stance ValidBounceBlockerStances = Stance.Enemy | Stance.Neutral | Stance.Ally;

        [Desc("Number of projectiles to fire.")]
        public readonly int NumProjectiles = 12;

        public IProjectile Create(ProjectileArgs args) { return new VortexProjectile(this, args); }

		void IRulesetLoaded<WeaponInfo>.RulesetLoaded(Ruleset rules, WeaponInfo info)
		{
			WeaponInfo weapon;
			if (!rules.Weapons.TryGetValue(Weapon.ToLowerInvariant(), out weapon))
				throw new YamlException("Weapons Ruleset does not contain an entry '{0}'".F(Weapon.ToLowerInvariant()));
			WeaponInfo = weapon;
		}
	}

	public class VortexProjectile : IProjectile, ISync
	{
		readonly VortexProjectileInfo info;
		readonly ProjectileArgs args;
		[Sync]
		readonly WDist speed;
		WPos targetpos, sourcepos;
		int lifespan;
		int ticks;
		int mindelay;
		World world;
		VortexProjectileEffect[] projectiles; // offset projectiles

		public Actor SourceActor { get { return args.SourceActor; } }

		public VortexProjectile(VortexProjectileInfo info, ProjectileArgs args)
		{
			this.info = info;
			this.args = args;

			sourcepos = args.Source;

			var firedBy = args.SourceActor;

			world = args.SourceActor.World;

			if (info.Speed.Length > 1)
				speed = new WDist(world.SharedRandom.Next(info.Speed[0].Length, info.Speed[1].Length));
			else
				speed = info.Speed[0];

			targetpos = GetTargetPos();

			mindelay = args.Weapon.MinRange.Length / speed.Length;

            projectiles = new VortexProjectileEffect[info.NumProjectiles];

			var mainFacing = (targetpos - sourcepos).Yaw.Facing;

			// used for lerping projectiles at the same pace
			var estimatedLifespan = Math.Max(args.Weapon.Range.Length / speed.Length, 1);

			// target that will be assigned
			Target target;

			// subprojectiles facing
			int facing = 0;

            int facingsInterval = 256 / info.NumProjectiles;

            for (int i = 0; i < info.NumProjectiles; i++)
            {
                target = Target.FromPos(targetpos);

                // If it's true then lifespan is counted from source position to target instead of max range.
                lifespan = info.KillProjectilesWhenReachedTargetLocation
                    ? Math.Max((args.PassiveTarget - args.Source).Length / speed.Length, 1)
                    : estimatedLifespan;

                facing = mainFacing + (facingsInterval * i);
                var newRotation = WRot.FromFacing(facing);
                var rotatedTarget = (targetpos - sourcepos).Rotate(newRotation);

                var dx = rotatedTarget.X - sourcepos.X;
                var dy = rotatedTarget.Y - sourcepos.Y;
                var normal = new WVec(-dy, dx, 0);

                target = Target.FromPos(sourcepos + rotatedTarget);
                var normalizedVec = WVec.Lerp(WVec.Zero, rotatedTarget, 512, rotatedTarget.Length);

                var projectileArgs = new VortexProjectileArgs
                {
                    Weapon = args.Weapon,
                    DamageModifiers = args.DamageModifiers,
                    Facing = facing,
                    Source = sourcepos,
                    CurrentSource = () => sourcepos,
                    SourceActor = firedBy,
                    GuidedTarget = target,
                    PassiveTarget = sourcepos + rotatedTarget,
                    VecNormalized = normalizedVec,
                    Normal = normal
                };

                projectiles[i] = new VortexProjectileEffect(info, projectileArgs, lifespan, estimatedLifespan);
            }

            foreach (var p in projectiles)
			world.AddFrameEndTask(w => w.Add(p));
		}

		// gets where main projectile should fly to
		WPos GetTargetPos()
		{
			var targetpos = args.PassiveTarget;

			return WPos.Lerp(sourcepos, targetpos, args.Weapon.Range.Length, (targetpos - sourcepos).Length);
		}

		public void Tick(World world)
		{
			if (ticks % info.ExplosionInterval == 0 && mindelay <= ticks)
				DoImpact();

			if (ticks >= lifespan)
			{
				foreach (var projectile in projectiles)
					projectile.Explode(world);

				world.AddFrameEndTask(w => w.Remove(this));
			}

			ticks++;
		}

		void DoImpact()
		{
			// Trigger all so-far-untriggered explosions.
			foreach (var projectile in projectiles)
				if (!projectile.DetonateSelf)
					info.WeaponInfo.Impact(Target.FromPos(projectile.Position), SourceActor, args.DamageModifiers);
		}

		public IEnumerable<IRenderable> Render(WorldRenderer wr)
		{
			yield break;
		}
	}
}
