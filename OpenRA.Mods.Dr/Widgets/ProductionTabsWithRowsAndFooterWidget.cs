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

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Widgets;

namespace OpenRA.Mods.Dr.Widgets
{
	public class ProductionTabsWithRowsAndFooterWidget : ProductionTabsWidget
	{
		//readonly World world;

		public readonly string RowWidget = null;
		public readonly string FooterWidget = null;
		
		//Lazy<ProductionPaletteWidget> paletteWidget;

		[ObjectCreator.UseCtor]
		public ProductionTabsWithRowsAndFooterWidget(World world) : base(world)
		{
			//this.world = world;
			
			//paletteWidget = Exts.Lazy(() => Ui.Root.Get<ProductionPaletteWidget>(PaletteWidget));
		}
	}
}
