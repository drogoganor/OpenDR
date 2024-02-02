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

using System.Collections.Generic;
using System.Linq;
using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Dr.Traits;
using OpenRA.Orders;
using OpenRA.Primitives;
using OpenRA.Traits;
using OpenRA.Widgets;

namespace OpenRA.Mods.Dr.Orders
{
	// Copied from PlaceBuildingOrderGenerator
	public class BuilderUnitBuildingOrderGenerator : IOrderGenerator
	{
		readonly string worldDefaultCursor = ChromeMetrics.Get<string>("WorldDefaultCursor");

		class VariantWrapper
		{
			public readonly ActorInfo ActorInfo;
			public readonly BuildingInfo BuildingInfo;
			public readonly IPlaceBuildingPreview Preview;

			public VariantWrapper(WorldRenderer wr, BuilderUnit queue, ActorInfo ai)
			{
				ActorInfo = ai;
				BuildingInfo = ActorInfo.TraitInfo<BuildingInfo>();

				var previewGeneratorInfo = ActorInfo.TraitInfoOrDefault<IPlaceBuildingPreviewGeneratorInfo>();
				if (previewGeneratorInfo != null)
				{
					string faction;
					var buildableInfo = ActorInfo.TraitInfoOrDefault<BuildableInfo>();
					if (buildableInfo != null && buildableInfo.ForceFaction != null)
						faction = buildableInfo.ForceFaction;
					else
					{
						var mostLikelyProducer = queue.MostLikelyProducer();
						faction = mostLikelyProducer.Trait != null ? mostLikelyProducer.Trait.Faction : queue.Actor.Owner.Faction.InternalName;
					}

					var td = new TypeDictionary()
					{
						new FactionInit(faction),
						new OwnerInit(queue.Actor.Owner),
					};

					foreach (var api in ActorInfo.TraitInfos<IActorPreviewInitInfo>())
						foreach (var o in api.ActorPreviewInits(ActorInfo, ActorPreviewType.PlaceBuilding))
							td.Add(o);

					Preview = previewGeneratorInfo.CreatePreview(wr, ActorInfo, td);
				}
			}
		}

		readonly BuilderUnit queue;
		readonly ActorInfo actorInfo;
		readonly BuildingInfo buildingInfo;
		readonly PlaceBuildingInfo placeBuildingInfo;
		readonly IResourceLayer resourceLayer;
		readonly Viewport viewport;
		readonly VariantWrapper[] variants;
		int variant;

		public BuilderUnitBuildingOrderGenerator(BuilderUnit queue, string name, WorldRenderer worldRenderer)
		{
			var world = queue.Actor.World;
			this.queue = queue;
			placeBuildingInfo = queue.Actor.Owner.PlayerActor.Info.TraitInfo<PlaceBuildingInfo>();
			resourceLayer = world.WorldActor.TraitOrDefault<IResourceLayer>();
			viewport = worldRenderer.Viewport;

			// Clear selection if using Left-Click Orders
			if (Game.Settings.Game.UseClassicMouseStyle)
				world.Selection.Clear();

			var variants = new List<VariantWrapper>()
			{
				new(worldRenderer, queue, world.Map.Rules.Actors[name])
			};

			foreach (var v in variants[0].ActorInfo.TraitInfos<PlaceBuildingVariantsInfo>())
				foreach (var a in v.Actors)
					variants.Add(new VariantWrapper(worldRenderer, queue, world.Map.Rules.Actors[a]));

			this.variants = variants.ToArray();

			var map = world.Map;

			actorInfo = map.Rules.Actors[name];
			buildingInfo = actorInfo.TraitInfo<BuildingInfo>();
		}

		static PlaceBuildingCellType MakeCellType(bool valid, bool lineBuild = false)
		{
			var cell = valid ? PlaceBuildingCellType.Valid : PlaceBuildingCellType.Invalid;
			if (lineBuild)
				cell |= PlaceBuildingCellType.LineBuild;

			return cell;
		}

		public IEnumerable<Order> Order(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			if ((mi.Button == MouseButton.Left && mi.Event == MouseInputEvent.Down) || (mi.Button == MouseButton.Right && mi.Event == MouseInputEvent.Up))
			{
				if (mi.Button == MouseButton.Right)
					world.CancelInputMode();

				var ret = InnerOrder(world, cell, mi).ToArray();

				// If there was a successful placement order
				if (ret.Any(o => o.OrderString == "BuildUnitPlaceBuilding"))
					world.CancelInputMode();

				return ret;
			}

			return Enumerable.Empty<Order>();
		}

		CPos TopLeft
		{
			get
			{
				var offsetPos = Viewport.LastMousePos;
				return viewport.ViewToWorld(offsetPos);
			}
		}

		protected virtual IEnumerable<Order> InnerOrder(World world, CPos cell, MouseInput mi)
		{
			if (world.Paused)
				yield break;

			var owner = queue.Actor.Owner;
			if (mi.Button == MouseButton.Left)
			{
				var orderType = "BuildUnitPlaceBuilding";
				var topLeft = TopLeft;
				var notification = queue.Info.CannotPlaceAudio ?? placeBuildingInfo.CannotPlaceNotification;
				var ai = variants[variant].ActorInfo;
				var bi = variants[variant].BuildingInfo;

				if (!world.CanPlaceBuilding(topLeft, actorInfo, buildingInfo, queue.Actor)
					|| !bi.IsCloseEnoughToBase(world, owner, ai, topLeft))
				{
					Game.Sound.PlayNotification(world.Map.Rules, owner, "Speech", notification, owner.Faction.InternalName);
					TextNotificationsManager.AddTransientLine(placeBuildingInfo.CannotPlaceTextNotification, owner);

					yield break;
				}
				else
				{
					yield return new Order(orderType, owner.PlayerActor, Target.FromCell(world, topLeft), false)
					{
						// Building to place
						TargetString = actorInfo.Name,

						// Actor ID to associate with placement may be quite large, so it gets its own uint
						ExtraData = queue.Actor.ActorID,

						// Actor variant will always be small enough to safely pack in a CPos
						ExtraLocation = topLeft,

						SuppressVisualFeedback = true
					};
				}
			}
		}

		void IOrderGenerator.Tick(World world)
		{
			if (queue == null)
				world.CancelInputMode();
		}

		void IOrderGenerator.SelectionChanged(World world, IEnumerable<Actor> selected) { }

		IEnumerable<IRenderable> IOrderGenerator.Render(WorldRenderer wr, World world) { yield break; }
		IEnumerable<IRenderable> IOrderGenerator.RenderAboveShroud(WorldRenderer wr, World world)
		{
			var topLeft = TopLeft;
			var footprint = new Dictionary<CPos, PlaceBuildingCellType>();
			var activeVariant = variants[variant];
			var buildingInfo = activeVariant.BuildingInfo;
			var preview = activeVariant.Preview;

			var isCloseEnough = buildingInfo.IsCloseEnoughToBase(world, world.LocalPlayer, actorInfo, topLeft);
			foreach (var t in buildingInfo.Tiles(topLeft))
				footprint.Add(t, MakeCellType(isCloseEnough && world.IsCellBuildable(t, actorInfo, buildingInfo) && (resourceLayer == null || resourceLayer.GetResource(t).Type == null)));

			return preview?.Render(wr, topLeft, footprint) ?? Enumerable.Empty<IRenderable>();
		}

		IEnumerable<IRenderable> IOrderGenerator.RenderAnnotations(WorldRenderer wr, World world)
		{
			var preview = variants[variant].Preview;
			return preview?.RenderAnnotations(wr, TopLeft) ?? Enumerable.Empty<IRenderable>();
		}

		public virtual string GetCursor(World world, CPos cell, int2 worldPixel, MouseInput mi)
		{
			return worldDefaultCursor;
		}

		bool IOrderGenerator.HandleKeyPress(KeyInput e)
		{
			if (variants.Length > 0 && placeBuildingInfo.ToggleVariantKey.IsActivatedBy(e))
			{
				if (++variant >= variants.Length)
					variant = 0;

				return true;
			}

			return false;
		}

		void IOrderGenerator.Deactivate() { }
	}
}
