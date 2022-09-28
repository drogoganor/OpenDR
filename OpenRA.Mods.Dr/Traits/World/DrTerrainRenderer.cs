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
using System.IO;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Terrain;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Dr.Traits
{
	[TraitLocation(SystemActors.World | SystemActors.EditorWorld)]
	public class DrTerrainRendererInfo : TraitInfo, ITiledTerrainRendererInfo
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

		[FieldLoader.LoadUsing("LoadEdges")]
		public Dictionary<string, DrTerrainEdgeInfo> Edges;

		static object LoadEdges(MiniYaml yaml)
		{
			var retList = new Dictionary<string, DrTerrainEdgeInfo>();
			var shorelines = yaml.Nodes.First(x => x.Key == "Edges");
			foreach (var node in shorelines.Value.Nodes.Where(n => n.Key.StartsWith("EdgeMatch")))
			{
				var ret = new DrTerrainEdgeInfo();
				FieldLoader.Load(ret, node.Value);
				retList.Add(node.Key, ret);
			}

			return retList;
		}

		bool ITiledTerrainRendererInfo.ValidateTileSprites(ITemplatedTerrainInfo terrainInfo, Action<string> onError)
		{
			var missingImages = new HashSet<string>();
			var failed = false;
			Action<uint, string> onMissingImage = (id, f) =>
			{
				onError($"\tTemplate `{id}` references sprite `{f}` that does not exist.");
				missingImages.Add(f);
				failed = true;
			};

			var tileCache = new DefaultTileCache((DefaultTerrain)terrainInfo, onMissingImage);
			foreach (var t in terrainInfo.Templates)
			{
				var templateInfo = (DefaultTerrainTemplateInfo)t.Value;
				for (var v = 0; v < templateInfo.Images.Length; v++)
				{
					if (!missingImages.Contains(templateInfo.Images[v]))
					{
						for (var i = 0; i < t.Value.TilesCount; i++)
						{
							if (t.Value[i] == null || tileCache.HasTileSprite(new TerrainTile(t.Key, (byte)i), v))
								continue;

							onError($"\tTemplate `{t.Key}` references frame {i} that does not exist in sprite `{templateInfo.Images[v]}`.");
							failed = true;
						}
					}
				}
			}

			return failed;
		}

		public override object Create(ActorInitializer init) { return new DrTerrainRenderer(init.World, this); }
	}

	public static class DrTerrainHelper
	{
		internal static bool IsLandTile(this TerrainTile tile) { return tile.Type > 0; }
		internal static bool IsSeaTile(this TerrainTile tile) { return tile.Type == 0; }

		internal static bool IsMatch(this TerrainTile tile, ShorelineMatchType match)
		{
			switch (match)
			{
				case ShorelineMatchType.Land:
					if (tile.IsLandTile())
						return true;
					break;
				case ShorelineMatchType.Sea:
					if (tile.IsSeaTile())
						return true;
					break;
			}

			return false;
		}

		internal static bool IsMatch(this TerrainTile tile, ushort type, EdgeMatchType match)
		{
			switch (match)
			{
				case EdgeMatchType.Below:
					if (tile.Type < type)
						return true;
					break;
				case EdgeMatchType.Equal:
					if (tile.Type == type)
						return true;
					break;
			}

			return false;
		}
	}

	public sealed class DrTerrainRenderer : IRenderTerrain, IWorldLoaded, INotifyActorDisposing, ITiledTerrainRenderer
	{
		const int NumEdgeLayers = 15;
		const int EdgeTileStart = 40;
		const int NumEdgeTiles = 12;
		const int NumSkipEdgeTiles = 2;

		readonly Map map;
		readonly DrTerrainRendererInfo info;
		readonly TerrainSpriteLayer[] edgeLayers;
		TerrainSpriteLayer spriteLayer;
		TerrainSpriteLayer shimLayer;

		readonly DefaultTerrain terrainInfo;
		readonly DefaultTileCache tileCache;
		WorldRenderer worldRenderer;
		bool disposed;

		public DrTerrainRenderer(World world, DrTerrainRendererInfo info)
		{
			map = world.Map;
			this.info = info;
			terrainInfo = map.Rules.TerrainInfo as DefaultTerrain;
			if (terrainInfo == null)
				throw new InvalidDataException("TerrainRenderer can only be used with the DefaultTerrain parser");

			tileCache = new DefaultTileCache(terrainInfo);
			edgeLayers = new TerrainSpriteLayer[NumEdgeLayers];
		}

		void IWorldLoaded.WorldLoaded(World world, WorldRenderer wr)
		{
			worldRenderer = wr;
			spriteLayer = new TerrainSpriteLayer(world, wr, tileCache.MissingTile, BlendMode.Alpha, world.Type != WorldType.Editor);
			shimLayer = new TerrainSpriteLayer(world, wr, tileCache.MissingTile, BlendMode.Alpha, world.Type != WorldType.Editor);
			for (var i = 0; i < NumEdgeLayers; i++)
			{
				edgeLayers[i] = new TerrainSpriteLayer(world, wr, tileCache.MissingTile, BlendMode.Alpha, world.Type != WorldType.Editor);
			}

			foreach (var cell in map.AllCells)
			{
				SetBaseCell(cell);
				UpdateShorelineCell(cell);
				UpdateEdgeCell(cell);
			}

			map.Tiles.CellEntryChanged += SetBaseCell;
			map.Height.CellEntryChanged += SetBaseCell;
			map.Tiles.CellEntryChanged += UpdateShorelineCell;
			map.Height.CellEntryChanged += UpdateShorelineCell;
			map.Tiles.CellEntryChanged += UpdateEdgeCell;
			map.Height.CellEntryChanged += UpdateEdgeCell;
		}

		public void UpdateEdgeCell(CPos cell)
		{
			var thisTile = map.Tiles[cell];
			if (thisTile.Type == 0 || thisTile.Type >= 15)
				return;

			var palette = terrainInfo.Palette;
			for (var x = -1; x < 2; x++)
			{
				for (var y = -1; y < 2; y++)
				{
					var newCell = cell + new CVec(x, y);

					if (!map.Contains(newCell))
						continue;

					ClearEdgeTile(newCell);
					var ok = GetEdgeTile(newCell, out var tile);

					if (!ok)
						continue;

					// Do a sum to get the matching base tile type from the edge tile
					var edgeTileType = (int)Math.Floor((double)(tile.Type - EdgeTileStart) / NumEdgeTiles) + NumSkipEdgeTiles;

					// Create shim tile
					if (thisTile.Type == edgeTileType)
					{
						// Use a shim tile of the tile to the northwest
						var shimTilePos = newCell - new CVec(1, 1);
						var shimTile = map.Tiles[shimTilePos];
						shimLayer.Clear(newCell);

						if (terrainInfo.Templates.TryGetValue(tile.Type, out var shimTemplate))
							palette = ((DefaultTerrainTemplateInfo)shimTemplate).Palette ?? palette;

						var shimSprite = tileCache.TileSprite(shimTile);
						var shimPaletteReference = worldRenderer.Palette(palette);
						shimLayer.Update(newCell, shimSprite, shimPaletteReference);
					}
					else
					{
						// Base tile is sufficient
						shimLayer.Clear(newCell);
					}

					if (terrainInfo.Templates.TryGetValue(tile.Type, out var template))
						palette = ((DefaultTerrainTemplateInfo)template).Palette ?? palette;

					var sprite = tileCache.TileSprite(tile);
					var paletteReference = worldRenderer.Palette(palette);

					edgeLayers[thisTile.Type].Update(newCell, sprite, paletteReference);
				}
			}
		}

		void ClearEdgeTile(CPos cell)
		{
			for (var i = 0; i < NumEdgeLayers; i++)
			{
				edgeLayers[i].Clear(cell);
			}
		}

		bool GetEdgeTile(CPos cell, out TerrainTile result)
		{
			var tile = map.Tiles[cell];
			var matchEdges = info.Edges.Values;
			var numIndices = 1;

			foreach (var m in matchEdges)
			{
				var match = true;
				var count = 0;
				foreach (var n in m.Neighbors.Values)
				{
					var neighborPos = cell + n.Offset;
					if (map.Tiles.Contains(neighborPos))
					{
						count++;
						var neighbour = map.Tiles[neighborPos];
						match = neighbour.IsMatch(tile.Type, n.Match);
						if (!match)
							break;
					}
				}

				if (match)
				{
					result = new TerrainTile(m.SetType, (byte)Game.CosmeticRandom.Next(numIndices));
					return true;
				}
			}

			result = tile;
			return false;
		}

		public void SetBaseCell(CPos cell)
		{
			var tile = map.Tiles[cell];
			var palette = terrainInfo.Palette;
			if (terrainInfo.Templates.TryGetValue(tile.Type, out var template))
				palette = ((DefaultTerrainTemplateInfo)template).Palette ?? palette;

			var sprite = tileCache.TileSprite(tile);
			var paletteReference = worldRenderer.Palette(palette);
			spriteLayer.Update(cell, sprite, paletteReference);
		}

		public void UpdateShorelineCell(CPos cell)
		{
			var tile = map.Tiles[cell];
			if (tile.Type != 0)
				return;

			for (var x = -1; x < 2; x++)
			{
				for (var y = -1; y < 2; y++)
				{
					var newCell = cell + new CVec(x, y);

					if (!map.Contains(newCell))
						continue;

					tile = GetShoreTile(newCell);

					var palette = terrainInfo.Palette;
					if (terrainInfo.Templates.TryGetValue(tile.Type, out var template))
						palette = ((DefaultTerrainTemplateInfo)template).Palette ?? palette;

					var sprite = tileCache.TileSprite(tile);
					var paletteReference = worldRenderer.Palette(palette);
					spriteLayer.Clear(newCell);
					spriteLayer.Update(newCell, sprite, paletteReference);
				}
			}
		}

		TerrainTile GetShoreTile(CPos cell)
		{
			var tile = map.Tiles[cell];
			var matchShorelines = info.Shorelines.Values.Where(x => tile.IsMatch(x.Match));
			var numIndices = 4;

			foreach (var m in matchShorelines)
			{
				var match = true;
				var count = 0;
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
			shimLayer.Draw(wr.Viewport);

			for (var i = 0; i < NumEdgeLayers; i++)
			{
				edgeLayers[i].Draw(wr.Viewport);
			}

			foreach (var r in wr.World.WorldActor.TraitsImplementing<IRenderOverlay>())
				r.Render(wr);
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			if (disposed)
				return;

			map.Tiles.CellEntryChanged -= UpdateShorelineCell;
			map.Height.CellEntryChanged -= UpdateShorelineCell;

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
					var palette = template.Palette ?? terrainInfo.Palette;

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
					var palette = wr.Palette(template.Palette ?? terrainInfo.Palette);

					yield return new SpriteRenderable(sprite, origin, offset, 0, palette, 1f, 1f, float3.Ones, TintModifiers.None, false);
				}
			}
		}
	}
}
