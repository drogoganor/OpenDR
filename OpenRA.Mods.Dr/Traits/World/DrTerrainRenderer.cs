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

using System;
using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("What type of tile is it? Land, Sea, etc.")]
	public enum TerrainMatchType
	{
		[Desc("Any type of land tile (1-15).")]
		Land,
		[Desc("Sea tile (0).")]
		Sea
	}

	[Desc("A neighbour tile and the TerrainMatchType we expect it to be.")]
	public class DrTerrainNeighborInfo
	{
		[Desc("Position of this tile relative to the target.")]
		public CVec Offset;

		[Desc("Check if this tile matches this condition.")]
		public TerrainMatchType Match;
	}

	[Desc("A tile type, a set of neighbour tile match conditions, and a target tile type to set the tile to if all conditions are met.")]
	public class DrTerrainShorelineInfo
	{
		[Desc("The tile type to inspect.")]
		public TerrainMatchType Match;

		[Desc("The tile type to display if all neighbours match.")]
		public ushort SetType;

		[Desc("Set of neighbour tiles and types to match against.")]
		[FieldLoader.LoadUsing("LoadNeighbors")]
		public Dictionary<string, DrTerrainNeighborInfo> Neighbors;

		static object LoadNeighbors(MiniYaml yaml)
		{
			var retList = new Dictionary<string, DrTerrainNeighborInfo>();
			var neighbors = yaml.Nodes.First(x => x.Key == "Neighbors");
			foreach (var node in neighbors.Value.Nodes.Where(n => n.Key.StartsWith("NeighborMatch")))
			{
				var ret = new DrTerrainNeighborInfo();
				FieldLoader.Load(ret, node.Value);
				retList.Add(node.Key, ret);
			}

			return retList;
		}
	}

	public class DrTerrainRendererInfo : ITraitInfoInterface
	{
		[FieldLoader.LoadUsing("LoadShorelines")]
		public Dictionary<string, DrTerrainShorelineInfo> Shorelines;

		static object LoadShorelines(MiniYaml yaml)
		{
			var retList = new Dictionary<string, DrTerrainShorelineInfo>();
			var shorelines = yaml.Nodes.First(x => x.Key == "Shorelines");
			foreach (var node in shorelines.Value.Nodes.Where(n => n.Key.StartsWith("ShoreMatch")))
			{
				var ret = new DrTerrainShorelineInfo();
				FieldLoader.Load(ret, node.Value);
				retList.Add(node.Key, ret);
			}

			return retList;
		}

		public object Create(ActorInitializer init) { return new DrTerrainRenderer(init.World, this); }
	}

	internal static class DrTerrainHelper
	{
		internal static bool IsLandTile(this TerrainTile tile) { return tile.Type > 0; }
		internal static bool IsSeaTile(this TerrainTile tile) { return tile.Type == 0; }

		internal static bool IsMatch(this TerrainTile tile, TerrainMatchType match)
		{
			switch (match)
			{
				case TerrainMatchType.Land:
					if (tile.IsLandTile())
						return true;
					break;
				case TerrainMatchType.Sea:
					if (tile.IsSeaTile())
						return true;
					break;
			}

			return false;
		}
	}

	public sealed class DrTerrainRenderer : IRenderTerrain, IWorldLoaded, INotifyActorDisposing
	{
		readonly DrTerrainRendererInfo info;
		readonly Map map;
		readonly Dictionary<string, TerrainSpriteLayer> spriteLayers = new Dictionary<string, TerrainSpriteLayer>();
		Theater theater;
		bool disposed;

		public DrTerrainRenderer(World world, DrTerrainRendererInfo info)
		{
			map = world.Map;
			this.info = info;
		}

		void IWorldLoaded.WorldLoaded(World world, WorldRenderer wr)
		{
			theater = wr.Theater;

			foreach (var template in map.Rules.TileSet.Templates)
			{
				var palette = template.Value.Palette ?? TileSet.TerrainPaletteInternalName;
				spriteLayers.GetOrAdd(palette, pal =>
					new TerrainSpriteLayer(world, wr, theater.Sheet, BlendMode.Alpha, wr.Palette(palette), world.Type != WorldType.Editor));
			}

			foreach (var cell in map.AllCells)
				UpdateCell(cell);

			map.Tiles.CellEntryChanged += UpdateCell;
			map.Height.CellEntryChanged += UpdateCell;
		}

		public void UpdateCell(CPos cell)
		{
			for (int x = -1; x < 2; x++)
			{
				for (int y = -1; y < 2; y++)
				{
					var newCell = cell + new CVec(x, y);

					if (!map.Contains(newCell))
						continue;

					var tile = map.Tiles[newCell];
					var palette = TileSet.TerrainPaletteInternalName;
					if (map.Rules.TileSet.Templates.ContainsKey(tile.Type))
						palette = map.Rules.TileSet.Templates[tile.Type].Palette ?? palette;

					tile = GetShoreTile(newCell);
					var sprite = theater.TileSprite(tile);
					foreach (var kv in spriteLayers)
						kv.Value.Update(newCell, palette == kv.Key ? sprite : null, false);
				}
			}
		}

		TerrainTile GetShoreTile(CPos cell)
		{
			var tile = map.Tiles[cell];
			var matchShorelines = info.Shorelines.Values.Where(x => tile.IsMatch(x.Match));
			int numIndices = 4;

			foreach (var m in matchShorelines)
			{
				bool match = true;
				int count = 0;
				foreach (var n in m.Neighbors.Values)
				{
					var neighborPos = cell + n.Offset;
					if (map.Tiles.Contains(neighborPos))
					{
						count++;
						var neighbour = map.Tiles[neighborPos];
						match = neighbour.IsMatch(n.Match);
						if (!match)
							break;
					}
				}

				if (match)
				{
					tile = new TerrainTile(m.SetType, (byte)Game.CosmeticRandom.Next(numIndices));
				}
			}

			return tile;
		}

		void IRenderTerrain.RenderTerrain(WorldRenderer wr, Viewport viewport)
		{
			foreach (var kv in spriteLayers.Values)
				kv.Draw(wr.Viewport);

			foreach (var r in wr.World.WorldActor.TraitsImplementing<IRenderOverlay>())
				r.Render(wr);
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			if (disposed)
				return;

			map.Tiles.CellEntryChanged -= UpdateCell;
			map.Height.CellEntryChanged -= UpdateCell;

			foreach (var kv in spriteLayers.Values)
				kv.Dispose();

			disposed = true;
		}
	}
}
