
MissionAccomplished = function()
	Media.PlaySpeechNotification(player, "MissionAccomplished")
end

MissionFailed = function()
	Media.PlaySpeechNotification(player, "MissionFailed")
end

WorldLoaded = function()
	player = Player.GetPlayer("Freedom Guard")
	enemy = Player.GetPlayer("Imperium")

	DestroyEnemiesObjective = player.AddPrimaryObjective("Find and destroy the enemy presence in the area.")
	
	Trigger.OnObjectiveCompleted(player, function(p, id)
		Media.DisplayMessage(p.GetObjectiveDescription(id), "Objective completed")
	end)
	Trigger.OnObjectiveFailed(player, function(p, id)
		Media.DisplayMessage(p.GetObjectiveDescription(id), "Objective failed")
	end)
	Trigger.OnPlayerWon(player, MissionAccomplished)
	Trigger.OnPlayerLost(player, MissionFailed)
end

Tick = function()
	if enemy.HasNoRequiredUnits() then
		player.MarkCompletedObjective(DestroyEnemiesObjective)
	end

	if player.HasNoRequiredUnits() then
		player.MarkFailedObjective(DestroyEnemiesObjective)
	end
end
