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

using OpenRA.Mods.Dr.Activities;
using OpenRA.Mods.Dr.Traits;
using OpenRA.Scripting;
using OpenRA.Traits;

namespace OpenRA.Mods.Dr.Scripting
{
	[ScriptPropertyGroup("Movement")]
	public class FreighterProperties : ScriptActorProperties, Requires<FreighterInfo>
	{
		public FreighterProperties(ScriptContext context, Actor self)
			: base(context, self)
		{ }

		[ScriptActorPropertyActivity]
		[Desc("Search for nearby resources and begin harvesting.")]
		public void FindResources()
		{
			Self.QueueActivity(new DrFindAndDeliverResources(Self));
		}
	}
}
