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

		protected override string GetSpriteSrc(ModData modData, TileSet tileSet, string sequence, string animation, string sprite, Dictionary<string, MiniYaml> d)
		{
			var loader = (DrTilesetSpecificSpriteSequenceLoader)Loader;

			var spriteName = sprite ?? sequence;

			var validTilesetIds = new string[] { "BARREN", "JUNGLE", "SNOW" }; // Alien?

			if (!spriteName.EndsWith(".shp"))
            {
                if (spriteName.StartsWith("tileset|"))
                {
                    if (validTilesetIds.Contains(tileSet.Id))
                        spriteName = spriteName.Replace("tileset", tileSet.Id.ToLower());
                    else
                        spriteName = spriteName.Replace("tileset", "barren");
                }
                else if (spriteName.StartsWith("tilesetEx|"))
                {
                    if (validTilesetIds.Contains(tileSet.Id))
                        spriteName = spriteName.Replace("tilesetEx", tileSet.Id.ToLower() + "Ex");
                    else
                        spriteName = spriteName.Replace("tilesetEx", "barrenEx");
                }
            }

			if (LoadField(d, "AddExtension", true))
			{
				return spriteName + loader.DefaultSpriteExtension;
			}

			return spriteName;
		}
	}
}
