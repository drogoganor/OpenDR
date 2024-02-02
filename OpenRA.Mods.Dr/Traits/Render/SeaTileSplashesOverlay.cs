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
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Dr.Traits.Render
{
	[Desc("Renders periodic splash animations over selected sea tiles.")]
	public class SeaTileSplashesOverlayInfo : TraitInfo
	{
		public readonly float Percent = 0.03f;

		public readonly int RoundDelay = 80;
		public readonly int RandomMax = 40;
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

	public class SeaTileSplashesOverlay : ITick, IWorldLoaded, IRenderOverlay
	{
		class SeaTileSplash
		{
			public int Delay;
			public Animation Animation;
			public string SequenceName;
			public ISpriteSequence Sequence;
			public CPos Cell;
		}

		struct SeaTileSample
		{
			public CPos Cell;
			public TerrainTile Tile;
		}

		readonly Actor self;
		readonly SeaTileSplashesOverlayInfo info;
		readonly List<SeaTileSplash> splashes = new List<SeaTileSplash>();

		TerrainSpriteLayer spriteLayer;
		PaletteReference palette;
		int tick = 0;

		public SeaTileSplashesOverlay(Actor self, SeaTileSplashesOverlayInfo info)
		{
			this.info = info;
			this.self = self;
		}

		void IWorldLoaded.WorldLoaded(World w, WorldRenderer wr)
		{
			var splashImageName = $"{info.SequencePrefix}-{self.World.Map.Tileset.ToLowerInvariant()}";
			//var sequences = w.Map.Rules.Sequences;
			palette = wr.Palette("terrain");

			// TODO: Fix
			return;

			if (spriteLayer == null)
			{
				//var first = sequences.GetSequence(splashImageName, $"{info.SequencePrefix}1").GetSprite(0);
				//var emptySprite = new Sprite(first.Sheet, Rectangle.Empty, TextureChannel.Alpha);
				//spriteLayer = new TerrainSpriteLayer(w, wr, emptySprite, BlendMode.None, wr.World.Type != WorldType.Editor);
			}

			// Get all sea tiles
			var seaTiles = new List<SeaTileSample>();
			foreach (var cell in self.World.Map.AllCells)
			{
				var tile = self.World.Map.Tiles[cell];
				if (tile.Type == 15 && IsValidSeaTile(cell))
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
			var selectedSeaTiles = seaTiles
				.OrderBy(x => self.World.LocalRandom.Next())
				.Take(percentAbsolute);

			foreach (var selectedSeaTile in selectedSeaTiles)
			{
				// Pick the underlying variant as animation index
				var tileIndex = selectedSeaTile.Tile.Index % info.Frames.Length;

				var splashSequenceName = $"{info.SequencePrefix}{tileIndex + 1}";
				var animation = new Animation(self.World, splashImageName);
				animation.Play(splashSequenceName);

				//var sequence = sequences.GetSequence(splashImageName, splashSequenceName);
				//splashes.Add(new SeaTileSplash
				//{
				//	Animation = animation,
				//	Cell = selectedSeaTile.Cell,
				//	SequenceName = splashSequenceName,
				//	Sequence = sequence,
				//	Delay = self.World.LocalRandom.Next(info.RandomMax)
				//});
			}
		}

		bool IsValidSeaTile(CPos cell)
		{
			var map = self.World.Map;
			for (var y = -1; y <= 0; y++)
			{
				for (var x = -1; x <= 0; x++)
				{
					var offset = new CVec(x, y);
					var neighborPos = cell + offset;
					if (neighborPos == cell)
						continue;

					if (map.Tiles.Contains(neighborPos))
					{
						var neighbour = map.Tiles[neighborPos];
						if (neighbour.Type != 15)
							return false;
					}
				}
			}

			return true;
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
					// Start animation
					splash.Animation.Play(splash.SequenceName);
					UpdateSpriteLayers(splash.Cell, splash.Sequence, 0, palette);
				}

				splash.Animation.Tick();
				UpdateSpriteLayers(splash.Cell, splash.Sequence, splash.Animation.CurrentFrame, palette);
			}
		}

		void UpdateSpriteLayers(CPos cell, ISpriteSequence sequence, int frame, PaletteReference palette)
		{
			if (sequence != null)
			{
				spriteLayer.Update(cell, sequence, palette, frame);
			}
			else
			{
				spriteLayer.Clear(cell);
			}
		}

		void IRenderOverlay.Render(WorldRenderer wr)
		{
			spriteLayer.Draw(wr.Viewport);
		}
	}
}
