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

namespace OpenRA.Mods.Common.Traits
{
    // Copy of ProductionItem
    public class BuilderItem
    {
        public readonly string Item;
        public readonly BuilderQueue Queue;
        public Action OnComplete;

        public int TotalTime { get; private set; }
        public int RemainingTime { get; private set; }
        public int RemainingTimeActual
        {
            get
            {
                return RemainingTime;
            }
        }

        public bool Done { get; private set; }

        readonly ActorInfo ai;
        readonly BuildableInfo bi;

        public BuilderItem(BuilderQueue queue, string item, Action onComplete)
        {
            Item = item;
            RemainingTime = TotalTime = 1;
            OnComplete = onComplete;
            Queue = queue;
            ai = Queue.Actor.World.Map.Rules.Actors[Item];
            bi = ai.TraitInfo<BuildableInfo>();
            var time = Queue.GetBuildTime(ai, bi);
            if (time > 0)
                RemainingTime = TotalTime = time;
        }

        public void Tick()
        {
            if (Done)
            {
                if (OnComplete != null)
                    OnComplete();

                return;
            }

            RemainingTime -= 1;
            if (RemainingTime > 0)
                return;

            Done = true;
        }
    }
}
