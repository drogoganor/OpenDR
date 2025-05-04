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

using System;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Common.Widgets.Logic;
using OpenRA.Widgets;

namespace OpenRA.Mods.Dr.Widgets.Logic
{
	public class ProductionTabsWithRowsAndFooterLogic : ProductionTabsLogic
	{
		readonly ProductionTabsWithRowsAndFooterWidget tabs;

		[ObjectCreator.UseCtor]
		public ProductionTabsWithRowsAndFooterLogic(Widget widget, World world)
			: base(widget, world)
		{
			tabs = widget.Get<ProductionTabsWithRowsAndFooterWidget>("PRODUCTION_TABS");
			var rowWidget = tabs.Parent.GetOrNull(tabs.RowWidget);
			if (rowWidget != null)
			{
				var palette = tabs.Parent.Get<ProductionPaletteWidget>(tabs.PaletteWidget);
				var foreground = tabs.Parent.Get<ContainerWidget>("PALETTE_FOREGROUND");
				var templates = foreground.Get<ImageWidget>("ROW_TEMPLATE");
				var footer = tabs.Parent.Get<ContainerWidget>(tabs.FooterWidget);
				const int MinRows = 4; // TODO: Get from attribute

				void UpdateBackground(int oldCount, int newCount)
				{
					foreground.RemoveChildren();
					foreground.AddChild(templates);

					var numRows = (int)Math.Ceiling((double)newCount / palette.Columns);
					if (numRows < MinRows)
						numRows = MinRows;
					for (var i = 0; i < numRows; i++)
					{
						var bg = templates.Clone();
						bg.Bounds.X = 0;
						bg.Bounds.Y = palette.IconSize.Y * i;
						foreground.AddChild(bg);
					}

					footer.Bounds.Y = numRows * palette.IconSize.Y;
				}

				palette.OnIconCountChanged += UpdateBackground;

				// Set the initial palette state
				UpdateBackground(0, 0);
			}
		}
	}
}
