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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Terrain;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Dr.Traits
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

	public class DrTerrainRendererInfo : TraitInfo
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

		public override object Create(ActorInitializer init) { return new DrTerrainRenderer(init.World, this); }
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

	public sealed class DrTerrainRenderer : IRenderTerrain, IWorldLoaded, INotifyActorDisposing, ITiledTerrainRenderer
	{
		readonly DrTerrainRendererInfo info;
		readonly Map map;
		TerrainSpriteLayer spriteLayer;
		readonly DefaultTerrain terrainInfo;
		readonly DefaultTileCache tileCache;
		WorldRenderer worldRenderer;
		bool disposed;

		public DrTerrainRenderer(World world, DrTerrainRendererInfo info)
		{
			map = world.Map;
			terrainInfo = map.Rules.TerrainInfo as DefaultTerrain;
			if (terrainInfo == null)
				throw new InvalidDataException("TerrainRenderer can only be used with the DefaultTerrain parser");

			tileCache = new DefaultTileCache(terrainInfo);
			this.info = info;
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

		public void UpdateCell(CPos cell)
		{
			for (int x = -1; x < 2; x++)
			{
				for (int y = -1; y < 2; y++)
				{
					var newCell = cell + new CVec(x, y);

					if (!map.Contains(newCell))
						continue;

					var tile = GetShoreTile(newCell);
					var palette = TileSet.TerrainPaletteInternalName;
					if (terrainInfo.Templates.TryGetValue(tile.Type, out var template))
						palette = ((DefaultTerrainTemplateInfo)template).Palette ?? palette;

					var sprite = tileCache.TileSprite(tile);
					var paletteReference = worldRenderer.Palette(palette);
					spriteLayer.Update(cell, sprite, paletteReference);
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
					break;
				}
			}

			return tile;
		}

		void IRenderTerrain.RenderTerrain(WorldRenderer wr, Viewport viewport)
		{
			spriteLayer.Draw(wr.Viewport);

			foreach (var r in wr.World.WorldActor.TraitsImplementing<IRenderOverlay>())
				r.Render(wr);
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

		Sprite ITiledTerrainRenderer.MissingTile => tileCache.MissingTile;

		Sprite ITiledTerrainRenderer.TileSprite(TerrainTile r, int? variant)
		{
			return tileCache.TileSprite(r, variant);
		}

		Rectangle ITiledTerrainRenderer.TemplateBounds(TerrainTemplateInfo template)
		{
			Rectangle? templateRect = null;
			var tileSize = map.Grid.TileSize;

			var i = 0;
			for (var y = 0; y < template.Size.Y; y++)
			{
				for (var x = 0; x < template.Size.X; x++)
				{
					var tile = new TerrainTile(template.Id, (byte)(i++));
					if (!terrainInfo.TryGetTileInfo(tile, out var tileInfo))
						continue;

					var sprite = tileCache.TileSprite(tile);
					var u = map.Grid.Type == MapGridType.Rectangular ? x : (x - y) / 2f;
					var v = map.Grid.Type == MapGridType.Rectangular ? y : (x + y) / 2f;

					var tl = new float2(u * tileSize.Width, (v - 0.5f * tileInfo.Height) * tileSize.Height) - 0.5f * sprite.Size;
					var rect = new Rectangle((int)(tl.X + sprite.Offset.X), (int)(tl.Y + sprite.Offset.Y), (int)sprite.Size.X, (int)sprite.Size.Y);
					templateRect = templateRect.HasValue ? Rectangle.Union(templateRect.Value, rect) : rect;
				}
			}

			return templateRect ?? Rectangle.Empty;
		}

		IEnumerable<IRenderable> ITiledTerrainRenderer.RenderUIPreview(WorldRenderer wr, TerrainTemplateInfo t, int2 origin, float scale)
		{
			if (!(t is DefaultTerrainTemplateInfo template))
				yield break;

			var ts = map.Grid.TileSize;
			var gridType = map.Grid.Type;

			var i = 0;
			for (var y = 0; y < template.Size.Y; y++)
			{
				for (var x = 0; x < template.Size.X; x++)
				{
					var tile = new TerrainTile(template.Id, (byte)i++);
					if (!terrainInfo.TryGetTileInfo(tile, out var tileInfo))
						continue;

					var sprite = tileCache.TileSprite(tile, 0);
					var u = gridType == MapGridType.Rectangular ? x : (x - y) / 2f;
					var v = gridType == MapGridType.Rectangular ? y : (x + y) / 2f;
					var offset = (new float2(u * ts.Width, (v - 0.5f * tileInfo.Height) * ts.Height) - 0.5f * sprite.Size.XY).ToInt2();
					var palette = template.Palette ?? TileSet.TerrainPaletteInternalName;

					yield return new UISpriteRenderable(sprite, WPos.Zero, origin + offset, 0, wr.Palette(palette), scale);
				}
			}
		}

		IEnumerable<IRenderable> ITiledTerrainRenderer.RenderPreview(WorldRenderer wr, TerrainTemplateInfo t, WPos origin)
		{
			if (!(t is DefaultTerrainTemplateInfo template))
				yield break;

			var i = 0;
			for (var y = 0; y < template.Size.Y; y++)
			{
				for (var x = 0; x < template.Size.X; x++)
				{
					var tile = new TerrainTile(template.Id, (byte)i++);
					if (!terrainInfo.TryGetTileInfo(tile, out var tileInfo))
						continue;

					var sprite = tileCache.TileSprite(tile, 0);
					var offset = map.Offset(new CVec(x, y), tileInfo.Height);
					var palette = wr.Palette(template.Palette ?? TileSet.TerrainPaletteInternalName);

					yield return new SpriteRenderable(sprite, origin, offset, 0, palette, 1f, 1f, float3.Ones, TintModifiers.None, false);
				}
			}
		}
	}
}
