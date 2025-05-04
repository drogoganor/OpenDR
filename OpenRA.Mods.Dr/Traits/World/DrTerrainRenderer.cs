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
using OpenRA.Mods.Dr.Terrain;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Dr.Traits
{
	[TraitLocation(SystemActors.World | SystemActors.EditorWorld)]
	public class DrTerrainRendererInfo : TraitInfo, ITiledTerrainRendererInfo
	{
		[FieldLoader.LoadUsing("LoadEdges")]
		public Dictionary<string, DrTerrainEdgeInfo> Edges;

		[FieldLoader.LoadUsing("LoadShadowEdges")]
		public Dictionary<string, DrTerrainEdgeInfo> ShadowEdges;

#pragma warning disable IDE0051 // Remove unused private members
		static object LoadEdges(MiniYaml yaml)
#pragma warning restore IDE0051 // Remove unused private members
		{
			var retList = new Dictionary<string, DrTerrainEdgeInfo>();
			var shorelines = yaml.Nodes.First(x => x.Key == "Edges");
			foreach (var node in shorelines.Value.Nodes.Where(n => n.Key.StartsWith("EdgeMatch", StringComparison.InvariantCulture)))
			{
				var ret = new DrTerrainEdgeInfo();
				FieldLoader.Load(ret, node.Value);
				retList.Add(node.Key, ret);
			}

			return retList;
		}

#pragma warning disable IDE0051 // Remove unused private members
		static object LoadShadowEdges(MiniYaml yaml)
#pragma warning restore IDE0051 // Remove unused private members
		{
			var retList = new Dictionary<string, DrTerrainEdgeInfo>();
			var shorelines = yaml.Nodes.First(x => x.Key == "ShadowEdges");
			foreach (var node in shorelines.Value.Nodes.Where(n => n.Key.StartsWith("EdgeMatch", StringComparison.InvariantCulture)))
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
			void OnMissingImage(uint id, string f)
			{
				onError($"\tTemplate `{id}` references sprite `{f}` that does not exist.");
				missingImages.Add(f);
				failed = true;
			}

			var tileCache = new DrTileCache((DrTerrain)terrainInfo, OnMissingImage);
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

		[Desc("Whether to render the elevation shadowing (incomplete).")]
		public readonly bool RenderShadows = true;

		public override object Create(ActorInitializer init) { return new DrTerrainRenderer(init.World, this); }
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

		const int NumShadowLayers = 2;
		const int NumEdgeLayers = 16;
		const int NumEdgeTiles = 14;
		const int NumSkipEdgeTiles = 1;

		readonly Map map;
		readonly DrTerrainRendererInfo info;
		readonly TerrainSpriteLayer[] edgeLayers;
		TerrainSpriteLayer spriteLayer;
		TerrainSpriteLayer shimLayer;
		readonly TerrainSpriteLayer[] shadowLayers;

		readonly CellLayer<TerrainTile>[] shadowTiles;

		readonly DrTerrain terrainInfo;
		readonly DrTileCache tileCache;
		WorldRenderer worldRenderer;
		bool disposed;

		public DrTerrainRenderer(World world, DrTerrainRendererInfo info)
		{
			map = world.Map;
			this.info = info;
			terrainInfo = map.Rules.TerrainInfo as DrTerrain;
			if (terrainInfo == null)
				throw new InvalidDataException("TerrainRenderer can only be used with the DefaultTerrain parser");

			shadowTiles = new CellLayer<TerrainTile>[NumShadowLayers];

			for (var i = 0; i < shadowTiles.Length; i++)
			{
				shadowTiles[i] = new CellLayer<TerrainTile>(map);
			}

			tileCache = new DrTileCache(terrainInfo);
			edgeLayers = new TerrainSpriteLayer[NumEdgeLayers];
			shadowLayers = new TerrainSpriteLayer[NumShadowLayers];
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

			for (var i = 0; i < NumShadowLayers; i++)
			{
				shadowLayers[i] = new TerrainSpriteLayer(world, wr, tileCache.MissingTile, BlendMode.Alpha, world.Type != WorldType.Editor);
			}

			foreach (var cell in map.AllCells)
			{
				OnCellEntryChanged(cell);

				// Build shadow tiles
				if (info.RenderShadows)
				{
					CalculateShadowTiles(cell, 0, 1, 1);
					CalculateShadowTiles(cell, 1, 4, 3);
				}
			}

			// Create shadow tile sprites
			if (info.RenderShadows)
			{
				foreach (var cell in map.AllCells)
				{
					UpdateShadowEdgeCell(cell, 0);
					UpdateShadowEdgeCell(cell, 1);
				}
			}

			map.Tiles.CellEntryChanged += OnCellEntryChanged;
		}

		static bool CellInMap(Map map, CPos pos)
		{
			if (pos.X <= 0 || pos.Y <= 0)
				return false;

			if (pos.X >= map.MapSize.X - 1 || pos.Y >= map.MapSize.Y - 1)
				return false;

			return true;
		}

		void CalculateShadowTiles(CPos cell, int shadowLayerIndex, int falloff, int minGradient)
		{
			const ushort ShadowTileType = 254;
			const int MaxElevation = 10;
			var currentElevation = 0;
			var currentCell = cell;
			var scrollIndex = 0;

			do
			{
				var leftCell = currentCell - new CVec(1, 0);
				if (!CellInMap(map, new CPos(leftCell.X, leftCell.Y)) || !CellInMap(map, new CPos(currentCell.X, currentCell.Y)))
					break;

				var leftHeight = map.Height[leftCell];
				var thisHeight = map.Height[currentCell];

				currentElevation += leftHeight - thisHeight;

				// Cap max shadow length
				currentElevation = Math.Min(currentElevation, MaxElevation);

				if (currentElevation > minGradient)
				{
					// Place shadow
					var shadowTile = new TerrainTile(ShadowTileType, 0);

					var shadowLayer = shadowTiles[shadowLayerIndex];
					shadowLayer[currentCell] = shadowTile;
				}

				currentElevation -= falloff;
				currentCell += new CVec(1, 0);
				scrollIndex++;
			}
			while (currentElevation > minGradient);
		}

		EdgeTileResult[] GetShadowEdgeTiles(CPos newCell, int shadowLayerIndex)
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
						var shadowLayer = shadowTiles[shadowLayerIndex];

						var neighbour = shadowLayer[neighborPos];
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

		void UpdateShadowEdgeCell(CPos cell, int shadowLayerIndex)
		{
			if (!CellInMap(map, cell))
				return;

			var edges = GetShadowEdgeTiles(cell, shadowLayerIndex);
			CreateShadowEdgesForMatches(edges, shadowLayerIndex);
		}

		void CreateShadowEdgesForMatches(EdgeTileResult[] edges, int shadowLayerIndex)
		{
			var self = edges.First(x => x.Self);
			var tile = self.Tile;
			var matchEdges = info.ShadowEdges.Values;

			var usedEdges = new HashSet<ushort>();

			// Go through each match set
			var foundMatch = false;
			ushort match = 0;
			foreach (var matchEdge in matchEdges)
			{
				var selfValue = tile.Type;
				var isSelfMatchBelow = matchEdge.SelfMatchType == EdgeMatchType.Below;
				var allDependentTilesMatch = true;

				if (isSelfMatchBelow && GetLowestEqualNeighbor(edges, matchEdge, out var highestValue))
				{
					selfValue = highestValue.Value;

					// Bomb out if our highest neighbour is equal to the current tile type
					if (highestValue.Value == 0 || highestValue.Value == tile.Type)
						continue;
				}

				// Go through each neighbor in the set
				var matchNeighbors = matchEdge.Neighbors.Values;
				var index = 0;
				foreach (var matchNeighbor in matchNeighbors)
				{
					// TODO: Implied association here by index, but will be ok if it's ordered in terrainrenderer.yaml
					// var neighbour = edges.First(x => x.Offset == matchNeighbor.Offset);
					var neighbour = edges[index];

					if (matchNeighbor.Match == EdgeMatchType.Below)
					{
						if (neighbour.Tile.Type >= selfValue)
						{
							allDependentTilesMatch = false;
							break;
						}
					}
					else if (matchNeighbor.Match == EdgeMatchType.Equal)
					{
						if (neighbour.Tile.Type < selfValue)
						{
							allDependentTilesMatch = false;
							break;
						}
					}

					index++;
				}

				if (!allDependentTilesMatch)
					continue;

				// Only allow one match per tile type
				if (usedEdges.Contains(tile.Type))
					continue;

				usedEdges.Add(tile.Type);

				// Match is good
				foundMatch = true;
				match = matchEdge.SetType;
			}

			if (!foundMatch && self.Tile.Type == 254)
				SetShadowTile(self.Cell, 254, shadowLayerIndex);
			else
				SetShadowTile(self.Cell, match, shadowLayerIndex);
		}

		void SetShadowTile(CPos cell, ushort type, int shadowLayerIndex)
		{
			string palette = null;
			var edgeTile = new TerrainTile(type, 0);

			if (terrainInfo.EdgeTemplates.TryGetValue(type, out var template))
				palette = ((DefaultTerrainTemplateInfo)template).Palette ?? terrainInfo.Palette;

			var sprite = tileCache.TileSprite(edgeTile);
			var paletteReference = worldRenderer.Palette(palette);

			shadowLayers[shadowLayerIndex].Update(cell, sprite, paletteReference);
		}

		void OnCellEntryChanged(CPos cell)
		{
			SetBaseCell(cell);
			UpdateEdgeCell(cell);
		}

		void UpdateEdgeCell(CPos cell)
		{
			for (var x = -1; x < 2; x++)
			{
				for (var y = -1; y < 2; y++)
				{
					var newCell = cell + new CVec(x, y);

					if (!CellInMap(map, newCell))
						continue;

					ClearEdgeTile(newCell);
					shimLayer.Clear(newCell);

					var edges = GetEdgeTiles(newCell);
					CreateEdgesForMatches(edges);
				}
			}
		}

		void CreateEdgesForMatches(EdgeTileResult[] edges)
		{
			ushort? shimType = null;
			string palette = null;
			var self = edges.First(x => x.Self);
			var tile = self.Tile;
			var matchEdges = info.Edges.Values;

			ClearEdgeTile(self.Cell);
			shimLayer.Clear(self.Cell);

			var usedEdges = new HashSet<ushort>();

			// Go through each match set
			foreach (var matchEdge in matchEdges)
			{
				var selfValue = tile.Type;
				var isSelfMatchBelow = matchEdge.SelfMatchType == EdgeMatchType.Below;

				if (isSelfMatchBelow && GetLowestEqualNeighbor(edges, matchEdge, out var highestValue))
				{
					selfValue = highestValue.Value;

					// Bomb out if our highest neighbour is 0, or equal to the current tile type
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
					// TODO: Implied association here by index, but will be ok if it's ordered in terrainrenderer.yaml
					// var neighbour = edges.First(x => x.Offset == matchNeighbor.Offset);
					var neighbour = edges[index];

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
								continue;
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
				lowestMatchValue ??= tile.Type;

				// Only allow one match per tile type
				if (usedEdges.Contains(lowestMatchValue.Value))
					continue;

				usedEdges.Add(lowestMatchValue.Value);

				if (lowestMatchValue.HasValue && (lowestMatchValue.Value < 1 || lowestMatchValue.Value >= 240))
					continue;

				// Match is good at this point; create edge tile
				// Get the right edge type for this tile type by adding the result tile type to the base tile type multiplied by how many edge tiles we have
				var resultTileType = (ushort)(matchEdge.SetType + (lowestMatchValue - NumSkipEdgeTiles) * NumEdgeTiles);

				var edgeTile = new TerrainTile(resultTileType, 0);

				if (terrainInfo.EdgeTemplates.TryGetValue(edgeTile.Type, out var template))
					palette = ((DefaultTerrainTemplateInfo)template).Palette ?? terrainInfo.Palette;

				var sprite = tileCache.TileSprite(edgeTile);
				var paletteReference = worldRenderer.Palette(palette);

				edgeLayers[lowestMatchValue.Value].Update(self.Cell, sprite, paletteReference);
			}

			if (shimType != null && shimType != 15)
			{
				if (terrainInfo.Templates.TryGetValue(shimType.Value, out var shimTemplate))
					palette = ((DefaultTerrainTemplateInfo)shimTemplate).Palette ?? terrainInfo.Palette;

				var shimTile = new TerrainTile(shimType.Value, tile.Index);
				var shimSprite = tileCache.TileSprite(shimTile);
				var shimPaletteReference = worldRenderer.Palette(palette);
				shimLayer.Update(self.Cell, shimSprite, shimPaletteReference);
			}
		}

		static bool GetLowestEqualNeighbor(EdgeTileResult[] edges, DrTerrainEdgeInfo edgeInfo, out ushort? lowestValue)
		{
			lowestValue = null;
			foreach (var edge in edges)
			{
				if (edge.Self)
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

		void SetBaseCell(CPos cell)
		{
			var tile = map.Tiles[cell];
			var palette = terrainInfo.Palette;
			if (terrainInfo.Templates.TryGetValue(tile.Type, out var template))
				palette = ((DefaultTerrainTemplateInfo)template).Palette ?? palette;

			var sprite = tileCache.TileSprite(tile);
			var paletteReference = worldRenderer.Palette(palette);
			spriteLayer.Update(cell, sprite, paletteReference);
		}

		void IRenderTerrain.RenderTerrain(WorldRenderer wr, Viewport viewport)
		{
			spriteLayer.Draw(wr.Viewport);
			shimLayer.Draw(wr.Viewport);

			for (var i = 0; i < NumEdgeLayers; i++)
			{
				edgeLayers[i].Draw(wr.Viewport);
			}

			for (var i = 0; i < NumShadowLayers; i++)
			{
				shadowLayers[i].Draw(wr.Viewport);
			}

			foreach (var r in wr.World.WorldActor.TraitsImplementing<IRenderOverlay>())
				r.Render(wr);
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			if (disposed)
				return;

			map.Tiles.CellEntryChanged -= OnCellEntryChanged;

			spriteLayer.Dispose();

			shimLayer.Dispose();

			foreach (var edgeLayer in edgeLayers)
			{
				edgeLayer.Dispose();
			}

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
					var tile = new TerrainTile(template.Id, (byte)i++);
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
			if (t is not DefaultTerrainTemplateInfo template)
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
			if (t is not DefaultTerrainTemplateInfo template)
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
