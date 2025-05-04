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
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Dr.Traits
{
	[TraitLocation(SystemActors.World)]
	[Desc("Attach this to the world actor.")]
	public class DrResourceLayerInfo : ResourceLayerInfo
	{
		[Desc("Resource value multiplier.")]
		public readonly int ResourceValueMultiplier = 1000;

		public override object Create(ActorInitializer init) { return new DrResourceLayer(init.Self, this); }
	}

	public class DrResourceLayer : ResourceLayer
	{
		readonly DrResourceLayerInfo info;
		readonly World world;

		public DrResourceLayer(Actor self, DrResourceLayerInfo info)
			: base(self, info)
		{
			this.info = info;
			world = self.World;
		}

		protected override void WorldLoaded(World w, WorldRenderer wr)
		{
			foreach (var cell in w.Map.AllCells)
			{
				var resource = world.Map.Resources[cell];
				if (!ResourceTypesByIndex.TryGetValue(resource.Type, out var resourceType))
					continue;

				if (!AllowResourceAt(resourceType, cell))
					continue;

				Content[cell] = new ResourceLayerContents(resourceType, resource.Index * info.ResourceValueMultiplier);
			}
		}
	}
}
