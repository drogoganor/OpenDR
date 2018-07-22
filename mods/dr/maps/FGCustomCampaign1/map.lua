
FGBase = { FGPower, FGCRig, FGFreighter }

MissionAccomplished = function()
	Media.PlaySpeechNotification(player, "MissionAccomplished")
end

MissionFailed = function()
	Media.PlaySpeechNotification(player, "MissionFailed")
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
end

WorldLoaded = function()
	player = Player.GetPlayer("FreedomGuard")
	abandonBase = Player.GetPlayer("AbandonedBase")
	enemy = Player.GetPlayer("Imperium")
	
	--baseDiscovered = false

	--Trigger.OnDiscovered(FGPower, DiscoverAbandonedBase)
	Trigger.OnPlayerDiscovered(abandonBase, DiscoverAbandonedBase)
	
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

end

Tick = function()
	if enemy.HasNoRequiredUnits() then
		player.MarkCompletedObjective(DestroyEnemiesObjective)
	end

	if player.HasNoRequiredUnits() then
		player.MarkFailedObjective(DestroyEnemiesObjective)
	end
end
