using System.Collections.Generic;
using System.Linq;

namespace OpenRA.Mods.Dr.Traits
{
	[Desc("What type of tile is it? Land, Sea, etc.")]
	public enum ShorelineMatchType
	{
		[Desc("Any type of land tile (1-15).")]
		Land,
		[Desc("Sea tile (0).")]
		Sea
	}

	[Desc("A neighbour tile and the TerrainMatchType we expect it to be.")]
	public class DrShorelineNeighborInfo
	{
		[Desc("Position of this tile relative to the target.")]
		public CVec Offset;

		[Desc("Check if this tile matches this condition.")]
		public ShorelineMatchType Match;
	}

	[Desc("A tile type, a set of neighbour tile match conditions, and a target tile type to set the tile to if all conditions are met.")]
	public class DrTerrainShorelineInfo
	{
		[Desc("The tile type to inspect.")]
		public ShorelineMatchType Match;

		[Desc("The tile type to display if all neighbours match.")]
		public ushort SetType;

		[Desc("Set of neighbour tiles and types to match against.")]
		[FieldLoader.LoadUsing("LoadNeighbors")]
		public Dictionary<string, DrShorelineNeighborInfo> Neighbors;

		static object LoadNeighbors(MiniYaml yaml)
		{
			var retList = new Dictionary<string, DrShorelineNeighborInfo>();
			var neighbors = yaml.Nodes.First(x => x.Key == "Neighbors");
			foreach (var node in neighbors.Value.Nodes.Where(n => n.Key.StartsWith("NeighborMatch")))
			{
				var ret = new DrShorelineNeighborInfo();
				FieldLoader.Load(ret, node.Value);
				retList.Add(node.Key, ret);
			}

			return retList;
		}
	}
}
