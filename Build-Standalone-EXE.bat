@echo off
setlocal
cd /d "%~dp0"

echo ========================================
echo   D&D Loot Roller - EXE Builder
echo ========================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
    echo ERROR: The .NET SDK was not found.
    echo.
    echo Open Visual Studio Installer, choose Modify, and install:
    echo   .NET desktop development
    echo Then restart Windows and run this file again.
    echo.
    pause
    exit /b 1
)

echo Publishing a standalone 64-bit Windows application...
dotnet publish "%~dp0DNDLootRoller.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false

if errorlevel 1 (
    echo.
    echo BUILD FAILED. Review the messages above.
    echo.
    pause
    exit /b 1
)

set "OUTDIR=%~dp0bin\Release\net8.0-windows\win-x64\publish"
echo.
echo BUILD COMPLETE.
echo Your application is here:
echo   %OUTDIR%\DNDLootRoller.exe
echo.
start "" "%OUTDIR%"
pause
