# Build the standalone installer exe (installer/, Rust + wxWidgets) into
# releases\EchopunksInstaller.exe. Needs cargo and libclang (the wxWidgets build uses
# bindgen); LIBCLANG_PATH is probed from the usual LLVM locations.
#
# Adapted from the Non-Visual Calculus installer by Rashad Naqeeb (MIT),
# https://github.com/rashadnaqeeb/NonVisualCalculus - by way of the Harkest Dungeon
# installer, https://github.com/amerikrainian/harkest-dungeon.

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$installerDir = Join-Path $scriptDir "installer"
$releaseDir = Join-Path $scriptDir "releases"
$targetExe = Join-Path $installerDir "target\release\echopunks-installer.exe"
$outputExe = Join-Path $releaseDir "EchopunksInstaller.exe"

if (-not (Test-Path (Join-Path $installerDir "Cargo.toml"))) {
    throw "Installer project not found: $installerDir"
}

. (Join-Path $scriptDir "tools\installer-toolchain.ps1")

Push-Location $installerDir
try {
    cargo build --release
    if ($LASTEXITCODE -ne 0) {
        throw "Installer build failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

if (-not (Test-Path $targetExe)) {
    throw "Expected installer executable not found: $targetExe"
}

New-Item -ItemType Directory -Force $releaseDir | Out-Null
Copy-Item -LiteralPath $targetExe -Destination $outputExe -Force

Write-Host "Installer: $outputExe"
