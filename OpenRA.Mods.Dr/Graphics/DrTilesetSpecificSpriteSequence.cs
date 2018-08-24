#region Copyright & License Information
/*
 * Copyright 2007-2018 The OpenRA Developers (see AUTHORS)
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

namespace OpenRA.Mods.Dr.Graphics
{
	public class DrTilesetSpecificSpriteSequenceLoader : DrSpriteSequenceLoader
	{
		public DrTilesetSpecificSpriteSequenceLoader(ModData modData)
			: base(modData)
		{
			var metadata = modData.Manifest.Get<SpriteSequenceFormat>().Metadata;
			MiniYaml yaml;
			//if (metadata.TryGetValue("DefaultSpriteExtension", out yaml))
			//	DefaultSpriteExtension = yaml.Value;
		}

		public override ISpriteSequence CreateSequence(ModData modData, TileSet tileSet, SpriteCache cache, string sequence, string animation, MiniYaml info)
		{
			return new DrTilesetSpecificSpriteSequence(modData, tileSet, cache, this, sequence, animation, info);
		}
	}

	public class DrTilesetSpecificSpriteSequence : DrSpriteSequence
	{
		public DrTilesetSpecificSpriteSequence(ModData modData, TileSet tileSet, SpriteCache cache, ISpriteSequenceLoader loader, string sequence, string animation, MiniYaml info)
			: base(modData, tileSet, cache, loader, sequence, animation, info) { }

		string ResolveTilesetId(TileSet tileSet, Dictionary<string, MiniYaml> d)
		{
			var tsId = tileSet.Id;

            /*
			MiniYaml yaml;
			if (d.TryGetValue("TilesetOverrides", out yaml))
			{
				var tsNode = yaml.Nodes.FirstOrDefault(n => n.Key == tsId);
				if (tsNode != null)
					tsId = tsNode.Value.Value;
			}
            */

			return tsId;
		}

		protected override string GetSpriteSrc(ModData modData, TileSet tileSet, string sequence, string animation, string sprite, Dictionary<string, MiniYaml> d)
		{
			var loader = (DrTilesetSpecificSpriteSequenceLoader)Loader;

			var spriteName = sprite ?? sequence;

            var validTilesetIds = new string[] { "JUNGLE", "SNOW" }; // Barren also has tileset-specific graphics but they don't look nice

            if (spriteName.StartsWith("base|") && !spriteName.EndsWith(".shp"))
            {
                if (validTilesetIds.Contains(tileSet.Id))
                    spriteName = spriteName.Replace("base", tileSet.Id.ToLower());
            }

            if (LoadField(d, "AddExtension", true))
			{
				return spriteName + loader.DefaultSpriteExtension;
			}

			return spriteName;
		}
	}
}
