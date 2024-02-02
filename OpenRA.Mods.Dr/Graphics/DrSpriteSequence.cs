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

using OpenRA.Graphics;
using OpenRA.Mods.Common.Graphics;

namespace OpenRA.Mods.Dr.Graphics
{
	public class DrSpriteSequenceLoader : DefaultSpriteSequenceLoader
	{
		public readonly string DefaultSpriteExtension = ".spr";

		public DrSpriteSequenceLoader(ModData modData)
			: base(modData)
		{
			var metadata = modData.Manifest.Get<SpriteSequenceFormat>().Metadata;
			if (metadata.TryGetValue("DefaultSpriteExtension", out var yaml))
			{
				DefaultSpriteExtension = yaml.Value;
			}
		}

		public override ISpriteSequence CreateSequence(ModData modData, string tileSet, SpriteCache cache, string image, string sequence,
			MiniYaml data, MiniYaml defaults)
		{
			return new DrSpriteSequence(modData, tileSet, cache, this, image, sequence, data, defaults);
		}
	}

	public class DrSpriteSequence : DefaultSpriteSequence
	{
		// TODO: Fix
		//protected override string GetSpriteSrc(ModData modData, string tileSet, string image, string sequence, string sprite, Dictionary<string, MiniYaml> d)
		//{
		//	var loader = (DrSpriteSequenceLoader)Loader;

		//	if (LoadField(d, "AddExtension", true))
		//	{
		//		return (sprite ?? image) + loader.DefaultSpriteExtension;
		//	}

		//	return sprite ?? image;
		//}

		public DrSpriteSequence(ModData modData, string tileSet, SpriteCache cache, ISpriteSequenceLoader loader, string image, string sequence, MiniYaml data, MiniYaml defaults)
			: base(cache, loader, image, sequence, data, defaults)
		{
		}

		public override Sprite GetSprite(int frame, WAngle facing)
		{
			//if (facing.Facing >= 245 && facing.Facing <= 250) // receiving a facing of 320 when unloading an APC
			//	facing = WAngle.Zero;

			return base.GetSprite(frame, facing);
		}
	}
}
