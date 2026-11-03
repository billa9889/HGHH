@echo off
setlocal

echo [1/3] clearing lock state and vault...
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\WinUpdate" /f >nul 2>&1
if %ERRORLEVEL% == 0 (echo     registry key wiped) else (echo     registry key not present)

echo [2/3] clearing state manifest...
if exist "%APPDATA%\Microsoft\Windows\WinUpdate\state.dat" (
    del /f /q "%APPDATA%\Microsoft\Windows\WinUpdate\state.dat"
    echo     state.dat deleted
) else (
    echo     state.dat not present
)

echo [3/3] removing auto-start entry...
reg delete "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v "WinUpdateHelper" /f >nul 2>&1
if %ERRORLEVEL% == 0 (echo     run key wiped) else (echo     run key not present)

echo.
echo done. relaunch the exe — it will start on the lock panel.
pause