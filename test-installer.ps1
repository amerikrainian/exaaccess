# Run the installer's unit tests (cargo test in installer/).
#
# Adapted from the Non-Visual Calculus installer by Rashad Naqeeb (MIT),
# https://github.com/rashadnaqeeb/NonVisualCalculus - by way of the Harkest Dungeon
# installer, https://github.com/amerikrainian/harkest-dungeon.

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$installerDir = Join-Path $scriptDir "installer"

if (-not (Test-Path (Join-Path $installerDir "Cargo.toml"))) {
    throw "Installer project not found: $installerDir"
}

. (Join-Path $scriptDir "tools\installer-toolchain.ps1")

Push-Location $installerDir
try {
    cargo test
    if ($LASTEXITCODE -ne 0) {
        throw "Installer tests failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}
