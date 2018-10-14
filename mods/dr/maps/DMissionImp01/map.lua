
AbandonedBase = { ABWater, ABPower, ABHQ, ABFreighter }
EnemyBase = { EnemyWater, EnemyHQ, EnemyBarracks, EnemyPower }
TrappedSoldiers1 = { TS101, TS102, TS103, TS104, TS105 }
TrappedSoldiers2 = { TS201, TS202, TS203, TS204, TS205 }
StartUnits = { "guardian", "guardian", "guardian", "guardian", "guardian", "bion" }
StartUnits2 = { "guardian", "guardian", "guardian", "guardian", "guardian", "bion" }

EnemyInfantrySquads =
{
	{ "raider", "raider" },
	{ "raider", "mercenary" },
	{ "mercenary", "mercenary" }
}

MissionAccomplished = function()
	Media.PlaySpeechNotification(player, "MissionAccomplished")
end

MissionFailed = function()
	Media.PlaySpeechNotification(player, "MissionFailed")
end

-- Called at start to infiltrate player units
InsertPlayerUnits = function()
	Reinforcements.Reinforce(player, StartUnits, { Entry1.Location, Rally1.Location }, 15)
	Reinforcements.Reinforce(player, StartUnits2, { Entry2.Location, Rally2.Location }, 15)
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
	DoAttackMove(actors, EnemyRally)
end

-- Recurrent enemy attack
RepeatBuildAttackForce = function()
	DoUnitProduction(Utils.Random(EnemyInfantrySquads), EnemyBarracks, EnemyAttack)
	Trigger.AfterDelay(DateTime.Seconds(36), RepeatBuildAttackForce)
end

DiscoverAbandonedBase = function(_, actor, discoverer)
	if baseDiscovered or not discoverer == player then
		return
	end
	
	Utils.Do(AbandonedBase, function(actor)
		actor.Owner = player
	end)

	baseDiscovered = true
	
	DestroyEnemiesObjective = player.AddPrimaryObjective("Find and destroy the enemy presence in the area.")
	player.MarkCompletedObjective(FindBaseObjective)
	
	--Trigger.AfterDelay(DateTime.Seconds(12), InitialOffenseCall)
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

	player = Player.GetPlayer("Imperium")
	abandonBase = Player.GetPlayer("Abandoned Base")
	allySoldiers1 = Player.GetPlayer("Trapped Soldiers 1")
	allySoldiers2 = Player.GetPlayer("Trapped Soldiers 2")
	enemy = Player.GetPlayer("Freedom Guard")
	
	Trigger.OnPlayerDiscovered(abandonBase, DiscoverAbandonedBase)
	Trigger.OnPlayerDiscovered(allySoldiers1, DiscoverTrappedSoldiers1)
	Trigger.OnPlayerDiscovered(allySoldiers2, DiscoverTrappedSoldiers2)
	
	Trigger.OnAllKilled(EnemyBase, function()
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
	
	FindBaseObjective = player.AddPrimaryObjective("Find the imperiled Imperium base.")
	
	Camera.Position = Rally1.CenterPosition

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
end
