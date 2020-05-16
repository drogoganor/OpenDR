@echo off

FOR /F "tokens=1,2 delims==" %%A IN (mod.config) DO (set %%A=%%B)
if exist user.config (FOR /F "tokens=1,2 delims==" %%A IN (user.config) DO (set %%A=%%B))

title OpenRA.Utility.exe %MOD_ID%

set MOD_SEARCH_PATHS=%MOD_SEARCH_PATHS%,./mods

:start
set TEMPLATE_DIR=%CD%
if not exist %ENGINE_DIRECTORY%\OpenRA.Game.exe goto noengine
cd %ENGINE_DIRECTORY%

call OpenRA.Utility.exe %MOD_ID% --import-dr-map "MULTI/2drybed/2drybed.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-map "MULTI/2forest/2forest.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-map "MULTI/2foxhunt/2foxhunt.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-map "MULTI/2outback/2outback.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-map "MULTI/2sandtrp/2sandtrp.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-map "MULTI/2saturn/2saturn.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-map "MULTI/2teamwar/2teamwar.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-map "MULTI/3relic/3relic.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-map "MULTI/4warzone/4warzone.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-map "MULTI/4web/4web.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-map "MULTI/6chibe/6chibe.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-map "MULTI/6fuschia/6fuschia.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-map "MULTI/6morchiba/6morchiba.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-map "MULTI/8hitnrun/8hitnrun.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-map "MULTI/8nerf/8nerf.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-map "MULTI/8tarafor/8tarafor.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-map "MULTI/8terran/8terran.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-map "MULTI/8chemy2/8chemy2.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-map "MULTI/6chemyv2/6chemyv2.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-map "MULTI/4chemynt/4chemynt.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-map "MULTI/2chemy/2chemy.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-map "MULTI/6sixway/6sixway.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-map "MULTI/4sixway/4sixway.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-map "MULTI/2sixway/2sixway.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-map "MULTI/8teamwar/8teamwar.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-map "MULTI/6teamwar/6teamwar.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-map "MULTI/4teamwar/4teamwar.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-map "MULTI/2teamwar/2teamwar.MAP"
pause
exit /b


:noengine
echo Required engine files not found.
echo Run `make all` in the mod directory to fetch and build the required files, then try again.
pause
exit /b