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
using OpenRA.Effects;
using OpenRA.GameRules;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Effects;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Dr.Projectiles;
using OpenRA.Traits;

namespace OpenRA.Mods.Dr.Effects
{
	public class ShockWaveProjectileEffect : IEffect, ISync
	{
		readonly ShockWaveProjectileInfo info;
		readonly ShockwaveProjectileArgs args;
		readonly Animation anim;

		ContrailRenderable contrail;
		string trailPalette;

		[Sync]
		WPos projectilepos, targetpos, source;
		int lifespan, estimatedlifespan;
		[Sync]
		int facing;
		int ticks, smokeTicks;
		[Sync]
		bool converged;
		World world;
		public bool DetonateSelf { get; private set; }
		public WPos Position { get { return projectilepos; } }

		public ShockWaveProjectileEffect(ShockWaveProjectileInfo info, ShockwaveProjectileArgs args, int lifespan, int estimatedlifespan)
		{
			this.info = info;
			this.args = args;
			this.lifespan = lifespan;
			this.estimatedlifespan = estimatedlifespan;
			projectilepos = args.Source;
			source = args.Source;

			world = args.SourceActor.World;
			targetpos = args.PassiveTarget;
			facing = args.Facing.Angle;

			if (!string.IsNullOrEmpty(info.Image))
			{
				anim = new Animation(world, info.Image, new Func<WAngle>(GetEffectiveFacing));
				anim.PlayRepeating(info.Sequences.Random(world.SharedRandom));
			}

			if (info.ContrailLength > 0)
			{
				var color = info.ContrailUsePlayerColor ? ContrailRenderable.ChooseColor(args.SourceActor) : info.ContrailColor;
				contrail = new ContrailRenderable(world, color, info.ContrailWidth, info.ContrailLength, info.ContrailDelay, info.ContrailZOffset);
			}

			trailPalette = info.TrailPalette;
			if (info.TrailUsePlayerPalette)
				trailPalette += args.SourceActor.Owner.InternalName;

			smokeTicks = info.TrailDelay;
		}

		WAngle GetEffectiveFacing()
		{
			var at = (float)ticks / (lifespan - 1);
			var attitude = WAngle.Zero.Tan() * (1 - 2 * at) / (4 * 1024);

			var u = (facing % 512) / 512f;
			var scale = 2048 * u * (1 - u);

			var effective = (int)(facing < 512
				? facing - scale * attitude
				: facing + scale * attitude);

			return new WAngle(effective);
		}

		public IEnumerable<IRenderable> Render(WorldRenderer wr)
		{
			if (info.ContrailLength > 0)
				yield return contrail;

			if (anim == null || ticks >= lifespan)
				yield break;

			if (!world.FogObscures(projectilepos))
			{
				if (info.Shadow)
				{
					var dat = world.Map.DistanceAboveTerrain(projectilepos);
					var shadowPos = projectilepos - new WVec(0, 0, dat.Length);
					foreach (var r in anim.Render(shadowPos, wr.Palette(info.ShadowPalette)))
						yield return r;
				}

				var palette = wr.Palette(info.Palette);
				foreach (var r in anim.Render(projectilepos, palette))
					yield return r;
			}
		}

		bool Side(WPos p1, WPos p2, WPos p)
		{
			WVec diff = p2 - p1;
			WVec perp = new WVec(-diff.Y, diff.X, 0);
			float d = Dot2d(p - p1, perp);
			return d < 0;
		}

		int Dot2d(WVec a, WVec b)
		{
			return a.X * b.X + a.Y * b.Y;
		}

		public void Tick(World world)
		{
			ticks++;
			if (anim != null)
				anim.Tick();

			var lastPos = projectilepos;

			if (!converged)
			{
				var p1 = args.ConvergePoint;
				var p2 = p1 + args.Normal;
				if (Side(p1, p2, projectilepos))
				{
					source = projectilepos;
					targetpos = projectilepos + args.OriginalTargetVec;
					converged = true;
					ticks = 0; // Have to reset to prevent improper lerp
				}
			}

			projectilepos = WPos.Lerp(source, targetpos, ticks, estimatedlifespan);

			// Check for walls or other blocking obstacles.
			WPos blockedPos;
			if (info.Blockable && BlocksProjectiles.AnyBlockingActorsBetween(world, world.LocalPlayer, lastPos, projectilepos, info.Width, out blockedPos))
			{
				projectilepos = blockedPos;
				DetonateSelf = true;
			}

			if (!string.IsNullOrEmpty(info.TrailImage) && --smokeTicks < 0)
			{
				var delayedPos = WPos.Lerp(source, targetpos, ticks - info.TrailDelay, estimatedlifespan);
				world.AddFrameEndTask(w => w.Add(new SpriteEffect(delayedPos, w, info.TrailImage, info.TrailSequences.Random(world.SharedRandom),
					trailPalette, false, GetEffectiveFacing().Angle)));

				smokeTicks = info.TrailInterval;
			}

			if (info.ContrailLength > 0)
				contrail.Update(projectilepos);

			var flightLengthReached = ticks >= lifespan;

			if (flightLengthReached)
				DetonateSelf = true;

			// Driving into cell with higher height level
			DetonateSelf |= world.Map.DistanceAboveTerrain(projectilepos) < info.ExplodeUnderThisAltitude;

			if (DetonateSelf)
				Explode(world);
		}

		public void Explode(World world)
		{
			args.Weapon.Impact(Target.FromPos(projectilepos), new WarheadArgs
			{
				SourceActor = args.SourceActor,
				DamageModifiers = args.DamageModifiers
			});

			if (info.ContrailLength > 0)
				world.AddFrameEndTask(w => w.Add(new ContrailFader(projectilepos, contrail)));

			world.AddFrameEndTask(w => w.Remove(this));
		}
	}
}
