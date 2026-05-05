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
$buildOutDir  = Join-Path $repoRoot 'src\Winhance.UI\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64'
$exePath      = Join-Path $buildOutDir 'Winhance.exe'
$msbuild      = Join-Path ${env:ProgramFiles} 'Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe'
$buildLogDir  = Join-Path $env:LOCALAPPDATA 'Winhance-dev'
$buildLog     = Join-Path $buildLogDir 'last-build.log'
$null = New-Item -ItemType Directory -Path $buildLogDir -Force

# Windows refuses to launch executables from network shares (Internet zone).
# When the repo lives on a UNC/SMB drive, we mirror the build output to a
# local path and launch from there. For local repos we just run in place.
$repoIsRemote = ($repoRoot -match '^[A-Z]:\\') -and `
                ((Get-PSDrive ($repoRoot.Substring(0, 1)) -ErrorAction SilentlyContinue).DisplayRoot -like '\\*')
$localRunDir  = Join-Path $env:LOCALAPPDATA 'Winhance-dev\win-x64'

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

# --- Optional wipe of per-project obj/bin under src/ ---------------------
# Off by default. Pass -Clean to enable.
#
# Why off by default: when the repo lives on a network share (e.g. an SMB-
# mapped Z:\) — which $repoIsRemote below detects — wiping obj/ and bin/
# and immediately asking MSBuild to recreate them produces a cascade of
# "Could not find a part of the path" errors (MSB3191, CS2012, MSB3026).
# The SMB server doesn't commit the namespace deletion fast enough for
# MSBuild's MakeDir tasks; even MSBuild's own 10-retry-with-1s-backoff
# loop can't outwait it. Incremental builds work fine on SMB because they
# update existing directories rather than recreating them.
#
# When to use -Clean: after a stale-intermediate failure that an
# incremental build can't shake — e.g. the WindowsAppSDK XAML compiler's
# WMC9999 ("Could not find file ...MainWindow.xaml"). Run once with
# -Clean to reset, then go back to incremental. If you're on a network
# share and -Clean fails with path-creation errors, the fallback is to
# run "Clean Solution" from Visual Studio (which closes file handles
# properly before deleting and lets the SMB server settle).
if ($Clean) {
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

# --- Build ---------------------------------------------------------------
Push-Location $repoRoot
$pushedLocation = $true

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

if ($repoIsRemote) {
    Write-Host "Repo is on a network share - mirroring output to $localRunDir" -ForegroundColor Cyan
    $null = New-Item -ItemType Directory -Path $localRunDir -Force
    & robocopy $buildOutDir $localRunDir /MIR /NFL /NDL /NJH /NJS /NC /NS /NP | Out-Null
    $rc = $LASTEXITCODE
    if ($rc -ge 8) { throw "robocopy failed with exit code $rc" }
    $global:LASTEXITCODE = 0  # robocopy uses 0-7 for success; reset so script returns clean
    $launchExe = Join-Path $localRunDir 'Winhance.exe'
}
else {
    $launchExe = $exePath
}

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
