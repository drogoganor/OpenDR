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

using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Dr.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Dr.Widgets.Logic
{
	public class DrResourcesLogic : ChromeLogic
	{
		readonly World world;
		readonly Player player;
		readonly DrPlayerResources resources;

		[ObjectCreator.UseCtor]
		public DrResourcesLogic(Widget widget, World world)
		{
			this.world = world;
			player = world.LocalPlayer;
			resources = player.PlayerActor.Trait<DrPlayerResources>();

			var waterLabelWidget = widget.GetOrNull<LabelWidget>("WATER_LABEL");
			waterLabelWidget.GetText = () => resources.Water.ToString();
		}

		// public override void Tick()
		// {
		// 	displayResources = resources.Water;
		// 	displayLabel = waterLabel.F(displayResources);
		// }
	}
}
