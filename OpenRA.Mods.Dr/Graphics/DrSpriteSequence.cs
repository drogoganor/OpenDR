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
using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;

namespace OpenRA.Mods.Dr.Graphics
{
    public class DrSpriteSequenceLoader : DefaultSpriteSequenceLoader
    {
        public readonly string DefaultSpriteExtension = ".spr";

		public DrSpriteSequenceLoader(ModData modData) : base(modData)
        {
            var metadata = modData.Manifest.Get<SpriteSequenceFormat>().Metadata;
            MiniYaml yaml;
            if (metadata.TryGetValue("DefaultSpriteExtension", out yaml))
                DefaultSpriteExtension = yaml.Value;
        }

        public override ISpriteSequence CreateSequence(ModData modData, TileSet tileSet, SpriteCache cache, string sequence, string animation, MiniYaml info)
		{
			return new DrSpriteSequence(modData, tileSet, cache, this, sequence, animation, info);
		}
	}

	public class DrSpriteSequence : DefaultSpriteSequence
	{
		protected override string GetSpriteSrc(ModData modData, TileSet tileSet, string sequence, string animation, string sprite, Dictionary<string, MiniYaml> d)
        {
            var loader = (DrSpriteSequenceLoader)Loader;

            if (LoadField(d, "AddExtension", true))
            {
                return (sprite ?? sequence) + loader.DefaultSpriteExtension;
            }

            return sprite ?? sequence;
		}

		public DrSpriteSequence(ModData modData, TileSet tileSet, SpriteCache cache, ISpriteSequenceLoader loader, string sequence, string animation, MiniYaml info)
            : base(modData, tileSet, cache, loader, sequence, animation, info)
		{
		}

		protected override Sprite GetSprite(int start, int frame, int facing)
		{
            if (facing > 256) // receiving a facing of 320 when unloading an APC
                facing = 256;

            var f = (int)(facing * (Facings / 256f));
            var i = (f * Stride) + (frame % Length);
            var j = Frames != null ? Frames[i] : start + i;

            if (j >= sprites.Length)
                j = 0;

			return sprites[j];
		}
	}
}
