#region Copyright & License Information
/*
 * Copyright 2015- OpenRA.Mods.AS Developers (see AUTHORS)
 * This file is a part of a third-party plugin for OpenRA, which is
 * free software. It is made available to you under the terms of the
 * GNU General Public License as published by the Free Software
 * Foundation. For more information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.GameRules;
using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Effects;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Warheads;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Dr.Warheads
{
	[Desc("Spawn actors upon explosion. Don't use this with buildings.")]
	public class SpawnActorWarhead : Warhead, IRulesetLoaded<WeaponInfo>
	{
		[Desc("Actors to spawn.")]
		public readonly string[] Actors = { };

		[Desc("Defines the image of an optional animation played at the spawning location.")]
		public readonly string Image = null;

		[SequenceReference("Image")]
		[Desc("Defines the sequence of an optional animation played at the spawning location.")]
		public readonly string Sequence = "idle";

		[PaletteReference]
		[Desc("Defines the palette of an optional animation played at the spawning location.")]
		public readonly string Palette = "effect";

		[Desc("List of sounds that can be played at the spawning location.")]
		public readonly string[] Sounds = new string[0];

		public readonly bool UsePlayerPalette = false;

		public void RulesetLoaded(Ruleset rules, WeaponInfo info)
		{
			foreach (var a in Actors)
			{
				var actorInfo = rules.Actors[a.ToLowerInvariant()];
				var buildingInfo = actorInfo.TraitInfoOrDefault<BuildingInfo>();

				if (buildingInfo != null)
					throw new YamlException("SpawnActorWarhead cannot be used to spawn building actor '{0}'!".F(a));
			}
		}

		public override void DoImpact(Target target, Actor firedBy, IEnumerable<int> damageModifiers)
		{
			if (!target.IsValidFor(firedBy))
				return;

			var map = firedBy.World.Map;
			var targetCell = map.CellContaining(target.CenterPosition);

			var targetCells = map.FindTilesInCircle(targetCell, 1);
			var cell = targetCells.GetEnumerator();

			foreach (var a in Actors)
			{
				var placed = false;
				var td = new TypeDictionary();
				var ai = map.Rules.Actors[a.ToLowerInvariant()];

				td.Add(new OwnerInit(firedBy.Owner));
				Actor unit = null;
				while (cell.MoveNext())
				{
					var cellpos = firedBy.World.Map.CenterOfCell(cell.Current);
					var pos = cellpos;

					td.Add(new LocationInit(cell.Current));

					unit = firedBy.World.CreateActor(false, a.ToLowerInvariant(), td);

					firedBy.World.AddFrameEndTask(w =>
					{
						w.Add(unit);

						var palette = Palette;
						if (UsePlayerPalette)
							palette += unit.Owner.InternalName;

						if (Image != null)
							w.Add(new SpriteEffect(pos, w, Image, Sequence, palette));

						var sound = Sounds.RandomOrDefault(Game.CosmeticRandom);
						if (sound != null)
							Game.Sound.Play(SoundType.World, sound, pos);
					});

					placed = true;
					break;
				}

				if (!placed && unit != null)
					unit.Dispose();
			}
		}
	}
}
