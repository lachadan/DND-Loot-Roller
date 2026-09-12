$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host 'The .NET SDK was not found.' -ForegroundColor Red
    Write-Host 'Open Visual Studio Installer and install the .NET desktop development workload.'
    Read-Host 'Press Enter to close'
    exit 1
}

dotnet publish .\DNDLootRoller.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true

if ($LASTEXITCODE -ne 0) { Write-Error 'Publish failed. See the build errors above.'; exit $LASTEXITCODE }

$publishFolder = Join-Path $PSScriptRoot 'bin\Release\net8.0-windows\win-x64\publish'
Write-Host "Published successfully to: $publishFolder" -ForegroundColor Green
Start-Process explorer.exe $publishFolder
