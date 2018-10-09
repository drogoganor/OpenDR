
FGBase = { FGHQ, FGPower, FGWater, FGFreighter }
ImpBase = { ImpHQ, ImpPower, ImpWater, ImpBarracks }
OffenseTroops = { OffenseTroop1, OffenseTroop2 }
ResponseUnit = { DefenderTank1, DefenderTank2 }
TrappedSoldiers1 = { TS101, TS102, TS103, TS104, TS105 }
TrappedSoldiers2 = { TS201, TS202, TS203, TS204, TS205, TS206, TS207, TS208, TS209 }
StartUnits = { "raider", "raider", "raider", "medic" }
StartUnits2 = { "raider", "raider", "raider", "sniper" }

EnemyInfantrySquads =
{
	{ "bion", "guardian" },
	{ "guardian", "guardian" },
	{ "bion", "bion" }
}

MissionAccomplished = function()
	Media.PlaySpeechNotification(player, "MissionAccomplished")
end

MissionFailed = function()
	Media.PlaySpeechNotification(player, "MissionFailed")
end

-- Called at start to infiltrate player units
InsertPlayerUnits = function()
	Reinforcements.Reinforce(player, StartUnits, { EntryWaypoint.Location, StartWaypoint.Location }, 15)
	Reinforcements.Reinforce(player, StartUnits2, { EntryWaypoint2.Location, StartWaypoint2.Location }, 15)
	--Media.PlaySpeechNotification(player, "Reinforce")
end

-- Attack move this group of actors to this location, then hunt
DoAttackMove = function(actors, location)
	Utils.Do(actors, function(actor)
		if not actor.IsDead then
			Trigger.OnIdle(actor, function()
				actor.AttackMove(location.Location, 50)
				actor.Hunt()
			end)
		end
	end)
end

-- Produce this squad from this producer and give them this command
DoUnitProduction = function(squad, producer, command)
	if producer.IsDead or not producer.Owner == enemy then
		return
	end
	producer.Build(squad, command)
end

-- Enemy infantry attack to the player's base
EnemyAttack = function(actors)
	DoAttackMove(actors, AttackWaypoint)
end

-- Enemy's response attack move to having a building destroyed
DefenseCall = function(actors)
	DoAttackMove(actors, EnemyBaseWaypoint)
end

-- Enemy's initial attack once you discover the base; to let you know where they are
InitialOffenseCall = function()
	DoAttackMove(OffenseTroops, AttackWaypoint)
end

-- Enemy's unit production call
EnemyBarracksProduction = function(Squad)
	DoUnitProduction(Squad, ImpBarracks, EnemyAttack)
end

-- Recurrent enemy attack
RepeatBuildAttackForce = function()
	EnemyBarracksProduction(Utils.Random(EnemyInfantrySquads))
	Trigger.AfterDelay(DateTime.Seconds(36), RepeatBuildAttackForce)
end

DiscoverAbandonedBase = function(_, actor, discoverer)
	if baseDiscovered or not discoverer == player then
		return
	end
	
	Utils.Do(FGBase, function(actor)
		actor.Owner = player
	end)

	baseDiscovered = true
	
	DestroyEnemiesObjective = player.AddPrimaryObjective("Find and destroy the enemy presence in the area.")
	player.MarkCompletedObjective(FindBaseObjective)
	
	Trigger.AfterDelay(DateTime.Seconds(12), InitialOffenseCall)
	Trigger.AfterDelay(DateTime.Seconds(32), RepeatBuildAttackForce)
end

DiscoverTrappedSoldiers1 = function(_, actor, discoverer)
	if allySoldiers1Discovered or not discoverer == player then
		return
	end
	
	Utils.Do(TrappedSoldiers1, function(actor)
		actor.Owner = player
	end)

	allySoldiers1Discovered = true
end

DiscoverTrappedSoldiers2 = function(_, actor, discoverer)
	if allySoldiers2Discovered or not discoverer == player then
		return
	end
	
	Utils.Do(TrappedSoldiers2, function(actor)
		actor.Owner = player
	end)

	allySoldiers2Discovered = true
end

WorldLoaded = function()

	player = Player.GetPlayer("Freedom Guard")
	abandonBase = Player.GetPlayer("Abandoned Base")
	allySoldiers1 = Player.GetPlayer("Trapped Soldiers 1")
	allySoldiers2 = Player.GetPlayer("Trapped Soldiers 2")
	enemy = Player.GetPlayer("Imperium")
	
	Trigger.OnPlayerDiscovered(abandonBase, DiscoverAbandonedBase)
	Trigger.OnPlayerDiscovered(allySoldiers1, DiscoverTrappedSoldiers1)
	Trigger.OnPlayerDiscovered(allySoldiers2, DiscoverTrappedSoldiers2)
	
	Trigger.OnAllKilled(ImpBase, function()
		player.MarkCompletedObjective(DestroyEnemiesObjective)
	end)

	Trigger.OnObjectiveCompleted(player, function(p, id)
		Media.DisplayMessage(p.GetObjectiveDescription(id), "Objective completed")
	end)
	Trigger.OnObjectiveFailed(player, function(p, id)
		Media.DisplayMessage(p.GetObjectiveDescription(id), "Objective failed")
	end)
	Trigger.OnPlayerWon(player, MissionAccomplished)
	Trigger.OnPlayerLost(player, MissionFailed)
	
	FindBaseObjective = player.AddPrimaryObjective("Find the imperiled Freedom Guard base.")
	
	Camera.Position = StartWaypoint.CenterPosition

	InsertPlayerUnits()
	
end

Tick = function()
	if enemy.HasNoRequiredUnits() then
		player.MarkCompletedObjective(DestroyEnemiesObjective)
	end

	if player.HasNoRequiredUnits() then
		if DateTime.GameTime > 3 then
			player.MarkFailedObjective(DestroyEnemiesObjective)
		end
	end

	if not defenseCalled then
		if ImpWater.IsDead or ImpHQ.IsDead then
			DefenseCall(ResponseUnit)
			defenseCalled = true
		end
	end
end
