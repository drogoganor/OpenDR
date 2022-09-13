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

using OpenRA.Widgets;

namespace OpenRA.Mods.Dr.Widgets.Logic
{
	// Based on ClassicProductionLogic
	// Performs extrajudicial manipulation of the existing production palette's visibility.
	public class BuildingPickerLogic : ChromeLogic
	{
		readonly BuildSelectPaletteWidget palette;
		readonly string productionParentName;

		[ObjectCreator.UseCtor]
		public BuildingPickerLogic(Widget widget, Dictionary<string, MiniYaml> logicArgs)
		{
			palette = widget.Get<BuildSelectPaletteWidget>("BUILD_SELECT_PALETTE");

			MiniYaml yaml;
			if (logicArgs.TryGetValue("ProductionParent", out yaml))
				if (string.IsNullOrWhiteSpace(yaml.Value))
					throw new YamlException("Invalid value for ProductionParent: {0}".F(yaml.Value));

			productionParentName = yaml.Value;

			var background = widget.GetOrNull("PALETTE_BACKGROUND");
			var foreground = widget.GetOrNull("PALETTE_FOREGROUND");

			var sidebarProductionWidget = widget.Parent.Get<Widget>(productionParentName);

			if (background != null || foreground != null)
			{
				Widget backgroundTemplate = null;
				Widget backgroundBottom = null;
				Widget foregroundTemplate = null;

				if (background != null)
				{
					backgroundTemplate = background.Get("ROW_TEMPLATE");
					backgroundBottom = background.GetOrNull("BOTTOM_CAP");
				}

				if (foreground != null)
					foregroundTemplate = foreground.Get("ROW_TEMPLATE");

				Action<int, int> updateBackground = (_, icons) =>
				{
					sidebarProductionWidget.Visible = icons == 0; // TODO: This is where the aformentioned hacking takes place.
					widget.Visible = icons > 0;

					var rows = Math.Max(palette.MinimumRows, (icons + palette.Columns - 1) / palette.Columns);
					rows = Math.Min(rows, palette.MaximumRows);

					if (background != null)
					{
						background.RemoveChildren();

						var rowHeight = backgroundTemplate.Bounds.Height;
						for (var i = 0; i < rows; i++)
						{
							var row = backgroundTemplate.Clone();
							row.Bounds.Y = i * rowHeight;
							background.AddChild(row);
						}

						if (backgroundBottom == null)
							return;

						backgroundBottom.Bounds.Y = rows * rowHeight;
						background.AddChild(backgroundBottom);
					}

					if (foreground != null)
					{
						foreground.RemoveChildren();

						var rowHeight = foregroundTemplate.Bounds.Height;
						for (var i = 0; i < rows; i++)
						{
							var row = foregroundTemplate.Clone();
							row.Bounds.Y = i * rowHeight;
							foreground.AddChild(row);
						}
					}
				};

				palette.OnIconCountChanged += updateBackground;

				// Set the initial palette state
				updateBackground(0, 0);
			}
		}
	}
}
