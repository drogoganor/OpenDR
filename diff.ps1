$diffApp = "& diffusew.exe"
$drModRoot = "\OpenRA.Mods.Dr"
$modsCommonRoot = "\engine\OpenRA.Mods.Common"
$env:Path = $env:Path + ';C:\Program Files (x86)\Diffuse'

Invoke-Expression "$diffApp `"$PSScriptRoot$drModRoot\Activities\DrDeliverResources.cs`" `"$PSScriptRoot$modsCommonRoot\Activities\DeliverResources.cs`""
Invoke-Expression "$diffApp `"$PSScriptRoot$drModRoot\Activities\DrFindAndDeliverResources.cs`" `"$PSScriptRoot$modsCommonRoot\Activities\FindAndDeliverResources.cs`""
Invoke-Expression "$diffApp `"$PSScriptRoot$drModRoot\Activities\DrHarvestResource.cs`" `"$PSScriptRoot$modsCommonRoot\Activities\HarvestResource.cs`""
Invoke-Expression "$diffApp `"$PSScriptRoot$drModRoot\Activities\FreighterDockSequence.cs`" `"$PSScriptRoot$modsCommonRoot\Activities\HarvesterDockSequence.cs`""

Invoke-Expression "$diffApp `"$PSScriptRoot$drModRoot\Orders\BuilderUnitBuildingOrderGenerator.cs`" `"$PSScriptRoot$modsCommonRoot\Orders\PlaceBuildingOrderGenerator.cs`""

Invoke-Expression "$diffApp `"$PSScriptRoot$drModRoot\Scripting\Properties\FreighterProperties.cs`" `"$PSScriptRoot$modsCommonRoot\Scripting\Properties\HarvesterProperties.cs`""

Invoke-Expression "$diffApp `"$PSScriptRoot$drModRoot\Traits\BotModules\BotModuleLogic\RigBaseBuilderManager.cs`" `"$PSScriptRoot$modsCommonRoot\Traits\BotModules\BotModuleLogic\BaseBuilderQueueManager.cs`""
Invoke-Expression "$diffApp `"$PSScriptRoot$drModRoot\Traits\BotModules\RigBaseBuilderBotModule.cs`" `"$PSScriptRoot$modsCommonRoot\Traits\BotModules\BaseBuilderBotModule.cs`""

Invoke-Expression "$diffApp `"$PSScriptRoot$drModRoot\Traits\Buildings\DrRefinery.cs`" `"$PSScriptRoot$modsCommonRoot\Traits\Buildings\Refinery.cs`""

Invoke-Expression "$diffApp `"$PSScriptRoot$drModRoot\Traits\Player\BuilderUnit.cs`" `"$PSScriptRoot$modsCommonRoot\Traits\Player\ProductionQueue.cs`""

Invoke-Expression "$diffApp `"$PSScriptRoot$drModRoot\Traits\Player\BuildUnitPlaceBuilding.cs`" `"$PSScriptRoot$modsCommonRoot\Traits\Player\PlaceBuilding.cs`""

Invoke-Expression "$diffApp `"$PSScriptRoot$drModRoot\Traits\Render\WithDrDockingAnimation.cs`" `"$PSScriptRoot$modsCommonRoot\Traits\Render\WithDockingAnimation.cs`""
Invoke-Expression "$diffApp `"$PSScriptRoot$drModRoot\Traits\Render\WithDrHarvestAnimation.cs`" `"$PSScriptRoot$modsCommonRoot\Traits\Render\WithHarvestAnimation.cs`""

Invoke-Expression "$diffApp `"$PSScriptRoot$drModRoot\Traits\World\DrResourceType.cs`" `"$PSScriptRoot$modsCommonRoot\Traits\World\ResourceType.cs`""

Invoke-Expression "$diffApp `"$PSScriptRoot$drModRoot\Traits\Freighter.cs`" `"$PSScriptRoot$modsCommonRoot\Traits\Harvester.cs`""

Invoke-Expression "$diffApp `"$PSScriptRoot$drModRoot\Widgets\BuildSelectPaletteWidget.cs`" `"$PSScriptRoot$modsCommonRoot\Widgets\ProductionPaletteWidget.cs`""

Invoke-Expression "$diffApp `"$PSScriptRoot$drModRoot\TraitsInterfaces.cs`" `"$PSScriptRoot$modsCommonRoot\TraitsInterfaces.cs`""

