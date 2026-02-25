using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace Winhance.Core.Features.SoftwareApps.Utilities;

public static class BloatRemovalScriptGenerator
{
    public const string ScriptVersion = "2.1";
    private static readonly ConcurrentDictionary<string, Regex> ArrayPatternCache = new();

    public static string GenerateScript(
        List<string> packages,
        List<string> capabilities,
        List<string> optionalFeatures,
        List<string> specialApps,
        bool includeXboxRegistryFix = false,
        bool includeTeamsProcessKill = false)
    {
        var sb = new StringBuilder();

        AppendHeader(sb);
        AppendLoggingSetup(sb);
        AppendRunspaceHelper(sb);
        AppendArrays(sb, packages, capabilities, optionalFeatures, specialApps);
        sb.Append(GetMainRemovalLogic(includeXboxRegistryFix, includeTeamsProcessKill));

        return sb.ToString();
    }

    public static List<string> ExtractArrayFromScript(string content, string arrayName)
    {
        var regex = ArrayPatternCache.GetOrAdd(arrayName, name =>
        {
            var pattern = $@"\${name}\s*=\s*@\(\s*(.*?)\s*\)";
            return new Regex(pattern, RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);
        });
        var match = regex.Match(content);

        if (!match.Success) return new List<string>();

        var arrayContent = match.Groups[1].Value;
        var items = arrayContent
            .Split('\n')
            .Select(line => line.Trim().Trim(',').Trim('\'', '"'))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();

        return items;
    }

    public static string UpdateScriptTemplate(string existingContent)
    {
        var packages = ExtractArrayFromScript(existingContent, "packages");
        var capabilities = ExtractArrayFromScript(existingContent, "capabilities");
        var optionalFeatures = ExtractArrayFromScript(existingContent, "optionalFeatures");
        var specialApps = ExtractArrayFromScript(existingContent, "specialApps");

        var xboxPackages = new[] { "Microsoft.GamingApp", "Microsoft.XboxGamingOverlay", "Microsoft.XboxGameOverlay" };
        var includeXboxRegistryFix = packages.Any(p => xboxPackages.Contains(p, StringComparer.OrdinalIgnoreCase));
        var includeTeamsProcessKill = packages.Any(p => p.Equals("MSTeams", StringComparison.OrdinalIgnoreCase));

        return GenerateScript(packages, capabilities, optionalFeatures, specialApps, includeXboxRegistryFix, includeTeamsProcessKill);
    }

    private static void AppendHeader(StringBuilder sb)
    {
        sb.AppendLine("<#");
        sb.AppendLine("  .SYNOPSIS");
        sb.AppendLine("      Removes Windows bloatware apps, legacy capabilities, and optional features from Windows 10/11 systems.");
        sb.AppendLine("      Script Version: " + ScriptVersion);
        sb.AppendLine();
        sb.AppendLine("  .DESCRIPTION");
        sb.AppendLine("      This script removes selected Windows components including:");
        sb.AppendLine("      - Appx packages (UWP apps like Calculator, Weather, etc.)");
        sb.AppendLine("      - Legacy Windows capabilities");
        sb.AppendLine("      - Optional Windows features");
        sb.AppendLine("      - Special apps requiring custom uninstall procedures (e.g., OneNote)");
        sb.AppendLine();
        sb.AppendLine("      Provisioned packages are removed first to ensure Remove-AppxPackage -AllUsers succeeds on Win10.");
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
        sb.AppendLine("if (!(Test-Path $logFolder)) {");
        sb.AppendLine("    New-Item -ItemType Directory -Path $logFolder -Force | Out-Null");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("function Write-Log {");
        sb.AppendLine("    param ([string]$Message)");
        sb.AppendLine("    if ((Test-Path $logFile) -and (Get-Item $logFile).Length -gt 512000) {");
        sb.AppendLine("        Remove-Item $logFile -Force -ErrorAction SilentlyContinue");
        sb.AppendLine("        \"$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') - Log rotated - previous log exceeded 500KB\" | Out-File -FilePath $logFile");
        sb.AppendLine("    }");
        sb.AppendLine("    \"$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') - $Message\" | Out-File -FilePath $logFile -Append");
        sb.AppendLine("    Write-Host $Message");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static void AppendRunspaceHelper(StringBuilder sb)
    {
        sb.AppendLine("function Invoke-RunspacePool {");
        sb.AppendLine("    param (");
        sb.AppendLine("        [array]$Items,");
        sb.AppendLine("        [scriptblock]$ScriptBlock,");
        sb.AppendLine("        [int]$MaxThreads = 10,");
        sb.AppendLine("        [string]$Label,");
        sb.AppendLine("        [string]$SuccessFormat,");
        sb.AppendLine("        [string]$FailFormat");
        sb.AppendLine("    )");
        sb.AppendLine("    if ($Items.Count -eq 0) { return }");
        sb.AppendLine();
        sb.AppendLine("    $threadCount = [Math]::Min($Items.Count, $MaxThreads)");
        sb.AppendLine("    Write-Log \"Removing $($Items.Count) $Label via runspace pool (threads=$threadCount)...\"");
        sb.AppendLine("    $pool = [RunspaceFactory]::CreateRunspacePool(1, $threadCount)");
        sb.AppendLine("    $pool.Open()");
        sb.AppendLine("    $jobs = [System.Collections.Generic.List[object]]::new()");
        sb.AppendLine();
        sb.AppendLine("    foreach ($item in $Items) {");
        sb.AppendLine("        $ps = [PowerShell]::Create().AddScript($ScriptBlock).AddArgument($item)");
        sb.AppendLine("        $ps.RunspacePool = $pool");
        sb.AppendLine("        $jobs.Add(@{ Pipe = $ps; Handle = $ps.BeginInvoke() })");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    foreach ($job in $jobs) {");
        sb.AppendLine("        $result = $job.Pipe.EndInvoke($job.Handle)");
        sb.AppendLine("        foreach ($r in $result) {");
        sb.AppendLine("            if ($r.Success) { Write-Log ($SuccessFormat -f $r.Name) }");
        sb.AppendLine("            else { Write-Log ($FailFormat -f $r.Name, $r.Error) }");
        sb.AppendLine("        }");
        sb.AppendLine("        $job.Pipe.Dispose()");
        sb.AppendLine("    }");
        sb.AppendLine("    $pool.Close()");
        sb.AppendLine("    $pool.Dispose()");
        sb.AppendLine("    Write-Log \"Parallel $Label removal completed\"");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static void AppendArrays(StringBuilder sb, List<string> packages, List<string> capabilities, List<string> optionalFeatures, List<string> specialApps)
    {
        sb.AppendLine("# ============================================================================");
        sb.AppendLine("# DATA");
        sb.AppendLine("# ============================================================================");
        sb.AppendLine();
        sb.AppendLine("$packages = @(");
        foreach (var package in packages)
        {
            sb.AppendLine($"    '{package}'");
        }
        sb.AppendLine(")");
        sb.AppendLine();

        sb.AppendLine("$capabilities = @(");
        foreach (var capability in capabilities)
        {
            sb.AppendLine($"    '{capability}'");
        }
        sb.AppendLine(")");
        sb.AppendLine();

        sb.AppendLine("$optionalFeatures = @(");
        foreach (var feature in optionalFeatures)
        {
            sb.AppendLine($"    '{feature}'");
        }
        sb.AppendLine(")");
        sb.AppendLine();

        sb.AppendLine("$specialApps = @(");
        foreach (var app in specialApps)
        {
            sb.AppendLine($"    '{app}'");
        }
        sb.AppendLine(")");
        sb.AppendLine();
    }

    private static string GetMainRemovalLogic(bool includeXboxRegistryFix, bool includeTeamsProcessKill)
    {
        var teamsProcessKill = includeTeamsProcessKill ? @"
# Stop Microsoft Teams process before removal to avoid long removal delays
Get-Process | Where-Object { $_.Name -like '*teams*' } | Stop-Process -Force -ErrorAction SilentlyContinue

" : "";

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
                $sid = $null
                $profListPath = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList'
                foreach ($profKey in Get-ChildItem $profListPath -ErrorAction SilentlyContinue) {
                    $profPath = (Get-ItemProperty $profKey.PSPath -ErrorAction SilentlyContinue).ProfileImagePath
                    if ($profPath -and $profPath.EndsWith(""\$username"")) {
                        $sid = $profKey.PSChildName
                        break
                    }
                }
                if ($sid) {
                    Write-Log ""Applying settings for user: $username (SID: $sid)""
                    reg add ""HKU\$sid\SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR"" /f /t REG_DWORD /v ""AppCaptureEnabled"" /d 0 2>$null | Out-Null
                    reg add ""HKU\$sid\System\GameConfigStore"" /f /t REG_DWORD /v ""GameDVR_Enabled"" /d 0 2>$null | Out-Null
                    Write-Log ""Xbox Game DVR registry settings applied successfully""
                } else {
                    Write-Log ""Warning: Could not resolve SID for user: $username""
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

        return @"# ============================================================================
# MAIN
# ============================================================================

Write-Log ""Starting bloat removal process""
" + teamsProcessKill + @"# Discover all packages upfront (single query each)
Write-Log ""Discovering all packages...""
$allInstalled = Get-AppxPackage -AllUsers -ErrorAction SilentlyContinue
$allProvisioned = Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue

$packagesToRemove = @()
$provisionedToRemove = @()
$notFound = @()

foreach ($package in $packages) {
    $installed = @($allInstalled | Where-Object Name -eq $package)
    $provisioned = @($allProvisioned | Where-Object DisplayName -eq $package)

    if ($installed) {
        foreach ($pkg in $installed) { Write-Log ""Queuing installed package: $($pkg.PackageFullName)"" }
        $packagesToRemove += $installed.PackageFullName
    }
    if ($provisioned) {
        foreach ($pkg in $provisioned) { Write-Log ""Queuing provisioned package: $($pkg.PackageName)"" }
        $provisionedToRemove += $provisioned.PackageName
    }

    if (-not $installed -and -not $provisioned) { $notFound += $package }
}

if ($notFound.Count -gt 0) { Write-Log ""Packages not found: $($notFound -join ', ')"" }

# Deprovision first — critical for Win10 (Remove-AppxPackage -AllUsers fails with 0x80070002 otherwise)
Invoke-RunspacePool -Items $provisionedToRemove -MaxThreads 10 -Label ""provisioned packages"" `
    -ScriptBlock {
        param($p)
        try {
            Remove-AppxProvisionedPackage -Online -PackageName $p -ErrorAction Stop | Out-Null
            @{ Name = $p; Success = $true; Error = $null }
        } catch {
            @{ Name = $p; Success = $false; Error = $_.Exception.Message }
        }
    } `
    -SuccessFormat ""Deprovisioned: {0}"" `
    -FailFormat ""Failed to deprovision {0}: {1}""

# Remove installed packages (for all users)
Invoke-RunspacePool -Items $packagesToRemove -MaxThreads 10 -Label ""installed packages"" `
    -ScriptBlock {
        param($p)
        try {
            Remove-AppxPackage -Package $p -AllUsers -ErrorAction Stop
            @{ Name = $p; Success = $true; Error = $null }
        } catch {
            @{ Name = $p; Success = $false; Error = $_.Exception.Message }
        }
    } `
    -SuccessFormat ""Removed installed package: {0}"" `
    -FailFormat ""Failed to remove installed package {0}: {1}""

# Capabilities — single DISM enumeration, then parallel removal
Write-Log ""Processing capabilities...""
$allCaps = Get-WindowsCapability -Online -ErrorAction SilentlyContinue
$capNamesToRemove = @()

foreach ($capability in $capabilities) {
    $matching = @($allCaps | Where-Object { $_.Name -like ""$capability*"" -and $_.State -eq ""Installed"" })
    if ($matching) {
        $matching | ForEach-Object { Write-Log ""Queuing capability: $($_.Name)"" }
        $capNamesToRemove += $matching.Name
    } else {
        Write-Log ""Capability not found or not installed: $capability""
    }
}

Invoke-RunspacePool -Items $capNamesToRemove -MaxThreads 5 -Label ""capabilities"" `
    -ScriptBlock {
        param($name)
        try {
            Remove-WindowsCapability -Online -Name $name -ErrorAction Stop | Out-Null
            @{ Name = $name; Success = $true; Error = $null }
        } catch {
            @{ Name = $name; Success = $false; Error = $_.Exception.Message }
        }
    } `
    -SuccessFormat ""Removed capability: {0}"" `
    -FailFormat ""Failed to remove capability {0}: {1}""

# Optional features — batch disable
Write-Log ""Processing optional features...""
$enabledFeatures = @()
foreach ($feature in $optionalFeatures) {
    $existing = Get-WindowsOptionalFeature -Online -FeatureName $feature -ErrorAction SilentlyContinue
    if ($existing -and $existing.State -eq ""Enabled"") {
        $enabledFeatures += $feature
    } else {
        Write-Log ""Feature not found or not enabled: $feature""
    }
}

if ($enabledFeatures.Count -gt 0) {
    Write-Log ""Disabling features: $($enabledFeatures -join ', ')""
    Disable-WindowsOptionalFeature -Online -FeatureName $enabledFeatures -NoRestart -ErrorAction SilentlyContinue | Out-Null
}

# Special apps — registry-based uninstall
if ($specialApps.Count -gt 0) {
    Write-Log ""Processing special apps...""

    $uninstallPaths = @(
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
        'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall'
    )

    foreach ($specialApp in $specialApps) {
        Write-Log ""Processing special app: $specialApp""

        $processNames = switch ($specialApp) {
            'OneNote' { @('OneNote', 'ONENOTE', 'ONENOTEM') }
            default { Write-Log ""Unknown special app: $specialApp""; continue }
        }

        foreach ($name in $processNames) {
            Get-Process -Name $name -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
        }

        $uninstalled = $false
        foreach ($basePath in $uninstallPaths) {
            $keys = Get-ChildItem -Path $basePath -ErrorAction SilentlyContinue |
                    Where-Object { $_.PSChildName -like ""$specialApp*"" }

            foreach ($key in $keys) {
                $uninstallString = (Get-ItemProperty -Path $key.PSPath -ErrorAction SilentlyContinue).UninstallString
                if (-not $uninstallString) { continue }

                Write-Log ""Found uninstall string: $uninstallString""
                $silent = if ($uninstallString -like '*OfficeClickToRun.exe*') { 'DisplayLevel=False' } else { '/silent' }

                if ($uninstallString -match '^""([^""]+)""(.*)$') {
                    Start-Process -FilePath $matches[1] -ArgumentList ""$($matches[2].Trim()) $silent"" -NoNewWindow -Wait -ErrorAction SilentlyContinue
                } else {
                    Start-Process -FilePath $uninstallString -ArgumentList $silent -NoNewWindow -Wait -ErrorAction SilentlyContinue
                }

                $uninstalled = $true
                Write-Log ""Completed uninstall for $specialApp""
            }
        }

        if (-not $uninstalled) { Write-Log ""No uninstall strings found for $specialApp"" }
    }
}
" + xboxRegistryFix + @"Write-Log ""Bloat removal process completed""
";
    }

}
