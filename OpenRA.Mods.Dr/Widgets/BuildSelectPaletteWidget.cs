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
using OpenRA.Mods.Common.Lint;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Common.Traits.Render;
using OpenRA.Mods.Common.Widgets;
using OpenRA.Mods.Dr.Orders;
using OpenRA.Mods.Dr.Traits;
using OpenRA.Network;
using OpenRA.Primitives;
using OpenRA.Widgets;

#nullable disable

namespace OpenRA.Mods.Dr.Widgets
{
	public class BuildSelectIcon : ProductionIcon
	{
		public ProductionItem Item;
		public BuilderUnit BuilderUnit;
	}

	// Copied from ProductionPaletteWidget.
	public class BuildSelectPaletteWidget : Widget
	{
		public enum ReadyTextStyleOptions { Solid, AlternatingColor, Blinking }
		public readonly ReadyTextStyleOptions ReadyTextStyle = ReadyTextStyleOptions.AlternatingColor;
		public readonly Color ReadyTextAltColor = Color.Gold;
		public readonly int Columns = 3;
		public readonly int2 IconSize = new int2(64, 48);
		public readonly int2 IconMargin = int2.Zero;
		public readonly int2 IconSpriteOffset = int2.Zero;

		public readonly string ClickSound = ChromeMetrics.Get<string>("ClickSound");
		public readonly string ClickDisabledSound = ChromeMetrics.Get<string>("ClickDisabledSound");
		public readonly string TooltipContainer;
		public readonly string TooltipTemplate = "PRODUCTION_TOOLTIP";

		// Note: LinterHotkeyNames assumes that these are disabled by default
		public readonly string HotkeyPrefix = null;
		public readonly int HotkeyCount = 0;
		public readonly HotkeyReference SelectProductionBuildingHotkey = new HotkeyReference();

		public readonly string ClockAnimation = "clock";
		public readonly string ClockSequence = "idle";
		public readonly string ClockPalette = "chrome";

		public readonly string NotBuildableAnimation = "clock";
		public readonly string NotBuildableSequence = "idle";
		public readonly string NotBuildablePalette = "chrome";

		public readonly string OverlayFont = "TinyBold";
		public readonly string SymbolsFont = "Symbols";

		public readonly bool DrawTime = true;

		public readonly string ReadyText = "";

		public readonly string HoldText = "";

		public readonly string InfiniteSymbol = "\u221E";

		public int DisplayedIconCount { get; private set; }
		public int TotalIconCount { get; private set; }
		public event Action<int, int> OnIconCountChanged = (a, b) => { };

		public BuildSelectIcon TooltipIcon { get; private set; }
		public Func<BuildSelectIcon> GetTooltipIcon;
		public readonly World World;
		readonly ModData modData;
		readonly OrderManager orderManager;

		public int MinimumRows = 4;
		public int MaximumRows = int.MaxValue;

		public int IconRowOffset = 0;
		public int MaxIconRowOffset = int.MaxValue;

		readonly Lazy<TooltipContainerWidget> tooltipContainer;
		BuilderUnit currentQueue;
		HotkeyReference[] hotkeys;

		public BuilderUnit CurrentQueue
		{
			get => currentQueue;
			set
			{
				currentQueue = value;
				if (currentQueue != null)
					UpdateCachedProductionIconOverlays();

				RefreshIcons();
			}
		}

		public override Rectangle EventBounds { get { return eventBounds; } }
		Dictionary<Rectangle, BuildSelectIcon> icons = new();
		readonly Animation cantBuild;
		readonly Animation clock;
		Rectangle eventBounds = Rectangle.Empty;

		readonly WorldRenderer worldRenderer;

		SpriteFont overlayFont, symbolFont;
		float2 iconOffset, holdOffset, readyOffset, timeOffset, queuedOffset, infiniteOffset;

		Player cachedQueueOwner;
		IProductionIconOverlay[] pios;

		[CustomLintableHotkeyNames]
		public static IEnumerable<string> LinterHotkeyNames(MiniYamlNode widgetNode, Action<string> emitError)
		{
			var prefix = "";
			var prefixNode = widgetNode.Value.Nodes.FirstOrDefault(n => n.Key == "HotkeyPrefix");
			if (prefixNode != null)
				prefix = prefixNode.Value.Value;

			var count = 0;
			var countNode = widgetNode.Value.Nodes.FirstOrDefault(n => n.Key == "HotkeyCount");
			if (countNode != null)
				count = FieldLoader.GetValue<int>("HotkeyCount", countNode.Value.Value);

			if (count == 0)
				return Array.Empty<string>();

			if (string.IsNullOrEmpty(prefix))
				emitError($"{widgetNode.Location} must define HotkeyPrefix if HotkeyCount > 0.");

			return Exts.MakeArray(count, i => prefix + (i + 1).ToString("D2"));
		}

		[ObjectCreator.UseCtor]
		public BuildSelectPaletteWidget(ModData modData, OrderManager orderManager, World world, WorldRenderer worldRenderer)
		{
			this.modData = modData;
			this.orderManager = orderManager;
			World = world;
			this.worldRenderer = worldRenderer;
			GetTooltipIcon = () => TooltipIcon;
			tooltipContainer = Exts.Lazy(() =>
				Ui.Root.Get<TooltipContainerWidget>(TooltipContainer));

			cantBuild = new Animation(world, NotBuildableAnimation);
			cantBuild.PlayFetchIndex(NotBuildableSequence, () => 0);
			clock = new Animation(world, ClockAnimation);
		}

		public override void Initialize(WidgetArgs args)
		{
			base.Initialize(args);

			hotkeys = Exts.MakeArray(HotkeyCount,
				i => modData.Hotkeys[HotkeyPrefix + (i + 1).ToString("D2")]);

			overlayFont = Game.Renderer.Fonts[OverlayFont];
			Game.Renderer.Fonts.TryGetValue(SymbolsFont, out symbolFont);

			iconOffset = 0.5f * IconSize.ToFloat2() + IconSpriteOffset;
			queuedOffset = new float2(4, 2);
			holdOffset = iconOffset - overlayFont.Measure(HoldText) / 2;
			readyOffset = iconOffset - overlayFont.Measure(ReadyText) / 2;

			if (ChromeMetrics.TryGet("InfiniteOffset", out infiniteOffset))
				infiniteOffset += queuedOffset;
			else
				infiniteOffset = queuedOffset;
		}

		public void ScrollDown()
		{
			if (CanScrollDown)
				IconRowOffset++;
		}

		public bool CanScrollDown
		{
			get
			{
				var totalRows = (TotalIconCount + Columns - 1) / Columns;

				return IconRowOffset < totalRows - MaxIconRowOffset;
			}
		}

		public void ScrollUp()
		{
			if (CanScrollUp)
				IconRowOffset--;
		}

		public bool CanScrollUp => IconRowOffset > 0;

		public void ScrollToTop()
		{
			IconRowOffset = 0;
		}

		public IEnumerable<ActorInfo> AllBuildables
		{
			get
			{
				if (CurrentQueue == null)
					return Enumerable.Empty<ActorInfo>();

				return CurrentQueue.AllItems().OrderBy(a => a.TraitInfo<BuildableInfo>().BuildPaletteOrder);
			}
		}

		public override void Tick()
		{
			TotalIconCount = AllBuildables.Count();

			if (CurrentQueue != null && !CurrentQueue.Actor.IsInWorld)
				CurrentQueue = null;

			if (CurrentQueue != null)
			{
				if (CurrentQueue.Actor.Owner != cachedQueueOwner)
					UpdateCachedProductionIconOverlays();

				RefreshIcons();
			}
		}

		public override void MouseEntered()
		{
			if (TooltipContainer != null)
				tooltipContainer.Value.SetTooltip(TooltipTemplate,
					new WidgetArgs() { { "player", World.LocalPlayer }, { "getTooltipIcon", GetTooltipIcon }, { "world", World } });
		}

		public override void MouseExited()
		{
			if (TooltipContainer != null)
				tooltipContainer.Value.RemoveTooltip();
		}

		public override bool HandleMouseInput(MouseInput mi)
		{
			var icon = icons.Where(i => i.Key.Contains(mi.Location))
				.Select(i => i.Value).FirstOrDefault();

			if (mi.Event == MouseInputEvent.Move)
				TooltipIcon = icon;

			if (icon == null)
				return false;

			// Eat mouse-up events
			if (mi.Event != MouseInputEvent.Down)
				return true;

			return HandleEvent(icon, mi.Button, mi.Modifiers);
		}

		bool HandleLeftClick(BuildSelectIcon icon)
		{
			CurrentQueue = icon.BuilderUnit;
			World.OrderGenerator = new BuilderUnitBuildingOrderGenerator(CurrentQueue, icon.Name, worldRenderer);
			return true;
		}

		bool HandleEvent(BuildSelectIcon icon, MouseButton btn, Modifiers modifiers)
		{
			var handled = btn == MouseButton.Left ? HandleLeftClick(icon) : false;

			if (!handled)
				Game.Sound.PlayNotification(World.Map.Rules, World.LocalPlayer, "Sounds", ClickDisabledSound, null);

			return true;
		}

		public override bool HandleKeyPress(KeyInput e)
		{
			if (e.Event == KeyInputEvent.Up || CurrentQueue == null)
				return false;

			if (SelectProductionBuildingHotkey.IsActivatedBy(e))
				return SelectProductionBuilding();

			var batchModifiers = e.Modifiers.HasModifier(Modifiers.Shift) ? Modifiers.Shift : Modifiers.None;

			// HACK: enable production if the shift key is pressed
			e.Modifiers &= ~Modifiers.Shift;
			var toBuild = icons.Values.FirstOrDefault(i => i.Hotkey != null && i.Hotkey.IsActivatedBy(e));
			return toBuild != null ? HandleEvent(toBuild, MouseButton.Left, batchModifiers) : false;
		}

		bool SelectProductionBuilding()
		{
			var viewport = worldRenderer.Viewport;
			var selection = World.Selection;

			if (CurrentQueue == null)
				return true;

			var facility = CurrentQueue.MostLikelyProducer().Actor;

			if (facility == null || facility.OccupiesSpace == null)
				return true;

			if (selection.Actors.Count() == 1 && selection.Contains(facility))
				viewport.Center(selection.Actors);
			else
				selection.Combine(World, new[] { facility }, false, true);

			Game.Sound.PlayNotification(World.Map.Rules, null, "Sounds", ClickSound, null);
			return true;
		}

		void UpdateCachedProductionIconOverlays()
		{
			cachedQueueOwner = CurrentQueue.Actor.Owner;
			pios = cachedQueueOwner.PlayerActor.TraitsImplementing<IProductionIconOverlay>().ToArray();
		}

		public void RefreshIcons()
		{
			icons = new Dictionary<Rectangle, BuildSelectIcon>();
			var producer = CurrentQueue != null ? CurrentQueue.MostLikelyProducer() : default(TraitPair<BuilderUnit>);
			if (CurrentQueue == null || producer.Trait == null)
			{
				if (DisplayedIconCount != 0)
				{
					OnIconCountChanged(DisplayedIconCount, 0);
					DisplayedIconCount = 0;
				}

				return;
			}

			var oldIconCount = DisplayedIconCount;
			DisplayedIconCount = 0;

			var rb = RenderBounds;
			var faction = producer.Trait.Faction;

			foreach (var item in AllBuildables.Skip(IconRowOffset * Columns).Take(MaxIconRowOffset * Columns))
			{
				var x = DisplayedIconCount % Columns;
				var y = DisplayedIconCount / Columns;
				var rect = new Rectangle(rb.X + x * (IconSize.X + IconMargin.X), rb.Y + y * (IconSize.Y + IconMargin.Y), IconSize.X, IconSize.Y);

				var rsi = item.TraitInfo<RenderSpritesInfo>();
				var icon = new Animation(World, rsi.GetImage(item, faction));
				var bi = item.TraitInfo<BuildableInfo>();
				icon.Play(bi.Icon);

				var palette = bi.IconPaletteIsPlayerPalette ? bi.IconPalette + producer.Actor.Owner.InternalName : bi.IconPalette;

				var dummyQueue = currentQueue.Actor.Owner.PlayerActor.TraitsImplementing<ProductionQueue>()
					.First(q => q.Info.Type == "Dummy");

				var pi = new BuildSelectIcon()
				{
					Actor = item,
					Name = item.Name,
					Hotkey = DisplayedIconCount < HotkeyCount ? hotkeys[DisplayedIconCount] : null,
					Sprite = icon.Image,
					Palette = worldRenderer.Palette(palette),
					IconClockPalette = worldRenderer.Palette(ClockPalette),
					IconDarkenPalette = worldRenderer.Palette(NotBuildablePalette),
					Pos = new float2(rect.Location),
					BuilderUnit = currentQueue,
					ProductionQueue = dummyQueue,
				};

				icons.Add(rect, pi);
				DisplayedIconCount++;
			}

			eventBounds = icons.Keys.Union();

			if (oldIconCount != DisplayedIconCount)
				OnIconCountChanged(oldIconCount, DisplayedIconCount);
		}

		public override void Draw()
		{
			timeOffset = iconOffset - overlayFont.Measure(WidgetUtils.FormatTime(0, World.Timestep)) / 2;

			if (CurrentQueue == null)
				return;

			var buildableItems = CurrentQueue.BuildableItems();

			// Icons
			Game.Renderer.EnableAntialiasingFilter();
			foreach (var icon in icons.Values)
			{
				WidgetUtils.DrawSpriteCentered(icon.Sprite, icon.Palette, icon.Pos + iconOffset);

				// Draw the ProductionIconOverlay's sprite
				foreach (var pio in pios.Where(p => p.IsOverlayActive(icon.Actor)))
					WidgetUtils.DrawSpriteCentered(pio.Sprite, worldRenderer.Palette(pio.Palette), icon.Pos + iconOffset + pio.Offset(IconSize));

				//var pio = pios.FirstOrDefault(p => p.IsOverlayActive(icon.Actor));
				//if (pio != null)
				//	WidgetUtils.DrawSpriteCentered(pio.Sprite, worldRenderer.Palette(pio.Palette), icon.Pos + iconOffset + pio.Offset(IconSize));

				if (!buildableItems.Any(a => a.Name == icon.Name))
					WidgetUtils.DrawSpriteCentered(cantBuild.Image, icon.IconDarkenPalette, icon.Pos + iconOffset);
			}

			Game.Renderer.DisableAntialiasingFilter();

			// Overlays
			foreach (var icon in icons.Values)
			{
				//var total = icon.Queued.Count;
				//if (total > 0)
				//{
				//	var first = icon.Queued[0];
					//var waiting = !CurrentQueue.IsProducing(first) && !first.Done;
					//if (first.Done)
					//{
						//if (ReadyTextStyle == ReadyTextStyleOptions.Solid || orderManager.LocalFrameNumber * worldRenderer.World.Timestep / 360 % 2 == 0)
						//	overlayFont.DrawTextWithContrast(ReadyText, icon.Pos + readyOffset, Color.White, Color.Black, 1);
						//else if (ReadyTextStyle == ReadyTextStyleOptions.AlternatingColor)
						//	overlayFont.DrawTextWithContrast(ReadyText, icon.Pos + readyOffset, ReadyTextAltColor, Color.Black, 1);
					//}
					//else if (first.Paused)
					//	overlayFont.DrawTextWithContrast(HoldText,
					//		icon.Pos + holdOffset,
					//		Color.White, Color.Black, 1);
					//else if (!waiting && DrawTime)
					//	overlayFont.DrawTextWithContrast(WidgetUtils.FormatTime(first.Queue.RemainingTimeActual(first), World.Timestep),
					//		icon.Pos + timeOffset,
					//		Color.White, Color.Black, 1);

					//if (first.Infinite && symbolFont != null)
					//	symbolFont.DrawTextWithContrast(InfiniteSymbol,
					//		icon.Pos + infiniteOffset,
					//		Color.White, Color.Black, 1);
					//else if (total > 1 || waiting)
					//	overlayFont.DrawTextWithContrast(total.ToString(),
					//		icon.Pos + queuedOffset,
					//		Color.White, Color.Black, 1);
				//}
			}
		}

		public override string GetCursor(int2 pos)
		{
			var icon = icons.Where(i => i.Key.Contains(pos))
				.Select(i => i.Value).FirstOrDefault();

			return icon != null ? base.GetCursor(pos) : null;
		}
	}
}
