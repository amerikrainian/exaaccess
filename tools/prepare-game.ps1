# Prepares the game-derived BUILD INPUTS the module needs (see CLAUDE.md "Typed game access"):
#   game\EXAPUNKS.orig.exe   - byte copy of the installed game's exe
#   game\EXAPUNKS-deob.exe   - de4dot 3.1.41592.3405 output (the readable names the module
#                              compiles against; version-pinned - a different de4dot could shift
#                              names and break compilation. Reproducibility verified: a fresh run
#                              yields a byte-identical namemap.)
# Also copies the game dlls tools resolve against, and can refresh the decompiled analysis tree.
#
# One-time on a fresh machine:  .\tools\prepare-game.ps1        (game must be installed)
# After a game update:          .\tools\prepare-game.ps1 -Force  (then re-verify ordinals/anchors!)
param(
    [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\EXAPUNKS",
    [switch]$Force,
    [switch]$Decompile
)

$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot
$game = Join-Path $repo "game"
$de4dotDir = Join-Path $repo "tools\de4dot"
$de4dotZip = Join-Path $repo "third_party\de4dot\de4dot.zip"
$de4dotZipSha = "C726CBD18B894CA63B7F6A565C6C86EF512B96E68119C6502CDF64A51F6A1C78"

$gameExe = Join-Path $GameDir "EXAPUNKS.exe"
if (-not (Test-Path $gameExe)) {
    throw "EXAPUNKS.exe not found at '$GameDir'. Install the game, or pass -GameDir."
}
New-Item -ItemType Directory -Force $game | Out-Null

# 1. The orig copy (+ the game dlls analysis tools resolve against).
$orig = Join-Path $game "EXAPUNKS.orig.exe"
if ($Force -or -not (Test-Path $orig) -or
    (Get-FileHash $gameExe).Hash -ne (Get-FileHash $orig).Hash) {
    Copy-Item $gameExe $orig -Force
    Write-Host "copied: EXAPUNKS.orig.exe (from $GameDir)"
} else {
    Write-Host "up to date: EXAPUNKS.orig.exe"
}
foreach ($dep in @("Steamworks.NET.dll", "Ionic.Zip.Reduced.dll")) {
    $src = Join-Path $GameDir $dep
    $dst = Join-Path $game $dep
    if ((Test-Path $src) -and ($Force -or -not (Test-Path $dst))) { Copy-Item $src $dst -Force; Write-Host "copied: $dep" }
}

# 2. de4dot: use the extracted tool, else extract the vendored zip (hash-verified).
$de4dot = Join-Path $de4dotDir "de4dot-x64.exe"
if (-not (Test-Path $de4dot)) {
    if (-not (Test-Path $de4dotZip)) {
        throw "de4dot not found. Expected the vendored zip at '$de4dotZip' (de4dot 3.1.41592.3405, SHA256 $de4dotZipSha)."
    }
    $actual = (Get-FileHash $de4dotZip -Algorithm SHA256).Hash
    if ($actual -ne $de4dotZipSha) {
        throw "de4dot.zip SHA256 mismatch (got $actual) - refusing to run an unverified tool."
    }
    New-Item -ItemType Directory -Force $de4dotDir | Out-Null
    Expand-Archive $de4dotZip $de4dotDir -Force
    if (-not (Test-Path $de4dot)) { throw "de4dot-x64.exe missing after extraction - unexpected zip layout." }
    Write-Host "extracted: de4dot 3.1.41592.3405 -> tools\de4dot"
}

# 3. The deob assembly (the module's compile reference).
$deob = Join-Path $game "EXAPUNKS-deob.exe"
if ($Force -or -not (Test-Path $deob) -or
    (Get-Item $orig).LastWriteTimeUtc -gt (Get-Item $deob).LastWriteTimeUtc) {
    Write-Host "running de4dot (this takes a moment)..."
    & $de4dot $orig -o $deob | Select-Object -Last 1 | Out-Host
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $deob)) { throw "de4dot failed (exit $LASTEXITCODE)." }
    Write-Host "generated: EXAPUNKS-deob.exe"
} else {
    Write-Host "up to date: EXAPUNKS-deob.exe"
}

# 4. Optional: refresh the decompiled analysis tree (game\decompiled).
if ($Decompile) {
    $ilspy = Get-Command ilspycmd -ErrorAction SilentlyContinue
    if (-not $ilspy) { throw "ilspycmd not found - install with: dotnet tool install -g ilspycmd" }
    Write-Host "decompiling (this takes a while)..."
    & $ilspy.Source $deob -p -o (Join-Path $game "decompiled") 2>&1 |
        Tee-Object -FilePath (Join-Path $game "decompile.log") | Select-Object -Last 1 | Out-Host
    Write-Host "decompiled -> game\decompiled"
}

Write-Host "done. The module builds now (dotnet build ExaAccess.sln)."
