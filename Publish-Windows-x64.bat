@echo off
setlocal
cd /d "%~dp0"

echo ==========================================
echo   Publishing DND Loot Roller for Windows
 echo ==========================================
echo.

where dotnet >nul 2>&1
if errorlevel 1 (
  echo ERROR: The .NET SDK was not found.
  echo Open Visual Studio Installer and add the workload:
  echo   .NET desktop development
  echo Then restart Windows and run this file again.
  echo.
  pause
  exit /b 1
)

dotnet publish DNDLootRoller.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
if errorlevel 1 (
  echo.
  echo BUILD FAILED. Review the messages above.
  pause
  exit /b 1
)

echo.
echo BUILD COMPLETE.
echo Opening the output folder...
start "" "%~dp0bin\Release\net8.0-windows\win-x64\publish"
pause
