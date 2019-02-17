
MissionAccomplished = function()
	Media.PlaySpeechNotification(player, "MissionAccomplished")
end

MissionFailed = function()
	Media.PlaySpeechNotification(player, "MissionFailed")
end

WorldLoaded = function()

	player = Player.GetPlayer("Imperium")
	enemy = Player.GetPlayer("Freedom Guard")

	Trigger.OnObjectiveCompleted(player, function(p, id)
		Media.DisplayMessage(p.GetObjectiveDescription(id), "Objective completed")
	end)
	Trigger.OnObjectiveFailed(player, function(p, id)
		Media.DisplayMessage(p.GetObjectiveDescription(id), "Objective failed")
	end)
	Trigger.OnPlayerWon(player, MissionAccomplished)
	Trigger.OnPlayerLost(player, MissionFailed)
	
	MakeEntranceObjective = player.AddPrimaryObjective("Clear the road for reinforcements.")

end

Tick = function()

end
