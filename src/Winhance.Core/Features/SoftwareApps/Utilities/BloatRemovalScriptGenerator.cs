using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Helpers;

namespace Winhance.Core.Features.SoftwareApps.Utilities;

public static class BloatRemovalScriptGenerator
{
    public const string ScriptName = "BloatRemoval";
    public const string ScriptVersion = "2.6";
    public const string PackagesArray = "packages";
    public const string CapabilitiesArray = "capabilities";
    public const string OptionalFeaturesArray = "optionalFeatures";
    private static readonly ConcurrentDictionary<string, Regex> ArrayPatternCache = new();

    public static string GenerateScript(
        List<string> packages,
        List<string> capabilities,
        List<string> optionalFeatures,
        List<string> specialApps,
        bool includeXboxRegistryFix = false,
        bool includeTeamsProcessKill = false)
    {
        var sb = new StringBuilder(GetPreamble());
        AppendArray(sb, PackagesArray, packages);
        AppendArray(sb, CapabilitiesArray, capabilities);
        AppendArray(sb, OptionalFeaturesArray, optionalFeatures);
        AppendArray(sb, "specialApps", specialApps);
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
            .Select(line => Unquote(line.Trim().Trim(',')))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();

        return items;
    }

    private static string Unquote(string text) =>
        text.Length >= 2 && text[0] == '\'' && text[^1] == '\''
            ? PowerShellQuoting.Unescape(text[1..^1])
            : text.Trim('\'', '"');

    public static string UpdateScriptTemplate(string existingContent)
    {
        var packages = ExtractArrayFromScript(existingContent, PackagesArray);
        var capabilities = ExtractArrayFromScript(existingContent, CapabilitiesArray);
        var optionalFeatures = ExtractArrayFromScript(existingContent, OptionalFeaturesArray);
        var specialApps = ExtractArrayFromScript(existingContent, "specialApps");

        var includeXboxRegistryFix = GameBarRemoval.Includes(packages);
        var includeTeamsProcessKill = packages.Any(p => p.Equals("MSTeams", StringComparison.OrdinalIgnoreCase));

        return GenerateScript(packages, capabilities, optionalFeatures, specialApps, includeXboxRegistryFix, includeTeamsProcessKill);
    }

    private static void AppendArray(StringBuilder sb, string name, List<string> items)
    {
        sb.AppendLine($"${name} = @(");
        foreach (var item in items)
        {
            sb.AppendLine($"    '{PowerShellQuoting.Escape(item)}'");
        }
        sb.AppendLine(")");
        sb.AppendLine();
    }

    private static string GetPreamble()
    {
        return @"<#
  .SYNOPSIS
      Removes Windows bloatware apps, legacy capabilities, and optional features from Windows 10/11 systems.
      Script Version: " + ScriptVersion + @"

  .DESCRIPTION
      This script removes selected Windows components including:
      - Appx packages (UWP apps like Calculator, Weather, etc.)
      - Legacy Windows capabilities
      - Optional Windows features
      - Special apps requiring custom uninstall procedures (e.g., OneNote)

      Provisioned packages are removed first to ensure Remove-AppxPackage -AllUsers succeeds on Win10.
      This script is designed to run in any context: user sessions, SYSTEM account, or scheduled tasks.
      With -Image it only applies the Game Bar protocol redirect to a mounted offline Windows image; DISM removes the packages there.

  .NOTES
      Source: https://github.com/memstechtips/Winhance

      Requirements:
      - Windows 10/11
      - Administrator privileges (script will auto-elevate)
      - PowerShell 5.1 or higher
#>

param([string]$Image = '',
      [string]$SoftwareRoot = 'HKEY_LOCAL_MACHINE\SOFTWARE')

If (-not $Image -and !([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]""Administrator"")) {
    Try { Start-Process PowerShell.exe -ArgumentList (""-NoProfile -ExecutionPolicy Bypass -File `""{0}`"""" -f $PSCommandPath) -Verb RunAs }
    Catch { Write-Host ""Failed to run as Administrator. Please rerun with elevated privileges."" }
    Exit
}

$Offline = $Image -ne ''
$logFolder = """ + ScriptPaths.LogsDirectoryLiteral + @"""
$logFile = ""$logFolder\BloatRemovalLog.txt""
if (-not $Offline) { New-Item -ItemType Directory -Path $logFolder -Force | Out-Null }

function Write-Log([string]$Message) {
    if ($Offline) { Write-Host $Message; return }
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    if ((Test-Path $logFile) -and (Get-Item $logFile).Length -gt 512000) {
        Remove-Item $logFile -Force -ErrorAction SilentlyContinue
        ""$timestamp - Log rotated - previous log exceeded 500KB"" | Out-File -FilePath $logFile
    }
    ""$timestamp - $Message"" | Out-File -FilePath $logFile -Append
    Write-Host $Message
}

# Runs $Action ($i = the item) per item on a runspace pool, falling back to a sequential loop
function Invoke-RunspacePool([array]$Items, [scriptblock]$Action, [int]$MaxThreads, [string]$Label, [string]$SuccessFormat, [string]$FailFormat) {
    if ($Items.Count -eq 0) { return }
    $ScriptBlock = [scriptblock]::Create('param($i) try { ' + $Action + '; @{ Name = $i; Success = $true; Error = $null } } catch { @{ Name = $i; Success = $false; Error = $_.Exception.Message } }')
    try {
        $threadCount = [Math]::Min($Items.Count, $MaxThreads)
        Write-Log ""Removing $($Items.Count) $Label via runspace pool (threads=$threadCount)...""
        $pool = [RunspaceFactory]::CreateRunspacePool(1, $threadCount)
        $pool.Open()
        $jobs = foreach ($item in $Items) {
            $ps = [PowerShell]::Create().AddScript($ScriptBlock).AddArgument($item)
            $ps.RunspacePool = $pool
            @{ Pipe = $ps; Handle = $ps.BeginInvoke() }
        }
        $results = foreach ($job in $jobs) {
            $job.Pipe.EndInvoke($job.Handle)
            $job.Pipe.Dispose()
        }
        $pool.Close()
        $pool.Dispose()
        $mode = 'Parallel'
    } catch {
        Write-Log ""Parallel execution unavailable, falling back to sequential removal for $Label...""
        $results = foreach ($item in $Items) { & $ScriptBlock $item }
        $mode = 'Sequential'
    }
    foreach ($r in $results) {
        if ($r.Success) { Write-Log ($SuccessFormat -f $r.Name) }
        else { Write-Log ($FailFormat -f $r.Name, $r.Error) }
    }
    Write-Log ""$mode $Label removal completed""
}

";
    }

    private static string GetMainRemovalLogic(bool includeXboxRegistryFix, bool includeTeamsProcessKill)
    {
        var teamsProcessKill = includeTeamsProcessKill ? @"    # Stop Microsoft Teams process before removal to avoid long removal delays
    Get-Process | Where-Object { $_.Name -like '*teams*' } | Stop-Process -Force -ErrorAction SilentlyContinue

" : "";

        var xboxRegistryFix = includeXboxRegistryFix ? @"
$xboxPackages = @('Microsoft.GamingApp', 'Microsoft.XboxGamingOverlay', 'Microsoft.XboxGameOverlay')

if ($packages | Where-Object { $xboxPackages -contains $_ }) {
    Write-Log ""Applying registry settings to prevent post-removal issues...""

    # Redirect ms-gamebar and ms-gamebarservices protocols to a no-op handler
    # to prevent Microsoft Store notification prompts after Game Bar removal
    $classes = if ($Offline) { ""$SoftwareRoot\Classes"" } else { 'HKCR' }
    $systray = if ($Offline) { 'C:\Windows\System32\systray.exe' } else { ""$env:SystemRoot\System32\systray.exe"" }
    try {
        foreach ($protocol in 'ms-gamebar', 'ms-gamebarservices') {
            $key = ""$classes\$protocol""
            $output = reg add $key /f /ve /d ""URL:$protocol"" 2>&1; if ($LASTEXITCODE -ne 0) { Write-Log ""Warning: reg add $protocol default failed: $output"" }
            $output = reg add $key /f /v ""URL Protocol"" /t REG_SZ /d """""""" 2>&1; if ($LASTEXITCODE -ne 0) { Write-Log ""Warning: reg add $protocol URL Protocol failed: $output"" }
            $output = reg add $key /f /v ""NoOpenWith"" /t REG_SZ /d """""""" 2>&1; if ($LASTEXITCODE -ne 0) { Write-Log ""Warning: reg add $protocol NoOpenWith failed: $output"" }
            $output = reg add ""$key\shell\open\command"" /f /ve /d ""`""$systray`"""" 2>&1; if ($LASTEXITCODE -ne 0) { Write-Log ""Warning: reg add $protocol command failed: $output"" }
        }
        Write-Log ""Game Bar protocol redirects applied successfully""
    } catch {
        Write-Log ""Warning: Could not apply Game Bar protocol redirects: $($_.Exception.Message)""
    }
}

" : "";

        return @"Write-Log ""Starting bloat removal process""
if (-not $Offline) {
" + teamsProcessKill + @"    # Discover all packages upfront (single query each)
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

    # Deprovision first: Remove-AppxPackage -AllUsers fails with 0x80070002 on Win10 otherwise
    Invoke-RunspacePool $provisionedToRemove { Remove-AppxProvisionedPackage -Online -PackageName $i -ErrorAction Stop | Out-Null } 10 'provisioned packages' 'Deprovisioned: {0}' 'Failed to deprovision {0}: {1}'
    Invoke-RunspacePool $packagesToRemove { Remove-AppxPackage -Package $i -AllUsers -ErrorAction Stop } 10 'installed packages' 'Removed installed package: {0}' 'Failed to remove installed package {0}: {1}'

    # Capabilities: one DISM enumeration, then parallel removal
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

    Invoke-RunspacePool $capNamesToRemove { Remove-WindowsCapability -Online -Name $i -ErrorAction Stop | Out-Null } 5 'capabilities' 'Removed capability: {0}' 'Failed to remove capability {0}: {1}'

    # Optional features: batch disable
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

    # Special apps: registry-based uninstall
    if ($specialApps.Count -gt 0) {
        Write-Log ""Processing special apps...""

        $uninstallPaths = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall', 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall'

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
                $keys = Get-ChildItem -Path $basePath -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -like ""$specialApp*"" }

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
}
" + xboxRegistryFix + @"Write-Log ""Bloat removal process completed""
";
    }
}
