# build.ps1 - Build Echopunks and deploy it into the game. Deploy itself is the Debug
# post-build target of the two projects (host dll + 0Harmony + Mono.Cecil + prism.dll +
# EXAPUNKS.exe.config + steam_appid.txt from the host; Echopunks.Module.dll + Echopunks\namemap.tsv
# + Echopunks\locale from the module); this script locates the game, checks the deob exe
# prerequisite, and runs the build. Close the game first, or the HOST dll copy is skipped
# (file locked) and you'll run a stale host - the module still deploys (it's byte-loaded).
#
# Adapted from the Non-Visual Calculus installer by Rashad Naqeeb (MIT),
# https://github.com/rashadnaqeeb/NonVisualCalculus - by way of the Harkest Dungeon
# installer, https://github.com/amerikrainian/harkest-dungeon.

param(
    [switch]$Help
)

if ($Help) {
    Write-Host "Usage: .\build.ps1 [-Help]"
    Write-Host "  Builds the solution (Debug) and deploys the mod into the game folder."
    Write-Host "  Run tools\prepare-game.ps1 once first (the module compiles against game\EXAPUNKS-deob.exe)."
    Write-Host "  Set EXAPUNKS_DIR to point at a non-default game folder."
    exit 0
}

$ErrorActionPreference = "Stop"

$GameExe = "EXAPUNKS.exe"
$GameFolder = "EXAPUNKS"

# --- Locate the game install: EXAPUNKS_DIR, then every Steam library ---
$Game = $env:EXAPUNKS_DIR
if (-not $Game) {
    $RegSteam = (Get-ItemProperty -Path "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam" -Name InstallPath -ErrorAction SilentlyContinue).InstallPath
    $DefaultSteam = if ($RegSteam) { $RegSteam } else { "C:\Program Files (x86)\Steam" }
    $SteamPaths = @()
    if (Test-Path "$DefaultSteam\steamapps") { $SteamPaths += $DefaultSteam }
    $LibFolders = "$DefaultSteam\steamapps\libraryfolders.vdf"
    if (Test-Path $LibFolders) {
        $content = Get-Content $LibFolders -Raw
        [regex]::Matches($content, '"path"\s+"([^"]+)"') | ForEach-Object {
            $p = $_.Groups[1].Value -replace '\\\\', '\'
            if ($p -ne $DefaultSteam -and (Test-Path "$p\steamapps")) { $SteamPaths += $p }
        }
    }
    foreach ($steam in $SteamPaths) {
        $candidate = "$steam\steamapps\common\$GameFolder"
        if (Test-Path "$candidate\$GameExe") { $Game = $candidate; break }
    }
    if (-not $Game) { $Game = "C:\Program Files (x86)\Steam\steamapps\common\$GameFolder" }
}
if (-not (Test-Path "$Game\$GameExe")) {
    Write-Host "ERROR: EXAPUNKS not found at: $Game" -ForegroundColor Red
    Write-Host "Set the EXAPUNKS_DIR environment variable to the game folder." -ForegroundColor Red
    exit 1
}

if (-not (Test-Path "$PSScriptRoot\game\EXAPUNKS-deob.exe")) {
    Write-Host "ERROR: game\EXAPUNKS-deob.exe is missing - the module compiles against it." -ForegroundColor Red
    Write-Host "Run tools\prepare-game.ps1 once (de4dot is vendored)." -ForegroundColor Red
    exit 1
}

# --- Build (the Debug post-build targets deploy) ---
Write-Host "Building Echopunks (game: $Game)..." -ForegroundColor Cyan
dotnet build "$PSScriptRoot\Echopunks.sln" -c Debug -p:GameDir="$Game"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build FAILED." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Done. Launch EXAPUNKS through Steam and listen for `"Echopunks ready`"." -ForegroundColor Cyan
