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

using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;

namespace OpenRA.Mods.Dr.Graphics
{
	public class DrTilesetSpecificSpriteSequenceLoader : DrSpriteSequenceLoader
	{
		public DrTilesetSpecificSpriteSequenceLoader(ModData modData)
			: base(modData) { }

		public override ISpriteSequence CreateSequence(ModData modData, string tileSet, SpriteCache cache, string image, string sequence,
			MiniYaml data, MiniYaml defaults)
		{
			return new DrTilesetSpecificSpriteSequence(modData, tileSet, cache, this, image, sequence, data, defaults);
		}
	}

	public class DrTilesetSpecificSpriteSequence : DrSpriteSequence
	{
		public DrTilesetSpecificSpriteSequence(ModData modData, string tileSet, SpriteCache cache, ISpriteSequenceLoader loader, string image, string sequence, MiniYaml data, MiniYaml defaults)
			: base(modData, tileSet, cache, loader, image, sequence, data, defaults) { }

		protected override IEnumerable<ReservationInfo> ParseFilenames(ModData modData, string tileset, int[] frames, MiniYaml data, MiniYaml defaults)
		{
			var filename = LoadField(Filename, data, defaults, out var location);

			var validTilesetIds = new string[] { "BARREN", "JUNGLE", "SNOW" };
			if (filename.StartsWith("tileset|"))
			{
				if (validTilesetIds.Contains(tileset))
					filename = filename.Replace("tileset", tileset.ToLower());
				else
					filename = filename.Replace("tileset", "barren");
			}

			var loadFrames = CalculateFrameIndices(start, length, stride ?? length ?? 0, facings, frames, transpose, reverseFacings, shadowStart);
			yield return new ReservationInfo(filename, loadFrames, frames, location);
		}
	}
}
