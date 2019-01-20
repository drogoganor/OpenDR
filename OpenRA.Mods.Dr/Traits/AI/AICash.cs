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
        public object Create(ActorInitializer init) { return new AICash(); }
    }

    public class AICash : IBotTick
    {
        public void BotTick(IBot bot)
        {
            bot.Player.PlayerActor.Trait<PlayerResources>().GiveCash(500);
        }
    }
}
