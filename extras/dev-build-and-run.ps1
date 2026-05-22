# dev-build-and-run.ps1
#
# Bumps the Winhance.UI csproj Version / FileVersion / AssemblyVersion to
# today's date (if the current value is older), builds in Debug/x64, and
# launches the fresh binary.
#
# Run from any working directory:
#   pwsh -File .\extras\dev-build-and-run.ps1
# or
#   & .\extras\dev-build-and-run.ps1
#
# Pass -Clean to wipe per-project obj/ and bin/ before the build. Default is
# incremental — much faster and survives the network-share gotchas a forced
# wipe creates (see comment on the wipe block below).
#
# On network-share repos, in-tree src\*\obj and src\*\bin are ALWAYS wiped
# before build (regardless of -Clean), because those dirs should be empty
# when WINHANCE_LOCAL_BUILD_ROOT is in effect — a populated in-tree obj/
# is a leak from VS or a bare dotnet build and causes 2k+ duplicate-
# definition errors. The local build root under $env:LOCALAPPDATA is left
# alone unless -Clean is passed.
[CmdletBinding()]
param(
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'

# Block at the end so the console window (when launched from Explorer or a
# shortcut) stays open long enough to read AND copy the build output. Fires
# on BOTH success and failure paths — on success the user usually still
# wants to see warnings, the app version that just launched, etc. Without
# this the script just blinks past on a fast clean build.
#
# We deliberately don't use ReadKey here — it intercepts Ctrl+C as "any
# key" before the terminal layer can route it to Copy-on-selection, which
# is exactly what makes "press any key to exit" prompts so frustrating
# when you want to grab the text. Sleeping in a loop instead lets the
# terminal handle Ctrl+C its normal way: copy if text is selected, send
# Break (and exit the script) if nothing is. Closing the window with the
# X button also tears the script down. No-op in non-console hosts (VS
# Code, ISE, etc.) where the window doesn't disappear on script exit
# anyway, and where the wait would be a usability bug.
function Wait-OnExit {
    param([string]$Outcome)
    if ($Host.Name -ne 'ConsoleHost') { return }
    Write-Host ""
    if ($Outcome -eq 'success') {
        Write-Host "Build succeeded and app launched." -ForegroundColor Green
    } else {
        Write-Host "Build failed." -ForegroundColor Red
    }
    Write-Host "Window will stay open. Select text and Ctrl+C to copy; close the window when done." -ForegroundColor Yellow
    while ($true) { Start-Sleep -Seconds 3600 }
}

# Default outcome — anything that throws below leaves it as 'failure'; the
# success path at the end of the try block flips it to 'success'. Wait-OnExit
# at the bottom then keeps the window open and reports the right status.
$script:buildOutcome = 'failure'
$pushedLocation = $false

try {

$repoRoot     = Split-Path -Parent $PSScriptRoot
$csprojPath   = Join-Path $repoRoot 'src\Winhance.UI\Winhance.UI.csproj'
$msbuild      = Join-Path ${env:ProgramFiles} 'Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'
$buildLogDir  = Join-Path $env:LOCALAPPDATA 'Winhance-dev'
$buildLog     = Join-Path $buildLogDir 'last-build.log'
$null = New-Item -ItemType Directory -Path $buildLogDir -Force

# Detect SMB-mapped drive. When true we redirect MSBuild's bin/ and obj/
# output to a local path (see "Build" section below). Two reasons:
#   1. Windows refuses to launch executables from network shares (the
#      Internet-zone Mark-of-the-Web block) so the binary HAS to end up
#      local before we Start-Process it.
#   2. Building straight to the SMB share is fundamentally racy. MSBuild
#      runs MakeDir then immediately writes into the new dir from a
#      sibling task; the SMB redirector hasn't surfaced the namespace
#      change to this client yet, so File.Create fails with
#      DirectoryNotFoundException (cascading MSB3191/CS2012/MSB4018).
#      No amount of pre-create or directory-listing cache flushing fixes
#      this — the race lives inside MSBuild's own task scheduling. The
#      only reliable cure is keeping bin/ and obj/ off the share entirely.
$repoIsRemote = ($repoRoot -match '^[A-Z]:\\') -and `
                ((Get-PSDrive ($repoRoot.Substring(0, 1)) -ErrorAction SilentlyContinue).DisplayRoot -like '\\*')

$tfm = 'net10.0-windows10.0.19041.0'
if ($repoIsRemote) {
    $localBuildRoot = Join-Path $env:LOCALAPPDATA 'Winhance-dev\build'
    $null = New-Item -ItemType Directory -Path $localBuildRoot -Force
    $exePath = Join-Path $localBuildRoot "Winhance.UI\bin\x64\Debug\$tfm\win-x64\Winhance.exe"
}
else {
    $exePath = Join-Path $repoRoot "src\Winhance.UI\bin\x64\Debug\$tfm\win-x64\Winhance.exe"
}

if (-not (Test-Path $csprojPath)) { throw "csproj not found: $csprojPath" }
if (-not (Test-Path $msbuild))    { throw "MSBuild not found: $msbuild" }

# --- Version bump ---------------------------------------------------------
# Use .NET APIs for IO so we preserve UTF-8-no-BOM. Windows PowerShell 5.1's
# Get-Content / Set-Content default to ANSI (Windows-1252) which mangles non-
# ASCII characters like the copyright symbol, and -Encoding UTF8 on 5.1 writes
# a BOM that git treats as a change.
$today        = Get-Date -Format 'yy.MM.dd'
$utf8NoBom    = New-Object System.Text.UTF8Encoding $false
$content      = [System.IO.File]::ReadAllText($csprojPath, [System.Text.Encoding]::UTF8)
$versionRegex = '<(Version|FileVersion|AssemblyVersion)>(\d{2}\.\d{2}\.\d{2})</\1>'
$infoVersionRegex = '<InformationalVersion>v(\d{2}\.\d{2}\.\d{2})</InformationalVersion>'
$changed = $false

if ($content -match $versionRegex) {
    $current = $matches[2]
    if ([version]$today -gt [version]$current) {
        Write-Host "Bumping Version/FileVersion/AssemblyVersion $current -> $today" -ForegroundColor Cyan
        $content = $content -replace $versionRegex, ('<$1>' + $today + '</$1>')
        $changed = $true
    }
    else {
        Write-Host "Version already current: $current (today: $today) - not bumping" -ForegroundColor DarkGray
    }
}
else {
    Write-Host 'No Version tag found in csproj to bump.' -ForegroundColor Yellow
}

# InformationalVersion has a `v` prefix and is what the in-app UI displays
# (read via FileVersionInfo.ProductVersion in VersionService / NewBadgeService).
if ($content -match $infoVersionRegex) {
    $currentInfo = $matches[1]
    if ([version]$today -gt [version]$currentInfo) {
        Write-Host "Bumping InformationalVersion v$currentInfo -> v$today" -ForegroundColor Cyan
        $content = $content -replace $infoVersionRegex, ('<InformationalVersion>v' + $today + '</InformationalVersion>')
        $changed = $true
    }
    else {
        Write-Host "InformationalVersion already current: v$currentInfo (today: $today) - not bumping" -ForegroundColor DarkGray
    }
}
else {
    Write-Host 'No InformationalVersion tag found in csproj to bump.' -ForegroundColor Yellow
}

if ($changed) {
    [System.IO.File]::WriteAllText($csprojPath, $content, $utf8NoBom)
}

# --- Optional clean wipe -------------------------------------------------
# Off by default. Pass -Clean to wipe build outputs before rebuilding.
#
# When the repo is on a network share, outputs live under $localBuildRoot
# on C:\ — a clean wipe is just an rm -rf on a local path, fast and safe.
# When the repo is local, outputs are the per-project src\<proj>\bin and
# obj\ folders, so we wipe those.
#
# When to use -Clean: after a stale-intermediate failure that an
# incremental build can't shake — e.g. the WindowsAppSDK XAML compiler's
# WMC9999 ("Could not find file ...MainWindow.xaml"). Run once with
# -Clean to reset, then go back to incremental.
if ($Clean) {
    if ($repoIsRemote) {
        if (Test-Path $localBuildRoot) {
            Write-Host "Clean build requested — wiping $localBuildRoot" -ForegroundColor Cyan
            Remove-Item -Recurse -Force $localBuildRoot
        }
        $null = New-Item -ItemType Directory -Path $localBuildRoot -Force
    }
    else {
        Write-Host "Clean build requested — wiping all per-project obj/ and bin/" -ForegroundColor Cyan
        Get-ChildItem -Path (Join-Path $repoRoot 'src') -Directory | ForEach-Object {
            foreach ($sub in @('obj', 'bin')) {
                $stale = Join-Path $_.FullName $sub
                if (Test-Path $stale) {
                    Write-Host "Wiping $stale" -ForegroundColor DarkGray
                    Remove-Item -Recurse -Force $stale
                }
            }
        }
    }
}

# --- Strip leaked in-tree obj/bin (network-share repos only) -------------
# When $repoIsRemote, the script writes outputs to $localBuildRoot on C:\
# (via WINHANCE_LOCAL_BUILD_ROOT + src\Directory.Build.props). The in-tree
# src\<proj>\obj and bin folders should therefore stay empty.
#
# But if a build ran *without* the env var (Visual Studio, a bare `dotnet
# build`, `msbuild` invoked directly) it populates the in-tree obj/bin.
# Next time this script runs, MSBuild's default Compile glob walks the
# repo and pulls in the stale generated files (Generated Files\CsWinRT\*,
# *AssemblyInfo.cs) ALONGSIDE the freshly-generated ones under
# $localBuildRoot. Result: thousands of CS0579 / CS0101 / CS0111 duplicate
# definition errors that have nothing to do with the user's actual code.
#
# Cheap unconditional pre-step: when remote, wipe in-tree obj/bin. Empty
# dirs cost nothing; populated ones are the bug. We log per-dir if and
# only if something was removed, so a leaked build is visible.
if ($repoIsRemote) {
    $leakedCount = 0
    Get-ChildItem -Path (Join-Path $repoRoot 'src') -Directory -ErrorAction SilentlyContinue | ForEach-Object {
        foreach ($sub in @('obj', 'bin')) {
            $stale = Join-Path $_.FullName $sub
            if (Test-Path $stale) {
                if ($leakedCount -eq 0) {
                    Write-Host "Stripping leaked in-tree obj/bin (network-share repo expects outputs under $localBuildRoot):" -ForegroundColor Cyan
                }
                Write-Host "  Removing $stale" -ForegroundColor DarkGray
                Remove-Item -Recurse -Force $stale
                $leakedCount++
            }
        }
    }
}

# --- Build ---------------------------------------------------------------
Push-Location $repoRoot
$pushedLocation = $true

# When on a network share, redirect MSBuild output (bin/ AND obj/) to a
# local path via the WINHANCE_LOCAL_BUILD_ROOT env var, which
# src\Directory.Build.props reads to override BaseIntermediateOutputPath
# and BaseOutputPath per project. Going through Directory.Build.props
# (instead of -p: command-line properties) is what actually works:
#   - Command-line properties are passed as literal strings with no
#     re-evaluation, so $(MSBuildProjectName) in a -p: value never
#     expands per project — all 4 csprojs would collide on the same
#     output dir.
#   - PowerShell's native-arg quoter also mangles any -p: value that
#     contains $(...) or ends with backslash, which made the previous
#     attempt produce two -p: args concatenated into one.
# An env var sidesteps both problems and Directory.Build.props gets
# evaluated by MSBuild natively, expanding $(MSBuildProjectName) the
# normal way. Visual Studio doesn't set the env var, so VS-driven
# builds still go to the in-tree src\<proj>\bin and obj\.
if ($repoIsRemote) {
    Write-Host "Repo on network share — building outputs to $localBuildRoot" -ForegroundColor Cyan
    $env:WINHANCE_LOCAL_BUILD_ROOT = $localBuildRoot
}

# Stream full MSBuild output to the console (so errors come with file/line
# context) and tee a copy to a log file for after-the-fact review.
# -v:m (minimal) is the sweet spot: shows warnings + errors with file
# paths but doesn't drown the user in build-step chatter. -restore does
# NuGet restore in the same invocation.
& $msbuild `
    'src\Winhance.UI\Winhance.UI.csproj' `
    -p:Configuration=Debug -p:Platform=x64 -restore -v:m -nologo 2>&1 |
    Tee-Object -FilePath $buildLog

if ($LASTEXITCODE -ne 0) {
    throw "MSBuild failed with exit code $LASTEXITCODE. Full log: $buildLog"
}

if (-not (Test-Path $exePath)) {
    throw "Build succeeded but exe not found at: $exePath"
}

# Output is already local (or repo is local). Launch in place — no mirror
# step needed.
$launchExe = $exePath

Write-Host "Launching: $launchExe" -ForegroundColor Green
Start-Process -FilePath $launchExe
$script:buildOutcome = 'success'

}
catch {
    Write-Host ""
    Write-Host "BUILD FAILED:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    if ($_.ScriptStackTrace) {
        Write-Host ""
        Write-Host "Stack trace:" -ForegroundColor DarkRed
        Write-Host $_.ScriptStackTrace -ForegroundColor DarkRed
    }
}
finally {
    if ($pushedLocation) { Pop-Location }
}

Wait-OnExit -Outcome $script:buildOutcome
if ($script:buildOutcome -ne 'success') { exit 1 }
