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

using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Traits;

namespace OpenRA.Mods.Dr.Traits.Render
{
	[Desc("Renders periodic splash animations over selected sea tiles.")]
	public class SeaTileSplashesOverlayInfo : TraitInfo
	{
		public readonly float Percent = 0.03f;

		public readonly int RoundDelay = 40;
		public readonly int RandomMax = 15;
		public readonly int TickLength = 1600;

		public readonly string SequencePrefix = "splash";
		public readonly int[] Frames;

		public override object Create(ActorInitializer init) { return new SeaTileSplashesOverlay(init.Self, this); }
	}

	public class SplashRenderSpritesInfo : RenderSpritesInfo
	{
		public new readonly string Image;

		public SplashRenderSpritesInfo()
		{
		}

		public SplashRenderSpritesInfo(
			string splashImageName)
		{
			Image = splashImageName;
		}
	}

	public class SeaTileSplashesOverlay : ITick, IWorldLoaded, IRenderAnnotations
	{
		class SeaTileAnimation
		{
			public string Sequence;
			public Animation Animation;
		}

		class SeaTileSplash
		{
			public int Delay;
			public string Sequence;
			public Animation Animation;
			public WPos Position;
		}

		struct SeaTileSample
		{
			public CPos Cell;
			public TerrainTile Tile;
		}

		readonly Actor self;
		readonly SeaTileSplashesOverlayInfo info;
		readonly List<SeaTileAnimation> splashAnimations = new List<SeaTileAnimation>();
		readonly List<SeaTileSplash> splashes = new List<SeaTileSplash>();

		bool IRenderAnnotations.SpatiallyPartitionable => false;

		public SeaTileSplashesOverlay(Actor self, SeaTileSplashesOverlayInfo info)
		{
			this.info = info;
			this.self = self;
		}

		int tick = 0;

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			var splashImageName = $"{info.SequencePrefix}-{self.World.Map.Tileset.ToLowerInvariant()}";

			// Get all sea tiles
			var seaTiles = new List<SeaTileSample>();
			foreach (var cell in self.World.Map.AllCells)
			{
				var tile = self.World.Map.Tiles[cell];
				if (tile.Type == 0)
				{
					seaTiles.Add(new SeaTileSample
					{
						Cell = cell,
						Tile = tile,
					});
				}
			}

			// Get the percentage of these sea tiles
			var numSplashes = info.Frames.Length;
			var percentAbsolute = (int)(seaTiles.Count * info.Percent);
			var selectedSeaTiles = seaTiles.OrderBy(x => self.World.LocalRandom.Next()).Take(percentAbsolute);

			foreach (var selectedSeaTile in selectedSeaTiles)
			{
				// Pick the underlying variant as animation index
				var tileIndex = selectedSeaTile.Tile.Index % info.Frames.Length;
				//if (tileIndex >= info.Frames.Length) // Or random if we don't have that one
				//	tileIndex = (byte)info.Frames[self.World.LocalRandom.Next(info.Frames.Length - 1)];

				var splashSequenceName = $"{info.SequencePrefix}{tileIndex + 1}";
				var animation = new Animation(self.World, splashImageName);
				animation.Play(splashSequenceName);

				splashes.Add(new SeaTileSplash
				{
					Animation = animation,
					Sequence = splashSequenceName,
					Delay = self.World.LocalRandom.Next(info.RandomMax),
					Position = self.World.Map.CenterOfCell(selectedSeaTile.Cell) - new WVec(512, 512, 0)
				});
			}
		}

		void ITick.Tick(Actor self)
		{
			if (tick >= info.RoundDelay)
			{
				tick = 0;
			}
			else
				tick++;


			// Render
			for (var i = 0; i < splashes.Count; i++)
			{
				var splash = splashes[i];
				if (splash.Delay == tick)
				{
					splash.Animation.Play(splash.Sequence);
				}

				splash.Animation.Tick();
			}
		}

		IEnumerable<IRenderable> IRenderAnnotations.RenderAnnotations(Actor self, WorldRenderer wr)
		{
			var palette = wr.Palette("terrain");
			foreach (var splash in splashes)
			{
				foreach (var renderable in splash.Animation.Render(splash.Position, palette))
				{
					yield return renderable;
				}
			}
		}
	}
}
