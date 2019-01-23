#region Copyright & License Information
/*
 * Copyright 2016-2018 The KKnD Developers (see AUTHORS)
 * This file is part of KKnD, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Dr.Traits.AI
{
    // TODO replace this completely when AI is modular!
    [Desc("Ugly hack to give the ai cash.")]
    public class AICashInfo : ITraitInfo
    {
        [Desc("Amount per cash infusion.")]
        public readonly int Amount = 500;

        [Desc("Game tick is modded with this value to determine how often cash is infused.")]
        public readonly int TickEach = 0;

        [Desc("Infuse cash until this tick has been reached. Zero is infinite.")]
        public readonly int UntilTick = 0;

        public object Create(ActorInitializer init) { return new AICash(this); }
    }

    public class AICash : IBotTick
    {
        readonly AICashInfo info;

        public AICash(AICashInfo info)
        {
            this.info = info;
        }

        void IBotTick.BotTick(IBot bot)
        {
            var tick = bot.Player.World.WorldTick;
            if ((info.UntilTick == 0 || tick <= info.UntilTick) && 
                (info.TickEach == 0 || tick % info.TickEach == 0))
                bot.Player.PlayerActor.Trait<PlayerResources>().GiveCash(info.Amount);
        }
    }
}
