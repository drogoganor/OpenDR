@echo off
set IMPORT_DIR=maps-import
set MOD_ID=dr
set MOD_SEARCH_PATHS=./../../mods,./../mods
set ENGINE_DIR=./..
set ENGINE_DIRECTORY2=./engine/bin
set TEMPLATE_DIR=%CD%
cd %ENGINE_DIRECTORY2%

call OpenRA.Utility.exe %MOD_ID% --import-dr-campaign-map "FIXED/M01F/M01F.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-campaign-map "FIXED/M02F/M02F.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-campaign-map "FIXED/M03F/M03F.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-campaign-map "FIXED/M04F/M04F.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-campaign-map "FIXED/M05F/M05F.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-campaign-map "FIXED/M06F/M06F.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-campaign-map "FIXED/M07F/M07F.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-campaign-map "FIXED/M08F/M08F.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-campaign-map "FIXED/M09F/M09F.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-campaign-map "FIXED/M10F/M10F.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-campaign-map "FIXED/M11F/M11F.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-campaign-map "FIXED/M12F/M12F.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-campaign-map "FIXED/M01I/M01I.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-campaign-map "FIXED/M02I/M02I.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-campaign-map "FIXED/M03I/M03I.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-campaign-map "FIXED/M04I/M04I.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-campaign-map "FIXED/M05I/M05I.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-campaign-map "FIXED/M06I/M06I.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-campaign-map "FIXED/M07I/M07I.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-campaign-map "FIXED/M08I/M08I.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-campaign-map "FIXED/M09I/M09I.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-campaign-map "FIXED/M10I/M10I.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-campaign-map "FIXED/M11I/M11I.MAP"
call OpenRA.Utility.exe %MOD_ID% --import-dr-campaign-map "FIXED/M12I/M12I.MAP"
pause
exit /b


:noengine
echo Required engine files not found.
echo Run `make all` in the mod directory to fetch and build the required files, then try again.
pause
exit /b