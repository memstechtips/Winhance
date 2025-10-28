using System.Text;

namespace Winhance.Core.Features.SoftwareApps.Utilities;

public static class BloatRemovalScriptGenerator
{
    public static string GenerateScript(
        List<string> packages,
        List<string> capabilities,
        List<string> optionalFeatures,
        List<string> specialApps,
        bool includeXboxRegistryFix = false)
    {
        var sb = new StringBuilder();

        AppendHeader(sb);
        AppendLoggingSetup(sb);
        AppendArrays(sb, packages, capabilities, optionalFeatures, specialApps);
        sb.Append(GetMainRemovalLogic(includeXboxRegistryFix));

        return sb.ToString();
    }

    private static void AppendHeader(StringBuilder sb)
    {
        sb.AppendLine("<#");
        sb.AppendLine("  .SYNOPSIS");
        sb.AppendLine("      Removes Windows bloatware apps, legacy capabilities, and optional features from Windows 10/11 systems.");
        sb.AppendLine();
        sb.AppendLine("  .DESCRIPTION");
        sb.AppendLine("      This script removes selected Windows components including:");
        sb.AppendLine("      - Appx packages (UWP apps like Calculator, Weather, etc.)");
        sb.AppendLine("      - Legacy Windows capabilities");
        sb.AppendLine("      - Optional Windows features");
        sb.AppendLine("      - Special apps requiring custom uninstall procedures (e.g., OneNote)");
        sb.AppendLine();
        sb.AppendLine("      The script includes retry logic and verification to ensure complete removal.");
        sb.AppendLine("      This script is designed to run in any context: user sessions, SYSTEM account, or scheduled tasks.");
        sb.AppendLine();
        sb.AppendLine("  .NOTES");
        sb.AppendLine("      Source: https://github.com/memstechtips/Winhance");
        sb.AppendLine();
        sb.AppendLine("      Requirements:");
        sb.AppendLine("      - Windows 10/11");
        sb.AppendLine("      - Administrator privileges (script will auto-elevate)");
        sb.AppendLine("      - PowerShell 5.1 or higher");
        sb.AppendLine("#>");
        sb.AppendLine();
        sb.AppendLine("# Check if script is running as Administrator");
        sb.AppendLine("If (!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]\"Administrator\")) {");
        sb.AppendLine("    Try {");
        sb.AppendLine("        Start-Process PowerShell.exe -ArgumentList (\"-NoProfile -ExecutionPolicy Bypass -File `\"{0}`\"\" -f $PSCommandPath) -Verb RunAs");
        sb.AppendLine("        Exit");
        sb.AppendLine("    }");
        sb.AppendLine("    Catch {");
        sb.AppendLine("        Write-Host \"Failed to run as Administrator. Please rerun with elevated privileges.\"");
        sb.AppendLine("        Exit");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static void AppendLoggingSetup(StringBuilder sb)
    {
        sb.AppendLine("# Setup logging");
        sb.AppendLine("$logFolder = \"C:\\ProgramData\\Winhance\\Logs\"");
        sb.AppendLine("$logFile = \"$logFolder\\BloatRemovalLog.txt\"");
        sb.AppendLine();
        sb.AppendLine("# Create log directory if it doesn't exist");
        sb.AppendLine("if (!(Test-Path $logFolder)) {");
        sb.AppendLine("    New-Item -ItemType Directory -Path $logFolder -Force | Out-Null");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("# Function to write to log file");
        sb.AppendLine("function Write-Log {");
        sb.AppendLine("    param (");
        sb.AppendLine("        [string]$Message");
        sb.AppendLine("    )");
        sb.AppendLine("    ");
        sb.AppendLine("    # Check if log file exists and is over 500KB (512000 bytes)");
        sb.AppendLine("    if ((Test-Path $logFile) -and (Get-Item $logFile).Length -gt 512000) {");
        sb.AppendLine("        Remove-Item $logFile -Force -ErrorAction SilentlyContinue");
        sb.AppendLine("        $timestamp = Get-Date -Format \"yyyy-MM-dd HH:mm:ss\"");
        sb.AppendLine("        \"$timestamp - Log rotated - previous log exceeded 500KB\" | Out-File -FilePath $logFile");
        sb.AppendLine("    }");
        sb.AppendLine("    ");
        sb.AppendLine("    $timestamp = Get-Date -Format \"yyyy-MM-dd HH:mm:ss\"");
        sb.AppendLine("    \"$timestamp - $Message\" | Out-File -FilePath $logFile -Append");
        sb.AppendLine("    ");
        sb.AppendLine("    # Also output to console for real-time progress");
        sb.AppendLine("    Write-Host $Message");
        sb.AppendLine("}");
        sb.AppendLine("Write-Log \"Starting bloat removal process\"");
        sb.AppendLine();
        sb.AppendLine("# Enable Remove-AppX -AllUsers compatibility aliases for this session");
        sb.AppendLine("Write-Log \"Setting up AppX compatibility aliases for this session...\"");
        sb.AppendLine("try {");
        sb.AppendLine("    Set-Alias Get-AppPackageAutoUpdateSettings Get-AppxPackageAutoUpdateSettings -Scope Global -Force");
        sb.AppendLine("    Set-Alias Remove-AppPackageAutoUpdateSettings Remove-AppxPackageAutoUpdateSettings -Scope Global -Force");
        sb.AppendLine("    Set-Alias Set-AppPackageAutoUpdateSettings Set-AppxPackageAutoUpdateSettings -Scope Global -Force");
        sb.AppendLine("    Set-Alias Reset-AppPackage Reset-AppxPackage -Scope Global -Force");
        sb.AppendLine("    Set-Alias Add-MsixPackage Add-AppxPackage -Scope Global -Force");
        sb.AppendLine("    Set-Alias Get-MsixPackage Get-AppxPackage -Scope Global -Force");
        sb.AppendLine("    Set-Alias Remove-MsixPackage Remove-AppxPackage -Scope Global -Force");
        sb.AppendLine("    Write-Log \"AppX compatibility aliases created successfully\"");
        sb.AppendLine("} catch {");
        sb.AppendLine("    Write-Log \"Warning: Could not create some AppX aliases: $($_.Exception.Message)\"");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static void AppendArrays(StringBuilder sb, List<string> packages, List<string> capabilities, List<string> optionalFeatures, List<string> specialApps)
    {
        sb.AppendLine("# Packages to remove");
        sb.AppendLine("$packages = @(");
        foreach (var package in packages)
        {
            sb.AppendLine($"    '{package}'");
        }
        sb.AppendLine(")");
        sb.AppendLine();

        sb.AppendLine("# Capabilities to remove");
        sb.AppendLine("$capabilities = @(");
        foreach (var capability in capabilities)
        {
            sb.AppendLine($"    '{capability}'");
        }
        sb.AppendLine(")");
        sb.AppendLine();

        sb.AppendLine("# Optional Features to disable");
        sb.AppendLine("$optionalFeatures = @(");
        foreach (var feature in optionalFeatures)
        {
            sb.AppendLine($"    '{feature}'");
        }
        sb.AppendLine(")");
        sb.AppendLine();

        sb.AppendLine("# Special apps requiring uninstall string execution");
        sb.AppendLine("$specialApps = @(");
        foreach (var app in specialApps)
        {
            sb.AppendLine($"    '{app}'");
        }
        sb.AppendLine(")");
        sb.AppendLine();
    }

    private static string GetMainRemovalLogic(bool includeXboxRegistryFix)
    {
        var xboxRegistryFix = includeXboxRegistryFix ? @"

# ============================================================================
# REGISTRY SETTINGS TO PREVENT ISSUES AND BUGS
# ============================================================================

$xboxPackages = @('Microsoft.GamingApp', 'Microsoft.XboxGamingOverlay', 'Microsoft.XboxGameOverlay')
$hasXboxPackages = $packages | Where-Object { $xboxPackages -contains $_ }

if ($hasXboxPackages) {
    Write-Log ""Applying registry settings to prevent post-removal issues...""

    try {
        $runningAsSystem = ($env:USERNAME -eq ""SYSTEM"" -or $env:USERPROFILE -like ""*\system32\config\systemprofile"")

        if ($runningAsSystem) {
            Write-Log ""Running as SYSTEM - detecting logged-in user...""
            $loggedInUser = (Get-WmiObject -Class Win32_ComputerSystem -ErrorAction SilentlyContinue).UserName

            if ($loggedInUser -and $loggedInUser -ne ""NT AUTHORITY\SYSTEM"") {
                $username = $loggedInUser.Split('\\')[1]
                try {
                    $sid = (New-Object System.Security.Principal.NTAccount($username)).Translate([System.Security.Principal.SecurityIdentifier]).Value
                    Write-Log ""Applying settings for user: $username (SID: $sid)""

                    reg add ""HKU\$sid\SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR"" /f /t REG_DWORD /v ""AppCaptureEnabled"" /d 0 2>$null | Out-Null
                    reg add ""HKU\$sid\System\GameConfigStore"" /f /t REG_DWORD /v ""GameDVR_Enabled"" /d 0 2>$null | Out-Null

                    Write-Log ""Xbox Game DVR registry settings applied successfully""
                } catch {
                    Write-Log ""Warning: Could not apply Xbox Game DVR registry settings: $($_.Exception.Message)""
                }
            } else {
                Write-Log ""Warning: Could not detect logged-in user for registry settings""
            }
        } else {
            Write-Log ""Running as user - applying settings directly to HKCU""
            reg add ""HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR"" /f /t REG_DWORD /v ""AppCaptureEnabled"" /d 0 2>$null | Out-Null
            reg add ""HKCU\System\GameConfigStore"" /f /t REG_DWORD /v ""GameDVR_Enabled"" /d 0 2>$null | Out-Null
            Write-Log ""Xbox Game DVR registry settings applied successfully""
        }
    } catch {
        Write-Log ""Warning: Could not apply Xbox Game DVR registry settings: $($_.Exception.Message)""
    }
}

" : "";

        return @"$maxRetries = 3
$retryCount = 0

do {
    $retryCount++
    Write-Log ""Standard removal attempt $retryCount of $maxRetries""

    Write-Log ""Discovering all packages...""
    $allInstalledPackages = Get-AppxPackage -AllUsers -ErrorAction SilentlyContinue
    $allProvisionedPackages = Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue

    Write-Log ""Processing packages...""
    $packagesToRemove = @()
    $provisionedPackagesToRemove = @()
    $notFoundPackages = @()

    foreach ($package in $packages) {
        $foundAny = $false

        $installedPackages = $allInstalledPackages | Where-Object { $_.Name -eq $package }
        if ($installedPackages) {
            Write-Log ""Found installed package: $package""
            foreach ($pkg in $installedPackages) {
                Write-Log ""Queuing installed package for removal: $($pkg.PackageFullName)""
                $packagesToRemove += $pkg.PackageFullName
            }
            $foundAny = $true
        }

        $provisionedPackages = $allProvisionedPackages | Where-Object { $_.DisplayName -eq $package }
        if ($provisionedPackages) {
            Write-Log ""Found provisioned package: $package""
            foreach ($pkg in $provisionedPackages) {
                Write-Log ""Queuing provisioned package for removal: $($pkg.PackageName)""
                $provisionedPackagesToRemove += $pkg.PackageName
            }
            $foundAny = $true
        }

        if (-not $foundAny) {
            $notFoundPackages += $package
        }
    }

    if ($notFoundPackages.Count -gt 0) {
        Write-Log ""Packages not found: $($notFoundPackages -join ', ')""
    }

    if ($packagesToRemove.Count -gt 0) {
        Write-Log ""Removing $($packagesToRemove.Count) installed packages in batch...""
        try {
            $packagesToRemove | ForEach-Object {
                Write-Log ""Removing installed package: $_""
                Remove-AppxPackage -Package $_ -AllUsers -ErrorAction SilentlyContinue
            }
            Write-Log ""Batch removal of installed packages completed""
        } catch {
            Write-Log ""Error in batch removal of installed packages: $($_.Exception.Message)""
        }
    }

    if ($provisionedPackagesToRemove.Count -gt 0) {
        Write-Log ""Removing $($provisionedPackagesToRemove.Count) provisioned packages...""
        foreach ($pkgName in $provisionedPackagesToRemove) {
            try {
                Write-Log ""Removing provisioned package: $pkgName""
                Remove-AppxProvisionedPackage -Online -PackageName $pkgName -ErrorAction SilentlyContinue
            } catch {
                Write-Log ""Error removing provisioned package $pkgName : $($_.Exception.Message)""
            }
        }
        Write-Log ""Provisioned packages removal completed""
    }

    Write-Log ""Processing capabilities...""
    foreach ($capability in $capabilities) {
        Write-Log ""Checking capability: $capability""
        try {
            $matchingCapabilities = Get-WindowsCapability -Online | Where-Object { $_.Name -like ""$capability*"" -or $_.Name -like ""$capability~~~~*"" }

            if ($matchingCapabilities) {
                $foundInstalled = $false
                foreach ($existingCapability in $matchingCapabilities) {
                    if ($existingCapability.State -eq ""Installed"") {
                        $foundInstalled = $true
                        Write-Log ""Removing capability: $($existingCapability.Name)""
                        Remove-WindowsCapability -Online -Name $existingCapability.Name -ErrorAction SilentlyContinue | Out-Null
                    }
                }

                if (-not $foundInstalled) {
                    Write-Log ""Found capability $capability but it is not installed""
                }
            }
            else {
                Write-Log ""No matching capabilities found for: $capability""
            }
        }
        catch {
            Write-Log ""Error checking capability: $capability - $($_.Exception.Message)""
        }
    }

    Write-Log ""Processing optional features...""
    if ($optionalFeatures.Count -gt 0) {
        $enabledFeatures = @()
        foreach ($feature in $optionalFeatures) {
            Write-Log ""Checking feature: $feature""
            $existingFeature = Get-WindowsOptionalFeature -Online -FeatureName $feature -ErrorAction SilentlyContinue
            if ($existingFeature -and $existingFeature.State -eq ""Enabled"") {
                $enabledFeatures += $feature
            } else {
                Write-Log ""Feature not found or not enabled: $feature""
            }
        }

        if ($enabledFeatures.Count -gt 0) {
            Write-Log ""Disabling features: $($enabledFeatures -join ', ')""
            Disable-WindowsOptionalFeature -Online -FeatureName $enabledFeatures -NoRestart -ErrorAction SilentlyContinue | Out-Null
        }
    }

    Write-Log ""Verifying removal results...""
    $remainingItems = @()

    $currentPackages = Get-AppxPackage -AllUsers -ErrorAction SilentlyContinue
    foreach ($package in $packages) {
        if ($currentPackages | Where-Object { $_.Name -eq $package }) {
            $remainingItems += $package
            Write-Log ""Package still installed: $package""
        }
    }

    $currentCapabilities = Get-WindowsCapability -Online -ErrorAction SilentlyContinue | Where-Object State -eq 'Installed'
    foreach ($capability in $capabilities) {
        if ($currentCapabilities | Where-Object { $_.Name -like ""$capability*"" }) {
            $remainingItems += $capability
            Write-Log ""Capability still installed: $capability""
        }
    }

    $currentFeatures = Get-WindowsOptionalFeature -Online -ErrorAction SilentlyContinue | Where-Object State -eq 'Enabled'
    foreach ($feature in $optionalFeatures) {
        if ($currentFeatures | Where-Object { $_.FeatureName -eq $feature }) {
            $remainingItems += $feature
            Write-Log ""Feature still enabled: $feature""
        }
    }

    if ($remainingItems.Count -eq 0) {
        Write-Log ""All standard items successfully removed!""
        break
    } else {
        Write-Log ""Retry needed. $($remainingItems.Count) items remain: $($remainingItems -join ', ')""
        if ($retryCount -lt $maxRetries) {
            Write-Log ""Waiting 2 seconds before retry...""
            Start-Sleep -Seconds 2
        }
    }

} while ($retryCount -lt $maxRetries -and $remainingItems.Count -gt 0)

if ($remainingItems.Count -gt 0) {
    Write-Log ""Warning: $($remainingItems.Count) standard items could not be removed after $maxRetries attempts: $($remainingItems -join ', ')""
}

if ($specialApps.Count -gt 0) {
    Write-Log ""Processing special apps that require custom uninstall procedures...""

    $maxSpecialRetries = 2
    $specialRetryCount = 0
    $specialAppsRemaining = @()

    do {
        $specialRetryCount++
        if ($specialRetryCount -gt 1) {
            Write-Log ""Special apps retry attempt $specialRetryCount of $maxSpecialRetries""
        }

        $uninstallBasePaths = @(
            'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
            'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall'
        )

        foreach ($specialApp in $specialApps) {
            Write-Log ""Processing special app: $specialApp""

            switch ($specialApp) {
                'OneNote' {
                    $processesToStop = @('OneNote', 'ONENOTE', 'ONENOTEM')
                    $searchPattern = 'OneNote*'
                    $packagePattern = '*OneNote*'
                }
                default {
                    Write-Log ""Unknown or unsupported special app: $specialApp""
                    continue
                }
            }

            foreach ($processName in $processesToStop) {
                $processes = Get-Process -Name $processName -ErrorAction SilentlyContinue
                if ($processes) {
                    $processes | Stop-Process -Force -ErrorAction SilentlyContinue
                    Write-Log ""Stopped process: $processName""
                }
            }

            $uninstallExecuted = $false
            foreach ($uninstallBasePath in $uninstallBasePaths) {
                try {
                    Write-Log ""Searching for $searchPattern in $uninstallBasePath""
                    $uninstallKeys = Get-ChildItem -Path $uninstallBasePath -ErrorAction SilentlyContinue |
                                    Where-Object { $_.PSChildName -like $searchPattern }

                    foreach ($key in $uninstallKeys) {
                        try {
                            $uninstallString = (Get-ItemProperty -Path $key.PSPath -ErrorAction SilentlyContinue).UninstallString
                            if ($uninstallString) {
                                Write-Log ""Found uninstall string: $uninstallString""

                                if ($uninstallString -match '^\""([^\""]+)\""(.*)$') {
                                    $exePath = $matches[1]
                                    $args = $matches[2].Trim()

                                    if ($exePath -like '*OfficeClickToRun.exe') {
                                        $args += ' DisplayLevel=False'
                                    } else {
                                        $args += ' /silent'
                                    }

                                    Write-Log ""Executing: $exePath with args: $args""
                                    Start-Process -FilePath $exePath -ArgumentList $args -NoNewWindow -Wait -ErrorAction SilentlyContinue
                                } else {
                                    if ($uninstallString -like '*OfficeClickToRun.exe*') {
                                        Start-Process -FilePath $uninstallString -ArgumentList 'DisplayLevel=False' -NoNewWindow -Wait -ErrorAction SilentlyContinue
                                    } else {
                                        Start-Process -FilePath $uninstallString -ArgumentList '/silent' -NoNewWindow -Wait -ErrorAction SilentlyContinue
                                    }
                                }

                                $uninstallExecuted = $true
                                Write-Log ""Completed uninstall execution for $specialApp""
                            }
                        }
                        catch {
                            Write-Log ""Error processing uninstall key: $($_.Exception.Message)""
                        }
                    }
                }
                catch {
                    Write-Log ""Error searching for uninstall keys: $($_.Exception.Message)""
                }
            }

            if (-not $uninstallExecuted) {
                Write-Log ""No uninstall strings found for $specialApp""
            }
        }

        if ($specialRetryCount -eq 1) {
            Write-Log ""Waiting 3 seconds for uninstallers to complete...""
            Start-Sleep -Seconds 3
        }

        Write-Log ""Verifying special apps removal...""
        $specialAppsRemaining = @()

        foreach ($specialApp in $specialApps) {
            $stillExists = $false

            switch ($specialApp) {
                'OneNote' {
                    $appxPackage = Get-AppxPackage -AllUsers -ErrorAction SilentlyContinue |
                                   Where-Object { $_.Name -like '*OneNote*' }
                    if ($appxPackage) {
                        $stillExists = $true
                        Write-Log ""OneNote AppxPackage still exists: $($appxPackage.PackageFullName)""
                    }

                    $uninstallKeys = Get-ChildItem -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall' -ErrorAction SilentlyContinue |
                                     Where-Object { $_.PSChildName -like 'OneNote*' }
                    if ($uninstallKeys) {
                        $stillExists = $true
                        Write-Log ""OneNote registry uninstall keys still exist""
                    }
                }
            }

            if ($stillExists) {
                $specialAppsRemaining += $specialApp
            }
        }

        if ($specialAppsRemaining.Count -eq 0) {
            Write-Log ""All special apps successfully removed!""
            break
        } else {
            Write-Log ""$($specialAppsRemaining.Count) special apps remain: $($specialAppsRemaining -join ', ')""
            if ($specialRetryCount -lt $maxSpecialRetries) {
                Write-Log ""Waiting 3 seconds before retry...""
                Start-Sleep -Seconds 3
            }
        }

    } while ($specialRetryCount -lt $maxSpecialRetries -and $specialAppsRemaining.Count -gt 0)

    if ($specialAppsRemaining.Count -gt 0) {
        Write-Log ""Warning: $($specialAppsRemaining.Count) special apps could not be removed after $maxSpecialRetries attempts: $($specialAppsRemaining -join ', ')""
    }
}
" + xboxRegistryFix + @"
Write-Log ""Bloat removal process completed""
";
    }

}
