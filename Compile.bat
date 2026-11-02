@echo off
setlocal
cd /d "%~dp0"

:: THIS STOPS THE INFINITE LOOP
set SKIP_CSHARP_BUILD=true

echo ==================================================
echo Building RansomKitV2 as a Single Standalone EXE
echo ==================================================
echo.

if exist "bin\Release\net8.0-windows\win-x64\publish" (
    rmdir /s /q "bin\Release\net8.0-windows\win-x64\publish"
)

dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [!] BUILD FAILED! Check the errors above.
    pause
    exit /b 0
)

echo.
echo ==================================================
echo BUILD SUCCESSFUL!
echo ==================================================
echo Your single EXE is ready (images embedded).
explorer "%~dp0bin\Release\net8.0-windows\win-x64\publish"

exit /b 0