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
		class EdgeTileResult
		{
			public TerrainTile Tile;
			public CPos Cell;
			public CVec Offset;
			public bool Self;
		}

		const int NumEdgeLayers = 16;
		const int NumEdgeTiles = 14;
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

			// map.Height.CellEntryChanged += SetBaseCell;
			map.Tiles.CellEntryChanged += UpdateShorelineCell;

			// map.Height.CellEntryChanged += UpdateShorelineCell;
			map.Tiles.CellEntryChanged += UpdateEdgeCell;

			// map.Height.CellEntryChanged += UpdateEdgeCell;
		}

		public void UpdateEdgeCell(CPos cell)
		{
			var thisTile = map.Tiles[cell];
			if (thisTile.Type == 0 || thisTile.Type >= 15)
				return;

			for (var x = -1; x < 2; x++)
			{
				for (var y = -1; y < 2; y++)
				{
					var newCell = cell + new CVec(x, y);

					if (!map.Contains(newCell))
						continue;

					var edges = GetEdgeTiles(newCell);
					CreateEdgesForMatches(edges);
				}
			}
		}

		void CreateEdgesForMatches(EdgeTileResult[] edges)
		{
			ushort? shimType = null;
			string palette = null;
			var self = edges.First(x => x.Self == true);
			var tile = self.Tile;
			var matchEdges = info.Edges.Values;

			// NOTE: The difference between the SelfMatch of Equal and Below:
			// When set to Equal, the current value of the tile should be used.
			// When set to Below, the value is Equal to the highest Above neighbor.
			// We need to calculate the highest Equal neighbor value before determining which neighbors are above or below.
			ClearEdgeTile(self.Cell);
			shimLayer.Clear(self.Cell);

			var usedEdges = new HashSet<ushort>();

			// Go through each match set
			foreach (var matchEdge in matchEdges)
			{
				var selfValue = tile.Type;
				var isSelfMatchBelow = matchEdge.SelfMatchType == EdgeMatchType.Below;
				var validLowestEqualNeighbor = GetLowestEqualNeighbor(edges, matchEdge, out var highestValue);
				if (isSelfMatchBelow && validLowestEqualNeighbor)
				{
					selfValue = highestValue.Value;

					// Bomb out if our highest neighbour is 0 or 1, or equal to the current tile type
					if (highestValue.Value < NumSkipEdgeTiles || highestValue.Value == tile.Type)
						continue;
				}

				// We must track the lowest value for Equal node matches
				ushort? lowestMatchValue = null;

				var allDependentTilesMatch = true;

				// Go through each neighbor in the set
				var matchNeighbors = matchEdge.Neighbors.Values;
				var index = 0;
				foreach (var matchNeighbor in matchNeighbors)
				{
					// TODO: Dodgy association here by index, but should be ordered in terrainrenderer.yaml
					var neighbour = edges.First(x => x.Offset == matchNeighbor.Offset);

					// var neighbour = edges[index];
					if (matchNeighbor.Match == EdgeMatchType.Below)
					{
						if (neighbour.Tile.Type >= selfValue)
						{
							allDependentTilesMatch = false;
							break;
						}

						// Get first below neighbor as shim
						if (!isSelfMatchBelow && (shimType == null || neighbour.Tile.Type < shimType))
						{
							shimType = neighbour.Tile.Type;
						}
					}
					else if (matchNeighbor.Match == EdgeMatchType.Equal)
					{
						if (neighbour.Tile.Type < selfValue)
						{
							allDependentTilesMatch = false;
							break;
						}

						if ((!lowestMatchValue.HasValue || neighbour.Tile.Type < lowestMatchValue.Value) && neighbour.Tile.Type >= NumSkipEdgeTiles)
						{
							lowestMatchValue = neighbour.Tile.Type;

							if (lowestMatchValue.Value == 0 || lowestMatchValue.Value > 15)
							{
								throw new Exception("WTF");
							}
						}
					}

					index++;
				}

				if (!allDependentTilesMatch)
					continue;

				// If our lowest Above neighbour is higher than ourself, use ourself
				if (!isSelfMatchBelow && lowestMatchValue.HasValue && lowestMatchValue.Value > tile.Type)
					lowestMatchValue = tile.Type;

				// If we don't have a lowest match value, it's ourself
				if (lowestMatchValue == null)
					lowestMatchValue = tile.Type;

				// Only allow one match per tile type
				if (usedEdges.Contains(lowestMatchValue.Value))
					continue;

				usedEdges.Add(lowestMatchValue.Value);

				// Match is good at this point; create edge tile

				// Get the right edge type for this tile type by adding the result tile type to the base tile type multiplied by how many edge tiles we have
				var resultTileType = (ushort)(matchEdge.SetType + ((lowestMatchValue - NumSkipEdgeTiles) * NumEdgeTiles));

				var edgeTile = new TerrainTile(resultTileType, 0);

				if (terrainInfo.Templates.TryGetValue(edgeTile.Type, out var template))
					palette = ((DefaultTerrainTemplateInfo)template).Palette ?? terrainInfo.Palette;

				var sprite = tileCache.TileSprite(edgeTile);
				var paletteReference = worldRenderer.Palette(palette);

				edgeLayers[lowestMatchValue.Value].Update(self.Cell, sprite, paletteReference);
			}

			if (shimType != null)
			{
				if (terrainInfo.Templates.TryGetValue(shimType.Value, out var shimTemplate))
					palette = ((DefaultTerrainTemplateInfo)shimTemplate).Palette ?? terrainInfo.Palette;

				var shimTile = new TerrainTile(shimType.Value, tile.Index);
				var shimSprite = tileCache.TileSprite(shimTile);
				var shimPaletteReference = worldRenderer.Palette(palette);
				shimLayer.Update(self.Cell, shimSprite, shimPaletteReference);
			}
		}

		bool GetHighestEqualNeighbor(EdgeTileResult[] edges, DrTerrainEdgeInfo edgeInfo, out ushort? highestValue)
		{
			highestValue = null;
			foreach (var edge in edges)
			{
				if (edge.Self == true)
					continue;

				var matchedEdge = edgeInfo.Neighbors.Values
					.SingleOrDefault(x => x.Offset == edge.Offset && x.Match == EdgeMatchType.Equal);

				if (matchedEdge == null)
					continue;

				if (!highestValue.HasValue || edge.Tile.Type > highestValue.Value)
				{
					highestValue = edge.Tile.Type;
				}
			}

			return highestValue.HasValue;
		}

		bool GetLowestEqualNeighbor(EdgeTileResult[] edges, DrTerrainEdgeInfo edgeInfo, out ushort? lowestValue)
		{
			lowestValue = null;
			foreach (var edge in edges)
			{
				if (edge.Self == true)
					continue;

				var matchedEdge = edgeInfo.Neighbors.Values
					.SingleOrDefault(x => x.Offset == edge.Offset && x.Match == EdgeMatchType.Equal);

				if (matchedEdge == null)
					continue;

				if (!lowestValue.HasValue || edge.Tile.Type < lowestValue.Value)
				{
					lowestValue = edge.Tile.Type;
				}
			}

			return lowestValue.HasValue;
		}

		Dictionary<ushort, List<CPos>> GetTileNeighborPossibilities(EdgeTileResult[] edges)
		{
			// Get above tile positions.
			// We need to create transparent edge tiles for potentially more than one set of joins.
			var highTilePositions = new Dictionary<ushort, List<CPos>>();
			foreach (var edge in edges)
			{
				if (edge.Self == true)
					continue;

				if (!highTilePositions.ContainsKey(edge.Tile.Type))
				{
					highTilePositions.Add(edge.Tile.Type, new List<CPos>() { edge.Cell });
				}
				else
				{
					highTilePositions[edge.Tile.Type].Add(edge.Cell);
				}
			}

			// Fill out all combinations i.e. if we have E:3, N:2, and W:1, we'll get results (E:3), (E:2, N:2), (E:1, N:1, W:1).
			var orderedPositions = highTilePositions.OrderByDescending(x => x.Key);
			var positionIndex = 0;
			foreach (var pair in orderedPositions)
			{
				foreach (var innerPair in orderedPositions.Skip(positionIndex))
				{
					foreach (var pairPosition in pair.Value)
					{
						if (!innerPair.Value.Contains(pairPosition))
						{
							innerPair.Value.Add(pairPosition);
						}
						else
						{
							throw new Exception("why");
						}
					}
				}

				positionIndex++;
			}

			return highTilePositions;
		}

		EdgeTileResult[] GetEdgeTiles(CPos newCell)
		{
			var results = new List<EdgeTileResult>();
			for (var y = -1; y <= 0; y++)
			{
				for (var x = -1; x <= 0; x++)
				{
					var offset = new CVec(x, y);
					var neighborPos = newCell + offset;
					if (map.Tiles.Contains(neighborPos))
					{
						var neighbour = map.Tiles[neighborPos];
						results.Add(new EdgeTileResult
						{
							Cell = neighborPos,
							Offset = offset,
							Tile = neighbour,
							Self = neighborPos == newCell
						});
					}
				}
			}

			return results.ToArray();
		}

		void ClearEdgeTile(CPos cell)
		{
			for (var i = 0; i < NumEdgeLayers; i++)
			{
				edgeLayers[i].Clear(cell);
			}
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
