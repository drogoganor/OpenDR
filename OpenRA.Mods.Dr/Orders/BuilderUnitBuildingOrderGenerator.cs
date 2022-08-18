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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Linguini.Syntax.Ast;
using OpenRA.Graphics;
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Graphics;
using OpenRA.Mods.Common.Orders;
using OpenRA.Mods.Common.Traits;
using OpenRA.Mods.Dr.Traits;
using OpenRA.Orders;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Dr.Orders
{
	// Copied from PlaceBuildingOrderGenerator
	public class BuilderUnitBuildingOrderGenerator : IOrderGenerator
	{
		readonly BuilderUnit queue;
		readonly ActorInfo actorInfo;
		readonly BuildingInfo buildingInfo;
		readonly PlaceBuildingInfo placeBuildingInfo;
		readonly FootprintPlaceBuildingPreviewInfo footprintPlaceBuildingPreviewInfo;
		readonly string faction;
		readonly Sprite buildOk;
		readonly Sprite buildBlocked;
		readonly IResourceLayer resourceLayer;
		readonly Viewport viewport;
		readonly WVec centerOffset;
		readonly int2 topLeftScreenOffset;
		IActorPreview[] preview;

		bool initialized;

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

			var map = world.Map;
			var tileset = world.Map.Tileset.ToLowerInvariant();

			actorInfo = map.Rules.Actors[name];
			footprintPlaceBuildingPreviewInfo = actorInfo.TraitInfo<FootprintPlaceBuildingPreviewInfo>();
			buildingInfo = actorInfo.TraitInfo<BuildingInfo>();
			centerOffset = buildingInfo.CenterOffset(world);
			topLeftScreenOffset = -worldRenderer.ScreenPxOffset(centerOffset);

			var buildableInfo = actorInfo.TraitInfo<BuildableInfo>();
			var mostLikelyProducer = queue.MostLikelyProducer();
			faction = buildableInfo.ForceFaction
				?? (mostLikelyProducer.Trait != null ? mostLikelyProducer.Trait.Faction : queue.Actor.Owner.Faction.InternalName);

			if (map.Rules.Sequences.HasSequence("overlay", "build-valid-{0}".F(tileset)))
				buildOk = map.Rules.Sequences.GetSequence("overlay", "build-valid-{0}".F(tileset)).GetSprite(0);
			else
				buildOk = map.Rules.Sequences.GetSequence("overlay", "build-valid").GetSprite(0);
			buildBlocked = map.Rules.Sequences.GetSequence("overlay", "build-invalid").GetSprite(0);
		}

		PlaceBuildingCellType MakeCellType(bool valid, bool lineBuild = false)
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
				//if (variants[variant].Preview != null)
				//	offsetPos += variants[variant].Preview.TopLeftScreenOffset;

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


				//var ai = variants[variant].ActorInfo;
				//var bi = variants[variant].BuildingInfo;
				var notification = queue.Info.CannotPlaceAudio ?? placeBuildingInfo.CannotPlaceNotification;


				if (!world.CanPlaceBuilding(topLeft, actorInfo, buildingInfo, queue.Actor))
				{
					foreach (var order in ClearBlockersOrders(world, topLeft))
						yield return order;

					Game.Sound.PlayNotification(world.Map.Rules, owner, "Speech", notification, owner.Faction.InternalName);
					TextNotificationsManager.AddTransientLine(placeBuildingInfo.CannotPlaceTextNotification, owner);

					yield break;
				}

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

		void IOrderGenerator.Tick(World world)
		{
			if (queue == null)
				world.CancelInputMode();

			if (preview == null)
				return;

			foreach (var p in preview)
				p.Tick();
		}

		void IOrderGenerator.SelectionChanged(World world, IEnumerable<Actor> selected) { }

		IEnumerable<IRenderable> IOrderGenerator.Render(WorldRenderer wr, World world) { yield break; }
		IEnumerable<IRenderable> IOrderGenerator.RenderAboveShroud(WorldRenderer wr, World world)
		{
			var topLeft = TopLeft;
			var footprint = new Dictionary<CPos, PlaceBuildingCellType>();
			var buildingInfo = actorInfo.TraitInfo<BuildingInfo>();
			//var activeVariant = variants[variant];
			//var plugInfo = activeVariant.PlugInfo;
			//var lineBuildInfo = activeVariant.LineBuildInfo;
			IPlaceBuildingPreview preview;



			var previewGeneratorInfo = actorInfo.TraitInfoOrDefault<IPlaceBuildingPreviewGeneratorInfo>();
			if (previewGeneratorInfo != null)
			{
				string faction;
				var buildableInfo = actorInfo.TraitInfoOrDefault<BuildableInfo>();
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

				foreach (var api in actorInfo.TraitInfos<IActorPreviewInitInfo>())
					foreach (var o in api.ActorPreviewInits(actorInfo, ActorPreviewType.PlaceBuilding))
						td.Add(o);

				preview = previewGeneratorInfo.CreatePreview(wr, actorInfo, td);
			}


			var owner = queue.Actor.Owner;

			
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

		IEnumerable<Order> ClearBlockersOrders(World world, CPos topLeft)
		{
			var allTiles = variants[variant].BuildingInfo.Tiles(topLeft).ToArray();
			var adjacentTiles = Util.ExpandFootprint(allTiles, true).Except(allTiles)
				.Where(world.Map.Contains).ToList();

			var blockers = allTiles.SelectMany(world.ActorMap.GetActorsAt)
				.Where(a => a.Owner == Queue.Actor.Owner && a.IsIdle)
				.Select(a => new TraitPair<IMove>(a, a.TraitOrDefault<IMove>()))
				.Where(x => x.Trait != null);

			foreach (var blocker in blockers)
			{
				CPos moveCell;
				if (blocker.Trait is Mobile mobile)
				{
					var availableCells = adjacentTiles.Where(t => mobile.CanEnterCell(t)).ToList();
					if (availableCells.Count == 0)
						continue;

					moveCell = blocker.Actor.ClosestCell(availableCells);
				}
				else if (blocker.Trait is Aircraft)
					moveCell = blocker.Actor.Location;
				else
					continue;

				yield return new Order("Move", blocker.Actor, Target.FromCell(world, moveCell), false)
				{
					SuppressVisualFeedback = true
				};
			}
		}
	}
}
