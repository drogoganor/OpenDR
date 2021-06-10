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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Terrain;
using OpenRA.Primitives;
using OpenRA.Support;
using OpenRA.Traits;

namespace OpenRA.Mods.Dr.Traits
{
	internal static class DrTileEdgesHelper
	{
		internal static bool IsLandTile2(this TerrainTile tile) { return tile.Type > 0; }
		internal static bool IsSeaTile2(this TerrainTile tile) { return tile.Type == 0; }

		internal static bool IsMatch(this TerrainTile tile, TileEdgeMatchType match, TerrainTile ourTile = default)
		{
			switch (match)
			{
				case TileEdgeMatchType.Land:
					if (tile.IsLandTile2())
						return true;
					break;
				case TileEdgeMatchType.Sea:
					if (tile.IsSeaTile2())
						return true;
					break;
			}

			return false;
		}
	}

	[Desc("What type of tile is it? Land, Sea, etc.")]
	public enum TileEdgeMatchType
	{
		[Desc("Any type of land tile (1-15).")]
		Land,
		[Desc("Sea tile (0).")]
		Sea,
	}

	[Desc("A neighbour tile and the TerrainMatchType we expect it to be.")]
	public class DrTileEdgeNeighborInfo
	{
		[Desc("Position of this tile relative to the target.")]
		public CVec Offset;

		[Desc("Check if this tile matches this condition.")]
		public TileEdgeMatchType Match;
	}

	[Desc("A tile type, a set of neighbour tile match conditions, and a target tile type to set the tile to if all conditions are met.")]
	public class DrTileEdgeInfo
	{
		[Desc("The tile type to inspect.")]
		public TileEdgeMatchType Match;

		[Desc("The tile type to display if all neighbours match.")]
		public ushort SetType;

		[Desc("Set of neighbour tiles and types to match against.")]
		[FieldLoader.LoadUsing("LoadNeighbors")]
		public Dictionary<string, DrTileEdgeNeighborInfo> Neighbors;

		static object LoadNeighbors(MiniYaml yaml)
		{
			var retList = new Dictionary<string, DrTileEdgeNeighborInfo>();
			var neighbors = yaml.Nodes.First(x => x.Key == "Neighbors");
			foreach (var node in neighbors.Value.Nodes.Where(n => n.Key.StartsWith("NeighborMatch")))
			{
				var ret = new DrTileEdgeNeighborInfo();
				FieldLoader.Load(ret, node.Value);
				retList.Add(node.Key, ret);
			}

			return retList;
		}
	}

	[Desc("Shows alpha-blended tile edges.")]
	public class TileEdgesOverlayInfo : TraitInfo, ILobbyCustomRulesIgnore
	{
		[FieldLoader.LoadUsing("LoadTileEdges")]
		public Dictionary<string, DrTileEdgeInfo> Shorelines;

		static object LoadTileEdges(MiniYaml yaml)
		{
			var retList = new Dictionary<string, DrTileEdgeInfo>();
			var shorelines = yaml.Nodes.First(x => x.Key == "TileEdges");
			foreach (var node in shorelines.Value.Nodes.Where(n => n.Key.StartsWith("TileMatch")))
			{
				var ret = new DrTileEdgeInfo();
				FieldLoader.Load(ret, node.Value);
				retList.Add(node.Key, ret);
			}

			return retList;
		}

		public override object Create(ActorInitializer init) { return new TileEdgesOverlay(init.World, this); }
	}

	public class TileEdgesOverlay : IWorldLoaded, INotifyActorDisposing, IRenderOverlay
	{
		readonly TileEdgesOverlayInfo info;
		readonly World world;
		readonly Map map;
		TerrainSpriteLayer spriteLayer;
		readonly DefaultTerrain terrainInfo;
		readonly DefaultTileCache tileCache;
		WorldRenderer worldRenderer;

		public TileEdgesOverlay(World world, TileEdgesOverlayInfo info)
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
		}

		private void UpdateCell(CPos cell)
		{
			for (var x = -1; x < 2; x++)
			{
				for (var y = -1; y < 2; y++)
				{
					var newCell = cell + new CVec(x, y);

					if (!map.Contains(newCell))
						continue;

					if (!GetShoreTile(newCell, out var tile))
						continue;

					spriteLayer.Clear(newCell);

					var palette = TileSet.TerrainPaletteInternalName;
					if (terrainInfo.Templates.TryGetValue(tile.Type, out var template))
						palette = ((DefaultTerrainTemplateInfo)template).Palette ?? palette;

					var sprite = tileCache.TileSprite(tile);
					var paletteReference = worldRenderer.Palette(palette);
					spriteLayer.Update(newCell, sprite, paletteReference);
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
