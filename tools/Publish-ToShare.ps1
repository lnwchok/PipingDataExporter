# Copies the team package (Release x64 DLL + BOQ report page + README) to the shared folder.
# Usage: pwsh -File tools/Publish-ToShare.ps1 [-Build]
param(
    [string]$Destination = '\\192.165.0.5\Piping_Group\XX_Backup\Sitthichok\Devs\OpenPlant_BQ_Extractor',
    [switch]$Build
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

if ($Build) {
    dotnet build (Join-Path $root 'PipingDataExporter.csproj') -c Release -p:Platform=x64
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
}

$files = @(
    (Join-Path $root 'bin\x64\Release\net48\PipingDataExporter.dll'),
    (Join-Path $root 'tools\boq_report.html'),
    (Join-Path $root 'README.md')
)
foreach ($f in $files) { if (-not (Test-Path $f)) { throw "Missing: $f" } }

if (-not (Test-Path $Destination)) { New-Item -ItemType Directory -Path $Destination | Out-Null }

foreach ($f in $files) {
    Copy-Item $f -Destination $Destination -Force
    Write-Host "Copied $(Split-Path $f -Leaf) -> $Destination"
}
