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

using System.Collections.Generic;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	public class DrResourceTypeInfo : TraitInfo
	{
		[Desc("Resource index used in the binary map data.")]
		public readonly int ResourceType = 1;

		[Desc("Credit value of a single resource unit.")]
		public readonly int ValuePerUnit = 0;

		[FieldLoader.Require]
		[Desc("Resource identifier used by other traits.")]
		public readonly string Type = null;

		[FieldLoader.Require]
		[Desc("Resource name used by tooltips.")]
		public readonly string Name = null;

		[FieldLoader.Require]
		[Desc("Terrain type used to determine unit movement and minimap colors.")]
		public readonly string TerrainType = null;

		[Desc("Terrain types that this resource can spawn on.")]
		public readonly HashSet<string> AllowedTerrainTypes = new HashSet<string>();

		[Desc("Allow resource to spawn under Mobile actors.")]
		public readonly bool AllowUnderActors = false;

		[Desc("Allow resource to spawn under Buildings.")]
		public readonly bool AllowUnderBuildings = false;

		[Desc("Allow resource to spawn on ramp tiles.")]
		public readonly bool AllowOnRamps = false;

		public override object Create(ActorInitializer init) { return new DrResourceType(this, init.World); }
	}

	public class DrResourceType
	{
		public readonly DrResourceTypeInfo Info;

		public DrResourceType(DrResourceTypeInfo info, World world)
		{
			Info = info;
		}
	}
}
