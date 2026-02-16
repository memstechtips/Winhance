<#
.SYNOPSIS
    Downloads and extracts standalone WinGet CLI binaries from the latest GitHub release.

.DESCRIPTION
    Fetches the latest (or pinned) WinGet CLI release from GitHub, extracts the x64 MSIX
    from the MSIX bundle, and copies only the needed binaries into the project tree.

.PARAMETER Version
    Optional. A specific release tag to download (e.g. "v1.10.340"). Defaults to "latest".

.EXAMPLE
    .\Update-BundledWinGet.ps1
    .\Update-BundledWinGet.ps1 -Version "v1.10.340"
#>
[CmdletBinding()]
param(
    [string]$Version = "latest"
)

$ErrorActionPreference = 'Stop'

$OutputDir = Join-Path $PSScriptRoot "..\src\Winhance.Infrastructure\Features\SoftwareApps\Services\WinGet\winget-cli"
$TempDir   = Join-Path ([System.IO.Path]::GetTempPath()) "winhance-winget-update"

# Files we need from the extracted MSIX
# NOTE: resources.pri is deliberately excluded — it conflicts with the WinUI PRI
# generator (duplicate 'Files/App.xbf') and the standalone CLI works without it.
$NeededFiles = @(
    'winget.exe'
    'WindowsPackageManager.dll'
    'Microsoft.Management.Configuration.dll'
    'Microsoft.Web.WebView2.Core.dll'
    'concrt140_app.dll'
    'msvcp140_app.dll'
    'msvcp140_1_app.dll'
    'msvcp140_2_app.dll'
    'msvcp140_atomic_wait_app.dll'
    'msvcp140_codecvt_ids_app.dll'
    'vcamp140_app.dll'
    'vccorlib140_app.dll'
    'vcomp140_app.dll'
    'vcruntime140_app.dll'
    'vcruntime140_1_app.dll'
)

function Get-ReleaseInfo {
    if ($Version -eq "latest") {
        $url = "https://api.github.com/repos/microsoft/winget-cli/releases/latest"
    } else {
        $url = "https://api.github.com/repos/microsoft/winget-cli/releases/tags/$Version"
    }
    Write-Host "Fetching release info from $url ..."
    $response = Invoke-RestMethod -Uri $url -Headers @{ 'User-Agent' = 'Winhance-Build' }
    return $response
}

try {
    # Clean up any previous temp files
    if (Test-Path $TempDir) { Remove-Item $TempDir -Recurse -Force }
    New-Item -ItemType Directory -Path $TempDir -Force | Out-Null

    # 1. Get release info
    $release = Get-ReleaseInfo
    $tag = $release.tag_name
    Write-Host "Release: $tag"

    # 2. Find the .msixbundle asset
    $bundleAsset = $release.assets | Where-Object { $_.name -like '*.msixbundle' } | Select-Object -First 1
    if (-not $bundleAsset) {
        throw "No .msixbundle asset found in release $tag"
    }

    $bundlePath = Join-Path $TempDir $bundleAsset.name
    Write-Host "Downloading $($bundleAsset.name) ($([math]::Round($bundleAsset.size / 1MB, 1)) MB) ..."
    Invoke-WebRequest -Uri $bundleAsset.browser_download_url -OutFile $bundlePath -UseBasicParsing

    # 3. Extract the bundle (rename to .zip since Expand-Archive requires .zip extension)
    $bundleExtract = Join-Path $TempDir "bundle"
    $bundleZip = [System.IO.Path]::ChangeExtension($bundlePath, '.zip')
    Write-Host "Extracting MSIX bundle ..."
    Copy-Item -Path $bundlePath -Destination $bundleZip -Force
    Expand-Archive -Path $bundleZip -DestinationPath $bundleExtract -Force

    $x64Msix = Get-ChildItem -Path $bundleExtract -Filter '*x64*.msix' | Select-Object -First 1
    if (-not $x64Msix) {
        # Fallback: look for any x64 package
        $x64Msix = Get-ChildItem -Path $bundleExtract -Filter '*x64*' | Select-Object -First 1
    }
    if (-not $x64Msix) {
        Write-Host "Available files in bundle:"
        Get-ChildItem -Path $bundleExtract | ForEach-Object { Write-Host "  $_" }
        throw "No x64 MSIX found in the bundle"
    }

    # 4. Extract the x64 MSIX (rename to .zip for Expand-Archive)
    $msixExtract = Join-Path $TempDir "msix"
    $msixZip = [System.IO.Path]::ChangeExtension($x64Msix.FullName, '.zip')
    Write-Host "Extracting $($x64Msix.Name) ..."
    Copy-Item -Path $x64Msix.FullName -Destination $msixZip -Force
    Expand-Archive -Path $msixZip -DestinationPath $msixExtract -Force

    # 5. Copy needed files to output directory
    if (Test-Path $OutputDir) { Remove-Item $OutputDir -Recurse -Force }
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

    $copied = 0
    $missing = @()
    foreach ($file in $NeededFiles) {
        $source = Get-ChildItem -Path $msixExtract -Filter $file -Recurse | Select-Object -First 1
        if ($source) {
            Copy-Item -Path $source.FullName -Destination (Join-Path $OutputDir $file) -Force
            $copied++
        } else {
            $missing += $file
        }
    }

    # Also check for libsmartscreenn.dll (may not exist in all versions)
    $smartscreen = Get-ChildItem -Path $msixExtract -Filter 'libsmartscreenn.dll' -Recurse | Select-Object -First 1
    if ($smartscreen) {
        Copy-Item -Path $smartscreen.FullName -Destination (Join-Path $OutputDir 'libsmartscreenn.dll') -Force
        $copied++
    }

    # Also check for WindowsPackageManagerServer.exe
    $server = Get-ChildItem -Path $msixExtract -Filter 'WindowsPackageManagerServer.exe' -Recurse | Select-Object -First 1
    if ($server) {
        Copy-Item -Path $server.FullName -Destination (Join-Path $OutputDir 'WindowsPackageManagerServer.exe') -Force
        $copied++
    }

    Write-Host ""
    Write-Host "Copied $copied files to $OutputDir"
    if ($missing.Count -gt 0) {
        Write-Warning "Missing files (may not exist in this release): $($missing -join ', ')"
    }

    # Verify winget.exe works
    $wingetExe = Join-Path $OutputDir "winget.exe"
    if (Test-Path $wingetExe) {
        Write-Host ""
        Write-Host "Verifying winget.exe ..."
        $versionOutput = & $wingetExe --version 2>&1
        Write-Host "Bundled WinGet version: $versionOutput"
    }

    Write-Host ""
    Write-Host "Done! WinGet CLI $tag binaries are ready."

} finally {
    # Clean up temp files
    if (Test-Path $TempDir) {
        Remove-Item $TempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
