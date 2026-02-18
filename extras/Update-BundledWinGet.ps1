<#
.SYNOPSIS
    Downloads and extracts standalone WinGet CLI binaries and VC++ Runtime DLLs.

.DESCRIPTION
    Fetches the latest (or pinned) WinGet CLI release from GitHub, extracts the x64 MSIX
    from the MSIX bundle, and copies only the needed binaries into the project tree.

    Also ensures the system has the latest Visual C++ Redistributable installed, then
    copies the desktop runtime DLLs (vcruntime140.dll, vcruntime140_1.dll, msvcp140.dll)
    into the bundle. These are needed for winget.exe to run on minimal Windows
    installations (e.g. LTSC) that lack the VC++ Runtime. The _app.dll variants in the
    MSIX are for UWP/AppContainer use only and do not satisfy desktop dependencies.

.PARAMETER Version
    Optional. A specific WinGet release tag to download (e.g. "v1.10.340").
    Defaults to "latest".

.PARAMETER Force
    Bypass all version checks and re-download everything regardless of current state.

.EXAMPLE
    .\Update-BundledWinGet.ps1
    .\Update-BundledWinGet.ps1 -Version "v1.10.340"
    .\Update-BundledWinGet.ps1 -Force
#>
[CmdletBinding()]
param(
    [string]$Version = "latest",
    [switch]$Force
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

# Desktop VC++ Runtime DLLs needed for non-packaged (Process.Start) execution.
# These are NOT in the WinGet MSIX — they come from the system VC++ Redistributable.
# Each entry is the desktop counterpart of an _app.dll listed in $NeededFiles above.
$VcRuntimeDlls = @(
    'concrt140.dll'
    'msvcp140.dll'
    'msvcp140_1.dll'
    'msvcp140_2.dll'
    'msvcp140_atomic_wait.dll'
    'msvcp140_codecvt_ids.dll'
    'vcamp140.dll'
    'vccorlib140.dll'
    'vcomp140.dll'
    'vcruntime140.dll'
    'vcruntime140_1.dll'
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

    # ============================================================
    #  1. WinGet CLI — check current vs latest version
    # ============================================================
    $wingetExe = Join-Path $OutputDir "winget.exe"
    $currentWingetVersion = $null
    if (Test-Path $wingetExe) {
        try {
            $currentWingetVersion = (& $wingetExe --version 2>&1).ToString().Trim()
        } catch {
            Write-Warning "Could not query bundled winget.exe version: $_"
        }
    }

    $release         = Get-ReleaseInfo
    $latestWingetTag = $release.tag_name

    if ($currentWingetVersion) {
        Write-Host "Current bundled WinGet: $currentWingetVersion"
    } else {
        Write-Host "Current bundled WinGet: (not found)"
    }
    Write-Host "Latest WinGet release:  $latestWingetTag"

    $updateWinget = $true
    if (-not $Force -and $currentWingetVersion -and $currentWingetVersion -eq $latestWingetTag) {
        Write-Host "  -> Up to date. Skipping WinGet CLI download." -ForegroundColor Green
        $updateWinget = $false
    } elseif ($Force -and $currentWingetVersion -eq $latestWingetTag) {
        Write-Host "  -> Up to date, but -Force specified. Re-downloading." -ForegroundColor Yellow
    } else {
        Write-Host "  -> Update available. Will download." -ForegroundColor Cyan
    }

    # ============================================================
    #  2. WinGet CLI — download and extract (if needed)
    # ============================================================
    $wingetCopied  = 0
    $wingetMissing = @()

    if ($updateWinget) {
        # 2a. Download .msixbundle
        $bundleAsset = $release.assets | Where-Object { $_.name -like '*.msixbundle' } | Select-Object -First 1
        if (-not $bundleAsset) {
            throw "No .msixbundle asset found in release $latestWingetTag"
        }

        $bundlePath = Join-Path $TempDir $bundleAsset.name
        Write-Host ""
        Write-Host "Downloading $($bundleAsset.name) ($([math]::Round($bundleAsset.size / 1MB, 1)) MB) ..."
        Invoke-WebRequest -Uri $bundleAsset.browser_download_url -OutFile $bundlePath -UseBasicParsing

        # 2b. Extract the bundle (rename to .zip for Expand-Archive)
        $bundleExtract = Join-Path $TempDir "bundle"
        $bundleZip = [System.IO.Path]::ChangeExtension($bundlePath, '.zip')
        Write-Host "Extracting MSIX bundle ..."
        Copy-Item -Path $bundlePath -Destination $bundleZip -Force
        Expand-Archive -Path $bundleZip -DestinationPath $bundleExtract -Force

        $x64Msix = Get-ChildItem -Path $bundleExtract -Filter '*x64*.msix' | Select-Object -First 1
        if (-not $x64Msix) {
            $x64Msix = Get-ChildItem -Path $bundleExtract -Filter '*x64*' | Select-Object -First 1
        }
        if (-not $x64Msix) {
            Write-Host "Available files in bundle:"
            Get-ChildItem -Path $bundleExtract | ForEach-Object { Write-Host "  $_" }
            throw "No x64 MSIX found in the bundle"
        }

        # 2c. Extract the x64 MSIX
        $msixExtract = Join-Path $TempDir "msix"
        $msixZip = [System.IO.Path]::ChangeExtension($x64Msix.FullName, '.zip')
        Write-Host "Extracting $($x64Msix.Name) ..."
        Copy-Item -Path $x64Msix.FullName -Destination $msixZip -Force
        Expand-Archive -Path $msixZip -DestinationPath $msixExtract -Force

        # 2d. Copy needed files to output directory (clean slate)
        if (Test-Path $OutputDir) { Remove-Item $OutputDir -Recurse -Force }
        New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

        foreach ($file in $NeededFiles) {
            $source = Get-ChildItem -Path $msixExtract -Filter $file -Recurse | Select-Object -First 1
            if ($source) {
                Copy-Item -Path $source.FullName -Destination (Join-Path $OutputDir $file) -Force
                $wingetCopied++
            } else {
                $wingetMissing += $file
            }
        }

        # Optional extras
        $smartscreen = Get-ChildItem -Path $msixExtract -Filter 'libsmartscreenn.dll' -Recurse | Select-Object -First 1
        if ($smartscreen) {
            Copy-Item -Path $smartscreen.FullName -Destination (Join-Path $OutputDir 'libsmartscreenn.dll') -Force
            $wingetCopied++
        }
        $server = Get-ChildItem -Path $msixExtract -Filter 'WindowsPackageManagerServer.exe' -Recurse | Select-Object -First 1
        if ($server) {
            Copy-Item -Path $server.FullName -Destination (Join-Path $OutputDir 'WindowsPackageManagerServer.exe') -Force
            $wingetCopied++
        }

        Write-Host "Copied $wingetCopied WinGet files to output directory." -ForegroundColor Green
        if ($wingetMissing.Count -gt 0) {
            Write-Warning "Missing files (may not exist in this release): $($wingetMissing -join ', ')"
        }
    }

    # ============================================================
    #  3. VC++ Runtime — ensure system is up to date, then copy
    # ============================================================
    Write-Host ""

    $systemDir = [System.Environment]::SystemDirectory

    # The core DLL must exist — the others are optional (e.g. vcamp140 is C++ AMP)
    $coreVcDll = Join-Path $systemDir "vcruntime140.dll"
    if (-not (Test-Path $coreVcDll)) {
        throw "vcruntime140.dll not found in $systemDir. " +
              "Install the Visual C++ Redistributable first: https://aka.ms/vs/17/release/vc_redist.x64.exe"
    }

    # Get the system-installed version
    $systemVcVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($coreVcDll).ProductVersion.Trim()
    Write-Host "System VC++ Runtime:   $systemVcVersion"

    # Download vc_redist.x64.exe to check the latest available version
    Write-Host "Downloading latest Visual C++ Redistributable (version check) ..."
    $vcRedistUrl  = "https://aka.ms/vs/17/release/vc_redist.x64.exe"
    $vcRedistPath = Join-Path $TempDir "vc_redist.x64.exe"
    Invoke-WebRequest -Uri $vcRedistUrl -OutFile $vcRedistPath -UseBasicParsing

    $latestVcVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($vcRedistPath).ProductVersion.Trim()
    Write-Host "Latest VC++ Redistributable: $latestVcVersion"

    # If system is behind, install the update before copying
    $vcRedistInstalled = $false
    try {
        $systemVer = [version]$systemVcVersion
        $latestVer = [version]$latestVcVersion
        if ($systemVer -lt $latestVer) {
            Write-Host "  -> System VC++ Runtime is outdated. Installing update ..." -ForegroundColor Yellow
            $installProc = Start-Process -FilePath $vcRedistPath `
                -ArgumentList "/install /passive /norestart" `
                -Wait -PassThru -NoNewWindow
            if ($installProc.ExitCode -eq 0 -or $installProc.ExitCode -eq 3010) {
                $vcRedistInstalled = $true
                # Re-read the version after install
                $systemVcVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo(
                    (Join-Path $systemDir "vcruntime140.dll")
                ).ProductVersion.Trim()
                Write-Host "  -> VC++ Redistributable updated to $systemVcVersion" -ForegroundColor Green
            } else {
                Write-Warning "VC++ Redistributable install returned exit code $($installProc.ExitCode). Continuing with existing version."
            }
        } else {
            Write-Host "  -> System VC++ Runtime is up to date." -ForegroundColor Green
        }
    } catch {
        Write-Warning "Could not compare VC++ versions: $_. Continuing with existing system DLLs."
    }

    # Check bundled DLLs vs system DLLs
    $allVcDllsExist = ($VcRuntimeDlls | ForEach-Object { Test-Path (Join-Path $OutputDir $_) }) -notcontains $false

    $currentVcVersion = $null
    if ($allVcDllsExist) {
        $currentVcVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo(
            (Join-Path $OutputDir "vcruntime140.dll")
        ).ProductVersion.Trim()
    }

    $updateVcRedist = $true
    if (-not $Force -and $allVcDllsExist -and $currentVcVersion) {
        try {
            if ([version]$currentVcVersion -ge [version]$systemVcVersion) {
                Write-Host "Bundled VC++ Runtime:  $currentVcVersion (up to date)" -ForegroundColor Green
                $updateVcRedist = $false
            } else {
                Write-Host "Bundled VC++ Runtime:  $currentVcVersion -> will update to $systemVcVersion" -ForegroundColor Cyan
            }
        } catch {
            Write-Warning "Could not compare bundled VC++ versions, will re-copy: $_"
        }
    } elseif ($Force -and $allVcDllsExist) {
        Write-Host "Bundled VC++ Runtime:  $currentVcVersion (-Force specified, re-copying)" -ForegroundColor Yellow
    } else {
        $missingDlls = $VcRuntimeDlls | Where-Object { -not (Test-Path (Join-Path $OutputDir $_)) }
        Write-Host "Bundled VC++ Runtime:  (missing: $($missingDlls -join ', '))" -ForegroundColor Cyan
    }

    # ============================================================
    #  4. VC++ Runtime — copy from System32 (if needed)
    # ============================================================
    $vcCopied = 0

    if ($updateVcRedist) {
        if (-not (Test-Path $OutputDir)) {
            New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
        }

        $vcSkipped = @()
        foreach ($dll in $VcRuntimeDlls) {
            $source = Join-Path $systemDir $dll
            if (Test-Path $source) {
                Copy-Item -Path $source -Destination (Join-Path $OutputDir $dll) -Force
                $vcCopied++
                $ver = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($source).ProductVersion.Trim()
                Write-Host "  Copied $dll ($ver)"
            } else {
                $vcSkipped += $dll
            }
        }

        if ($vcCopied -gt 0) {
            Write-Host "Copied $vcCopied VC++ Runtime desktop DLLs from System32." -ForegroundColor Green
        }
        if ($vcSkipped.Count -gt 0) {
            Write-Host "  Skipped $($vcSkipped.Count) optional DLLs not present in System32: $($vcSkipped -join ', ')" -ForegroundColor DarkGray
        }
    }

    # ============================================================
    #  5. Verify and summarize
    # ============================================================
    $wingetExe = Join-Path $OutputDir "winget.exe"
    if (Test-Path $wingetExe) {
        Write-Host ""
        Write-Host "Verifying winget.exe ..."
        try {
            $versionOutput = (& $wingetExe --version 2>&1).ToString().Trim()
            Write-Host "Bundled WinGet version: $versionOutput"
        } catch {
            Write-Warning "winget.exe --version failed: $_"
        }
    }

    # Final file inventory
    Write-Host ""
    $totalFiles = (Get-ChildItem -Path $OutputDir -File -ErrorAction SilentlyContinue).Count
    Write-Host "Output directory: $OutputDir ($totalFiles files)"

    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host " Summary" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    if ($updateWinget) {
        Write-Host "  WinGet CLI:    Updated to $latestWingetTag ($wingetCopied files)" -ForegroundColor Green
    } else {
        Write-Host "  WinGet CLI:    Up to date ($currentWingetVersion)" -ForegroundColor DarkGray
    }
    if ($vcRedistInstalled) {
        Write-Host "  VC++ Redist:   System updated to $systemVcVersion" -ForegroundColor Green
    } else {
        Write-Host "  VC++ Redist:   System up to date ($systemVcVersion)" -ForegroundColor DarkGray
    }
    if ($updateVcRedist) {
        Write-Host "  VC++ Bundle:   Copied $vcCopied DLLs ($systemVcVersion)" -ForegroundColor Green
    } else {
        Write-Host "  VC++ Bundle:   Up to date ($currentVcVersion)" -ForegroundColor DarkGray
    }
    Write-Host "========================================" -ForegroundColor Cyan

} finally {
    # Clean up temp files
    if (Test-Path $TempDir) {
        Remove-Item $TempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
