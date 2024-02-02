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
using Util = OpenRA.Mods.Common.Util;

namespace OpenRA.Mods.Dr.Graphics
{
	public class DrSpriteSequenceLoader : DefaultSpriteSequenceLoader
	{
		public DrSpriteSequenceLoader(ModData modData)
			: base(modData) { }

		public override ISpriteSequence CreateSequence(ModData modData, string tileSet, SpriteCache cache, string image, string sequence,
			MiniYaml data, MiniYaml defaults)
		{
			return new DrSpriteSequence(modData, tileSet, cache, this, image, sequence, data, defaults);
		}
	}

	public class DrSpriteSequence : DefaultSpriteSequence
	{
		public DrSpriteSequence(ModData modData, string tileSet, SpriteCache cache, ISpriteSequenceLoader loader, string image, string sequence, MiniYaml data, MiniYaml defaults)
			: base(cache, loader, image, sequence, data, defaults)
		{
		}

		protected override int GetFacingFrameOffset(WAngle facing)
		{
			return Util.IndexFacing(facing, facings) % facings;
		}
	}
}
