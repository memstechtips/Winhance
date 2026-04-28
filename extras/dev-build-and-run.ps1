# dev-build-and-run.ps1
#
# Bumps the Winhance.UI csproj Version / FileVersion / AssemblyVersion to
# today's date (if the current value is older), rebuilds in Debug/x64, and
# launches the fresh binary.
#
# Run from any working directory:
#   pwsh -File .\extras\dev-build-and-run.ps1
# or
#   & .\extras\dev-build-and-run.ps1

$ErrorActionPreference = 'Stop'

$repoRoot     = Split-Path -Parent $PSScriptRoot
$csprojPath   = Join-Path $repoRoot 'src\Winhance.UI\Winhance.UI.csproj'
$buildOutDir  = Join-Path $repoRoot 'src\Winhance.UI\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64'
$exePath      = Join-Path $buildOutDir 'Winhance.exe'
$msbuild      = Join-Path ${env:ProgramFiles} 'Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe'

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

# --- Build ---------------------------------------------------------------
Push-Location $repoRoot
try {
    $buildErrors = & $msbuild `
        'src\Winhance.UI\Winhance.UI.csproj' `
        -p:Configuration=Debug -p:Platform=x64 -restore -v:q -nologo 2>&1 |
        Where-Object { $_ -match 'error ' }

    if ($buildErrors) {
        $buildErrors | ForEach-Object { Write-Host $_ -ForegroundColor Red }
        throw 'Build failed.'
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
}
finally {
    Pop-Location
}
