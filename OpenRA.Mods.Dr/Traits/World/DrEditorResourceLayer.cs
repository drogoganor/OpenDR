//#region Copyright & License Information
///*
// * Copyright 2007-2022 The OpenRA Developers (see AUTHORS)
// * This file is part of OpenRA, which is free software. It is made
// * available to you under the terms of the GNU General Public License
// * as published by the Free Software Foundation, either version 3 of
// * the License, or (at your option) any later version. For more
// * information, see COPYING.
// */
//#endregion

//using System;
//using System.Collections.Generic;
//using System.Linq;
//using OpenRA.Graphics;
//using OpenRA.Mods.Common.Traits;
//using OpenRA.Traits;

//namespace OpenRA.Mods.Dr.Traits.World
//{
//	[TraitLocation(SystemActors.EditorWorld)]
//	[Desc("Required for the map editor to work. Attach this to the world actor.")]
//	public class DrEditorResourceLayerInfo : EditorResourceLayerInfo
//	{
//		public override object Create(ActorInitializer init) { return new DrEditorResourceLayer(init.Self, this); }
//	}

//	public class DrEditorResourceLayer : EditorResourceLayer, IResourceLayer
//	{
//		readonly DrEditorResourceLayerInfo info;
//		bool IResourceLayer.CanAddResource(string resourceType, CPos cell, int amount) { return CanAddResource(resourceType, cell, amount); }
//		int IResourceLayer.AddResource(string resourceType, CPos cell, int amount) { return AddResource(resourceType, cell, amount); }
//		int IResourceLayer.RemoveResource(string resourceType, CPos cell, int amount) { return RemoveResource(resourceType, cell, amount); }
//		void IResourceLayer.ClearResources(CPos cell) { ClearResources(cell); }
//		bool IResourceLayer.IsVisible(CPos cell) { return Map.Contains(cell); }
//		bool IResourceLayer.IsEmpty => false;
//		IResourceLayerInfo IResourceLayer.Info => info;

//		public DrEditorResourceLayer(Actor self, DrEditorResourceLayerInfo info) : base(self, info)
//		{
//			this.info = info;
//		}

//		bool CanAddResource(string resourceType, CPos cell, int amount = 1)
//		{
//			var resources = Map.Resources;
//			if (!resources.Contains(cell))
//				return false;

//			if (!info.ResourceTypes.TryGetValue(resourceType, out var resourceInfo))
//				return false;

//			// The editor allows the user to replace one resource type with another, so treat mismatching resource type as an empty cell
//			var content = resources[cell];
//			if (content.Type != resourceInfo.ResourceIndex)
//				return AllowResourceAt(resourceType, cell);

//			var oldDensity = content.Type == resourceInfo.ResourceIndex ? content.Index : 0;
//			return oldDensity + amount <= resourceInfo.MaxDensity;
//		}

//		protected override int AddResource(string resourceType, CPos cell, int amount = 1)
//		{
//			var resources = Map.Resources;
//			if (!resources.Contains(cell))
//				return 0;

//			if (!info.ResourceTypes.TryGetValue(resourceType, out var resourceInfo))
//				return 0;

//			// The editor allows the user to replace one resource type with another, so treat mismatching resource type as an empty cell
//			var content = resources[cell];
//			var oldDensity = content.Type == resourceInfo.ResourceIndex ? content.Index : 0;
//			var density = (byte)Math.Min(resourceInfo.MaxDensity, oldDensity + amount);
//			Map.Resources[cell] = new ResourceTile(resourceInfo.ResourceIndex, density);

//			return density - oldDensity;
//		}

//		protected override int RemoveResource(string resourceType, CPos cell, int amount = 1)
//		{
//			var resources = Map.Resources;
//			if (!resources.Contains(cell))
//				return 0;

//			if (!info.ResourceTypes.TryGetValue(resourceType, out var resourceInfo))
//				return 0;

//			var content = resources[cell];
//			if (content.Type == 0 || content.Type != resourceInfo.ResourceIndex)
//				return 0;

//			var oldDensity = content.Index;
//			var density = (byte)Math.Max(0, oldDensity - amount);
//			resources[cell] = density > 0 ? new ResourceTile(resourceInfo.ResourceIndex, density) : default;

//			return oldDensity - density;
//		}

//		protected override void ClearResources(CPos cell)
//		{
//			Map.Resources[cell] = default;
//		}
//	}
//}
