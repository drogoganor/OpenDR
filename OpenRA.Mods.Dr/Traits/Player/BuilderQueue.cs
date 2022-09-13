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

using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.Dr.Traits
{
	public class BuilderQueueInfo : ProductionQueueInfo
	{
		public override object Create(ActorInitializer init) { return new BuilderQueue(init, this); }
	}

	public class BuilderQueue : ProductionQueue
	{
		public BuilderQueue(ActorInitializer init, BuilderQueueInfo info)
			: base(init, info) { }

		protected override void TickInner(Actor self, bool allProductionPaused)
		{
			foreach (var i in Queue)
			{
				i.Tick(playerResources);
			}
		}

		public void BeginProduction(ProductionItem item)
		{
			BeginProduction(item, false);
		}
	}
}
