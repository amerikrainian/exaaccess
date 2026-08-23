# Dot-sourced by build-installer.ps1 / test-installer.ps1: put libclang (bindgen, for the
# wxWidgets build) and ninja (its CMake generator) where cargo will find them. Visual
# Studio ships both but puts neither on PATH.
#
# Adapted from the Non-Visual Calculus installer by Rashad Naqeeb (MIT),
# https://github.com/rashadnaqeeb/NonVisualCalculus - by way of the Harkest Dungeon
# installer, https://github.com/amerikrainian/harkest-dungeon.

if ([string]::IsNullOrWhiteSpace($env:LIBCLANG_PATH)) {
    $libclangCandidates = @(
        "C:\Program Files\Microsoft Visual Studio\18\Community\VC\Tools\Llvm\x64\bin",
        "C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Tools\Llvm\x64\bin",
        "C:\Program Files\Microsoft Visual Studio\2022\Professional\VC\Tools\Llvm\x64\bin",
        "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\VC\Tools\Llvm\x64\bin",
        "C:\Program Files\Microsoft Visual Studio\2022\BuildTools\VC\Tools\Llvm\x64\bin",
        "C:\Program Files\LLVM\bin"
    )
    foreach ($candidate in $libclangCandidates) {
        if (Test-Path (Join-Path $candidate "libclang.dll")) {
            $env:LIBCLANG_PATH = $candidate
            break
        }
    }
}

if ($null -eq (Get-Command ninja -ErrorAction SilentlyContinue)) {
    $ninjaCandidates = @(
        "C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\Ninja",
        "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\CMake\Ninja",
        "C:\Program Files\Microsoft Visual Studio\2022\BuildTools\Common7\IDE\CommonExtensions\Microsoft\CMake\Ninja"
    )
    foreach ($candidate in $ninjaCandidates) {
        if (Test-Path (Join-Path $candidate "ninja.exe")) {
            $env:PATH = "$candidate;$env:PATH"
            break
        }
    }
}
