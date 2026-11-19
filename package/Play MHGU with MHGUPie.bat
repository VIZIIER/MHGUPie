@echo off
rem Starts MHGU (MHGURecomp) and the MHGUPie overlay together.
rem Put this file in the game folder, next to mhgurecomp.exe, with the MHGUPie folder beside it.
cd /d "%~dp0"

if not exist "MHGUPie\MHGUPie.exe" (
    echo MHGUPie\MHGUPie.exe was not found.
    echo Put the MHGUPie folder in the same folder as this file and mhgurecomp.exe.
    pause
    exit /b 1
)

if exist "mhgurecomp-updater.exe" (
    start "" "mhgurecomp-updater.exe"
) else if exist "mhgurecomp.exe" (
    start "" "mhgurecomp.exe"
) else (
    echo mhgurecomp.exe was not found. Put this file in the MHGURecomp game folder.
    pause
    exit /b 1
)

rem the overlay closes by itself when the game closes
start "" "MHGUPie\MHGUPie.exe" --close-with-game
