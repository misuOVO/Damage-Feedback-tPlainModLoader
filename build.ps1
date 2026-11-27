# Build the HPAware port for tPlainModLoader / Terraria 1.4.5.8
#
# Usage: powershell -ExecutionPolicy Bypass -File .\build.ps1

param(
    [switch]$Run
)

$ErrorActionPreference = 'Stop'

$root    = Split-Path -Parent $MyInvocation.MyCommand.Path
$srcDir  = Join-Path $root 'src'
$modTpl  = Join-Path $root 'mod'
$outDir  = Join-Path $root 'dist'
$gameDir = 'E:\SteamLibrary\steamapps\common\Terraria'
$modsDir = Join-Path $gameDir 'Mods\HPAware'
$modName = 'HPAware'

$csc = 'C:\Program Files\dotnet\sdk\8.0.426\Roslyn\bincore\csc.dll'
if (-not (Test-Path $csc)) {
    $csc = (Get-ChildItem 'C:\Program Files\dotnet\sdk' -Directory |
            Sort-Object Name -Descending |
            ForEach-Object { Join-Path $_.FullName 'Roslyn\bincore\csc.dll' } |
            Where-Object { Test-Path $_ } |
            Select-Object -First 1)
}
if (-not $csc) { throw 'Roslyn csc.dll not found. Please install the .NET SDK.' }

$fw  = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319'
$xna = 'C:\Windows\Microsoft.NET\assembly\GAC_32'

$refs = @(
    (Join-Path $fw 'mscorlib.dll'),
    (Join-Path $fw 'System.dll'),
    (Join-Path $fw 'System.Core.dll'),
    (Join-Path $fw 'System.Xml.dll'),
    (Join-Path $fw 'System.Drawing.dll'),
    (Join-Path $gameDir 'Terraria.exe'),
    (Join-Path $gameDir 'tContentPatch.dll'),
    (Join-Path $gameDir 'CommandHelp.dll'),
    (Join-Path $gameDir '0Harmony.dll'),
    (Join-Path $gameDir 'ReLogic.dll'),
    (Join-Path $gameDir 'Newtonsoft.Json.dll'),
    (Join-Path $xna 'Microsoft.Xna.Framework\v4.0_4.0.0.0__842cf8be1de50553\Microsoft.Xna.Framework.dll'),
    (Join-Path $xna 'Microsoft.Xna.Framework.Game\v4.0_4.0.0.0__842cf8be1de50553\Microsoft.Xna.Framework.Game.dll'),
    (Join-Path $xna 'Microsoft.Xna.Framework.Graphics\v4.0_4.0.0.0__842cf8be1de50553\Microsoft.Xna.Framework.Graphics.dll')
)

foreach ($r in $refs) {
    if (-not (Test-Path $r)) { throw "Missing reference: $r" }
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null

$sources = Get-ChildItem -Path $srcDir -Filter '*.cs' -Recurse | ForEach-Object { $_.FullName }
if (-not $sources) { throw 'No .cs files found under src' }

$dll = Join-Path $outDir "$modName.dll"
if (Test-Path $dll) { Remove-Item $dll -Force }

$argList = @(
    '/nologo',
    '/target:library',
    '/platform:x86',
    '/optimize+',
    '/nostdlib+',
    '/langversion:latest',
    '/codepage:65001',
    "/out:$dll"
)
foreach ($r in $refs) { $argList += "/reference:$r" }

# embedded assets
$resources = @{}
$potion = Join-Path $root 'assets\PotionReady.png'
if (Test-Path $potion) { $resources[$potion] = 'HPAware.Assets.PotionReady.png' }

foreach ($k in $resources.Keys) {
    if (Test-Path $k) { $argList += "/resource:$k,$($resources[$k])" }
    else { Write-Host "Skip missing resource: $k" -ForegroundColor Yellow }
}

$argList += $sources

& dotnet $csc @argList
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)" }

Write-Host "Build OK: $dll" -ForegroundColor Green

New-Item -ItemType Directory -Force -Path $modsDir | Out-Null
Copy-Item $dll (Join-Path $modsDir "$modName.dll") -Force

if (Test-Path $modTpl) {
    Copy-Item (Join-Path $modTpl '*.json') $modsDir -Force
}

# mod icon (tPlainModLoader reads ico.png from the mod folder)
$ico = Join-Path $root 'assets\ico.png'
if (Test-Path $ico) {
    Copy-Item $ico (Join-Path $modsDir 'ico.png') -Force
}

Write-Host "Deployed to: $modsDir" -ForegroundColor Green
