# Use-LocalBuildOutputs.ps1
# Shared network-share build fix. Dot-source from any script that runs `dotnet build` / `dotnet test`
# against this repo:  . "$PSScriptRoot\Use-LocalBuildOutputs.ps1" -RepoRoot $solutionDir
#
# When the repo lives on an SMB-mapped drive, MSBuild cannot reliably build into the in-tree
# src\<proj>\obj and bin: the SMB redirector races MakeDir/write, and the build user often cannot
# write the share's obj\ at all - so a project (e.g. Winhance.Core) silently links a STALE prebuilt
# .dll and ignores source edits, while a forced rebuild errors writing obj\...sourcelink.json.
# `dotnet test` also refuses to launch testhost.exe from a share. The cure (identical to
# build-and-package.ps1 / dev-build-and-run.ps1) is to redirect every project's obj\ and bin\ to a
# local path via the WINHANCE_LOCAL_BUILD_ROOT env var, which src\Directory.Build.props and
# tests\Directory.Build.props read, and to strip any leaked in-tree obj\ / bin\ on the share so the
# redirected build's default Compile glob does not pull stale generated files (CS0579/CS0101/CS0111
# duplicate-definition errors).
#
# No-op when the repo is on a local disk, so it is always safe to dot-source.
param(
    [Parameter(Mandatory = $true)]
    [string]$RepoRoot
)

$driveLetter = if ($RepoRoot -match '^([A-Za-z]):\\') { $Matches[1] } else { $null }
$repoIsRemote = $driveLetter -and `
    ((Get-PSDrive $driveLetter -ErrorAction SilentlyContinue).DisplayRoot -like '\\*')

if (-not $repoIsRemote) {
    return
}

$localBuildRoot = Join-Path $env:LOCALAPPDATA 'Winhance-dev\build'
$null = New-Item -ItemType Directory -Path $localBuildRoot -Force
$env:WINHANCE_LOCAL_BUILD_ROOT = $localBuildRoot
Write-Host "  repo on network share - redirecting build outputs to $localBuildRoot" -ForegroundColor Cyan

# Strip any leaked in-tree obj\ / bin\ on the share (left by a non-redirected build - VS, a bare
# dotnet build, or an earlier run of these scripts before this fix). Best-effort: a dir we cannot
# delete is surfaced as a warning rather than aborting the run.
$leaked = 0
foreach ($area in @('src', 'tests')) {
    Get-ChildItem -Path (Join-Path $RepoRoot $area) -Directory -ErrorAction SilentlyContinue | ForEach-Object {
        foreach ($sub in @('obj', 'bin')) {
            $stale = Join-Path $_.FullName $sub
            if (Test-Path $stale) {
                if ($leaked -eq 0) {
                    Write-Host "  stripping leaked in-tree obj\ / bin\ on the share:" -ForegroundColor Cyan
                }
                Write-Host "    removing $stale" -ForegroundColor DarkGray
                Remove-Item -Recurse -Force $stale -ErrorAction SilentlyContinue
                if (Test-Path $stale) {
                    Write-Host "    WARNING: could not remove $stale (permission?); a build may still pull stale files from it" -ForegroundColor Yellow
                }
                $leaked++
            }
        }
    }
}
