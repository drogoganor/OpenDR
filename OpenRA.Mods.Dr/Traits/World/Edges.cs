using System.Collections.Generic;
using System.Linq;

namespace OpenRA.Mods.Dr.Traits
{
	[Desc("What type of match is it?")]
	public enum EdgeMatchType
	{
		Below,
		Equal,
	}

	public class DrEdgeNeighborInfo
	{
		[Desc("Position of this tile relative to the target.")]
		public CVec Offset;

		[Desc("Check if this tile matches this condition.")]
		public EdgeMatchType Match;
	}

	[Desc("A tile type, a set of neighbour tile match conditions, and a target tile type to set the tile to if all conditions are met.")]
	public class DrTerrainEdgeInfo
	{
		[Desc("The tile type to inspect.")]
		public EdgeMatchType Match;

		[Desc("The tile type to display if all neighbours match.")]
		public ushort SetType;

		[Desc("Set of neighbour tiles and types to match against.")]
		[FieldLoader.LoadUsing("LoadNeighbors")]
		public Dictionary<string, DrEdgeNeighborInfo> Neighbors;

		static object LoadNeighbors(MiniYaml yaml)
		{
			var retList = new Dictionary<string, DrEdgeNeighborInfo>();
			var neighbors = yaml.Nodes.First(x => x.Key == "Neighbors");
			foreach (var node in neighbors.Value.Nodes.Where(n => n.Key.StartsWith("NeighborMatch")))
			{
				var ret = new DrEdgeNeighborInfo();
				FieldLoader.Load(ret, node.Value);
				retList.Add(node.Key, ret);
			}

			return retList;
		}
	}
}
