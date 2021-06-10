#region Copyright & License Information
/*
 * Copyright 2007-2020 The OpenRA Developers (see AUTHORS)
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Terrain;
using OpenRA.Traits;

namespace OpenRA.Mods.Dr.Traits
{
	internal static class DrTerrainHelper
	{
		internal static bool IsLandTile(this TerrainTile tile) { return tile.Type > 0; }
		internal static bool IsSeaTile(this TerrainTile tile) { return tile.Type == 0; }

		internal static bool IsMatch(this TerrainTile tile, TerrainMatchType match, TerrainTile ourTile = default)
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

	[Desc("What type of tile is it? Land, Sea, etc.")]
	public enum TerrainMatchType
	{
		[Desc("Any type of land tile (1-15).")]
		Land,
		[Desc("Sea tile (0).")]
		Sea,
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

	[Desc("Calculates sea tile edges and renders the shore tiles.")]
	public class ShorelinesOverlayInfo : TraitInfo, ILobbyCustomRulesIgnore
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

		public override object Create(ActorInitializer init) { return new ShorelinesOverlay(init.World, this); }
	}

	public class ShorelinesOverlay : IWorldLoaded, INotifyActorDisposing, IRenderOverlay
	{
		readonly ShorelinesOverlayInfo info;
		readonly World world;
		readonly Map map;
		TerrainSpriteLayer spriteLayer;
		readonly DefaultTerrain terrainInfo;
		readonly DefaultTileCache tileCache;
		WorldRenderer worldRenderer;
		bool disposed;

		public ShorelinesOverlay(World world, ShorelinesOverlayInfo info)
		{
			this.info = info;
			this.world = world;
			map = world.Map;
			terrainInfo = map.Rules.TerrainInfo as DefaultTerrain;
			if (terrainInfo == null)
				throw new InvalidDataException("TerrainRenderer can only be used with the DefaultTerrain parser");

			tileCache = new DefaultTileCache(terrainInfo);
		}

		void IWorldLoaded.WorldLoaded(World world, WorldRenderer wr)
		{
			worldRenderer = wr;
			spriteLayer = new TerrainSpriteLayer(world, wr, tileCache.MissingTile, BlendMode.Alpha, world.Type != WorldType.Editor);
			foreach (var cell in map.AllCells)
				UpdateCell(cell);

			map.Tiles.CellEntryChanged += UpdateCell;
			map.Height.CellEntryChanged += UpdateCell;
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			if (disposed)
				return;

			map.Tiles.CellEntryChanged -= UpdateCell;
			map.Height.CellEntryChanged -= UpdateCell;

			spriteLayer.Dispose();

			tileCache.Dispose();
			disposed = true;
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

					spriteLayer.Clear(newCell);

					if (GetShoreTile(newCell, out var tile))
					{
						var palette = TileSet.TerrainPaletteInternalName;
						if (terrainInfo.Templates.TryGetValue(tile.Type, out var template))
							palette = ((DefaultTerrainTemplateInfo)template).Palette ?? palette;

						var sprite = tileCache.TileSprite(tile);
						var paletteReference = worldRenderer.Palette(palette);
						spriteLayer.Update(newCell, sprite, paletteReference);
					}
				}
			}
		}

		bool GetShoreTile(CPos cell, out TerrainTile resultTile)
		{
			var tile = map.Tiles[cell];
			var matchShorelines = info.Shorelines.Values.Where(x => tile.IsMatch(x.Match)).ToArray();
			const int numIndices = 4;

			foreach (var m in matchShorelines)
			{
				var match = true;
				foreach (var n in m.Neighbors.Values)
				{
					var neighborPos = cell + n.Offset;
					if (map.Tiles.Contains(neighborPos))
					{
						var neighbour = map.Tiles[neighborPos];
						match = neighbour.IsMatch(n.Match, tile);
						if (!match)
							break;
					}
				}

				if (match)
				{
					resultTile = new TerrainTile(m.SetType, (byte)Game.CosmeticRandom.Next(numIndices));
					return true;
				}
			}

			resultTile = default;
			return false;
		}

		public void Render(WorldRenderer wr)
		{
			spriteLayer.Draw(wr.Viewport);
		}
	}
}
