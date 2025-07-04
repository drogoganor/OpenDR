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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common.Terrain;
using OpenRA.Primitives;
using OpenRA.Support;

namespace OpenRA.Mods.Dr.Terrain
{
	public sealed class DrTileCache : IDisposable
	{
		readonly Dictionary<ushort, TheaterTemplate> templates = new();
		readonly Cache<SheetType, SheetBuilder> sheetBuilders;
		readonly MersenneTwister random;

		public DrTileCache(DrTerrain terrainInfo, Action<uint, string> onMissingImage = null)
		{
			sheetBuilders = new Cache<SheetType, SheetBuilder>(t => new SheetBuilder(t, terrainInfo.SheetSize));

			random = new MersenneTwister();

			var frameCache = new FrameCache(Game.ModData.DefaultFileSystem, Game.ModData.SpriteLoaders);
			var allTemplates = new Dictionary<ushort, TerrainTemplateInfo>(terrainInfo.Templates)
				.Union(terrainInfo.EdgeTemplates)
				.ToDictionary(pair => pair.Key, pair => pair.Value);

			foreach (var t in allTemplates)
			{
				var variants = new List<Sprite[]>();
				var templateInfo = (DefaultTerrainTemplateInfo)t.Value;

				for (var ii = 0; ii < templateInfo.Images.Length; ii++)
				{
					var i = templateInfo.Images[ii];

					ISpriteFrame[] allFrames;
					ISpriteFrame[] depthFrames = null;

					if (onMissingImage != null)
					{
						try
						{
							allFrames = frameCache[i];
						}
						catch (FileNotFoundException)
						{
							onMissingImage(t.Key, i);
							continue;
						}
					}
					else
						allFrames = frameCache[i];

					if (terrainInfo.EnableDepth && templateInfo.DepthImages != null && templateInfo.DepthImages.Length == templateInfo.Images.Length)
					{
						var di = templateInfo.DepthImages[ii];
						if (onMissingImage != null)
						{
							try
							{
								depthFrames = frameCache[di];
							}
							catch (FileNotFoundException)
							{
								onMissingImage(t.Key, di);
								continue;
							}
						}
						else
							depthFrames = frameCache[di];
					}

					var frameCount = terrainInfo.EnableDepth && depthFrames == null ? allFrames.Length / 2 : allFrames.Length;
					var indices = templateInfo.Frames ?? Exts.MakeArray(t.Value.TilesCount, j => j);

					var start = indices.Min();
					var end = indices.Max();
					if (start < 0 || end >= frameCount)
						throw new YamlException($"Template `{t.Key}` uses frames [{start}..{end}] of {i}, but only [0..{frameCount - 1}] actually exist");

					variants.Add(indices.Select(j =>
					{
						var f = allFrames[j];
						var tile = t.Value.Contains(j) ? (DefaultTerrainTileInfo)t.Value[j] : null;

						// The internal z axis is inverted from expectation (negative is closer)
						var zOffset = tile != null ? -tile.ZOffset : 0;
						var zRamp = tile != null ? tile.ZRamp : 1f;
						var offset = new float3(f.Offset, zOffset);
						var type = SheetBuilder.FrameTypeToSheetType(f.Type);

						var s = sheetBuilders[type].Allocate(f.Size, zRamp, offset);
						Util.FastCopyIntoChannel(s, f.Data, f.Type);

						if (terrainInfo.EnableDepth)
						{
							var depthFrame = depthFrames != null ? depthFrames[j] : allFrames[j + frameCount];
							var depthType = SheetBuilder.FrameTypeToSheetType(depthFrame.Type);
							var ss = sheetBuilders[depthType].Allocate(depthFrame.Size, zRamp, offset);
							Util.FastCopyIntoChannel(ss, depthFrame.Data, depthFrame.Type);
							s = new SpriteWithSecondaryData(s, ss.Sheet, ss.Bounds, ss.Channel);
						}

						return s;
					}).ToArray());
				}

				var allSprites = variants.SelectMany(s => s);

				// Ignore the offsets baked into R8 sprites
				if (terrainInfo.IgnoreTileSpriteOffsets)
					allSprites = allSprites.Select(s => new Sprite(s.Sheet, s.Bounds, s.ZRamp, new float3(float2.Zero, s.Offset.Z), s.Channel, s.BlendMode));

				if (onMissingImage != null && variants.Count == 0)
					continue;

				templates.Add(t.Value.Id, new TheaterTemplate(allSprites.ToArray(), variants[0].Length, templateInfo.Images.Length));
			}

			// 1x1px transparent tile
			var missingDataLength = 1;
			var missingFrameType = SpriteFrameType.Indexed8;
			var missingSheetType = SheetType.Indexed;

			// Avoid creating an indexed sheet if all tiles are BGRA
			var missing = sheetBuilders.FirstOrDefault();
			if (missing.Value != null && missing.Key == SheetType.BGRA)
			{
				missingDataLength = 4;
				missingFrameType = SpriteFrameType.Bgra32;
				missingSheetType = SheetType.BGRA;
			}

			MissingTile = sheetBuilders[missingSheetType].Add(new byte[missingDataLength], missingFrameType, new Size(1, 1));
			foreach (var sb in sheetBuilders.Values)
				sb.Current.ReleaseBuffer();
		}

		public bool HasTileSprite(TerrainTile r, int? variant = null)
		{
			return TileSprite(r, variant) != MissingTile;
		}

		public Sprite TileSprite(TerrainTile r, int? variant = null)
		{
			if (!templates.TryGetValue(r.Type, out var template))
				return MissingTile;

			if (r.Index >= template.Stride)
				return MissingTile;

			var start = template.Variants > 1 ? variant ?? random.Next(template.Variants) : 0;
			return template.Sprites[start * template.Stride + r.Index];
		}

		public Sprite MissingTile { get; }

		public void Dispose()
		{
			foreach (var sb in sheetBuilders.Values)
				sb.Dispose();
		}
	}
}
