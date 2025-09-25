using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

public class BloatRemovalService(
    ILogService logService,
    IScheduledTaskService scheduledTaskService,
    IPowerShellExecutionService powerShellService) : IBloatRemovalService
{
    public async Task<bool> RemoveAppsAsync(
        List<ItemDefinition> selectedApps,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            progress?.Report(new TaskProgressDetail { Progress = 10, StatusText = "Creating removal script..." });

            var scriptPath = await CreateOrUpdateBloatRemovalScript(selectedApps, progress);

            if (!string.IsNullOrEmpty(scriptPath))
            {
                progress?.Report(new TaskProgressDetail { Progress = 40, StatusText = "Executing removal script..." });
                var success = await ExecuteRemovalScriptAsync(scriptPath);

                progress?.Report(new TaskProgressDetail { Progress = 70, StatusText = "Registering scheduled task..." });
                await RegisterStartupTaskAsync(scriptPath);

                progress?.Report(new TaskProgressDetail
                {
                    Progress = 100,
                    StatusText = success ? "Apps removed successfully" : "Removal failed",
                    LogLevel = success ? LogLevel.Success : LogLevel.Error
                });

                return success;
            }
            else
            {
                logService.LogInformation("App removal completed using dedicated scripts only");
                progress?.Report(new TaskProgressDetail
                {
                    Progress = 100,
                    StatusText = "Apps removed successfully via dedicated scripts",
                    LogLevel = LogLevel.Success
                });
                return true;
            }
        }
        catch (Exception ex)
        {
            logService.LogError($"Error removing apps: {ex.Message}", ex);
            progress?.Report(new TaskProgressDetail
            {
                Progress = 0,
                StatusText = $"Error: {ex.Message}",
                LogLevel = LogLevel.Error
            });
            return false;
        }
    }

    public async Task<bool> RemoveSpecialAppsAsync(
        List<string> specialAppTypes,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var supportedApps = specialAppTypes.Where(app =>
                app.Equals("OneNote", StringComparison.OrdinalIgnoreCase)).ToList();

            if (!supportedApps.Any())
                return true;

            progress?.Report(new TaskProgressDetail { Progress = 10, StatusText = "Creating special app removal script..." });

            var scriptPath = await CreateSpecialAppRemovalScript(supportedApps);

            progress?.Report(new TaskProgressDetail { Progress = 40, StatusText = "Executing special app removal..." });
            var success = await ExecuteRemovalScriptAsync(scriptPath);

            progress?.Report(new TaskProgressDetail { Progress = 70, StatusText = "Registering scheduled task..." });
            await RegisterStartupTaskAsync(scriptPath);

            progress?.Report(new TaskProgressDetail
            {
                Progress = 100,
                StatusText = success ? "Special apps removed successfully" : "Special app removal failed",
                LogLevel = success ? LogLevel.Success : LogLevel.Error
            });

            return success;
        }
        catch (Exception ex)
        {
            logService.LogError($"Error removing special apps: {ex.Message}", ex);
            return false;
        }
    }

    private async Task<string> CreateSpecialAppRemovalScript(List<string> specialApps)
    {
        Directory.CreateDirectory(ScriptPaths.ScriptsDirectory);
        var scriptPath = Path.Combine(ScriptPaths.ScriptsDirectory, "BloatRemoval.ps1");

        var scriptContent = GenerateScriptContent(
            packages: new List<string>(),
            capabilities: new List<string>(),
            features: new List<string>(),
            specialApps: specialApps);

        await File.WriteAllTextAsync(scriptPath, scriptContent);
        logService.LogInformation($"Special app removal script created at: {scriptPath}");
        return scriptPath;
    }

    public async Task<bool> ExecuteRemovalScriptAsync(string scriptPath)
    {
        try
        {
            logService.LogInformation($"Executing removal script: {scriptPath}");

            // The PowerShell service throws an exception if exit code != 0
            // If no exception is thrown, the script succeeded
            await powerShellService.ExecuteScriptFileAsync(scriptPath);
            return true;  // Success if no exception was thrown
        }
        catch (Exception ex)
        {
            logService.LogError($"Error executing script: {ex.Message}", ex);
            return false;
        }
    }

    public async Task<bool> RegisterStartupTaskAsync(string scriptPath)
    {
        try
        {
            var script = new RemovalScript
            {
                Name = "BloatRemoval",
                Content = await File.ReadAllTextAsync(scriptPath),
                TargetScheduledTaskName = "WinhanceBloatRemoval",
                RunOnStartup = true,
                ActualScriptPath = scriptPath
            };

            return await scheduledTaskService.RegisterScheduledTaskAsync(script);
        }
        catch (Exception ex)
        {
            logService.LogError($"Error registering scheduled task: {ex.Message}", ex);
            return false;
        }
    }

    private async Task<string> CreateOrUpdateBloatRemovalScript(
        List<ItemDefinition> apps,
        IProgress<TaskProgressDetail>? progress = null)
    {
        Directory.CreateDirectory(ScriptPaths.ScriptsDirectory);
        var scriptPath = Path.Combine(ScriptPaths.ScriptsDirectory, "BloatRemoval.ps1");

        var packages = new List<string>();
        var capabilities = new List<string>();
        var optionalFeatures = new List<string>();
        var specialApps = new List<string>();

        // Handle apps with dedicated scripts first
        var appsWithScripts = apps.Where(a => a.RemovalScript != null).ToList();
        for (int i = 0; i < appsWithScripts.Count; i++)
        {
            await CreateDedicatedRemovalScript(appsWithScripts[i], i, appsWithScripts.Count, progress);
        }

        // Handle regular apps (including OneNote)
        foreach (var app in apps.Where(a => a.RemovalScript == null))
        {
            var name = GetAppName(app);
            if (string.IsNullOrEmpty(name)) continue;

            if (!string.IsNullOrEmpty(app.CapabilityName))
                capabilities.Add(name);
            else if (!string.IsNullOrEmpty(app.OptionalFeatureName))
                optionalFeatures.Add(name);
            else
            {
                packages.Add(name);

                if (IsOneNote(app))
                    specialApps.Add("OneNote");
            }
        }

        bool hasRegularApps = packages.Any() || capabilities.Any() || optionalFeatures.Any() || specialApps.Any();

        if (!hasRegularApps)
        {
            logService.LogInformation("No regular apps to process. Skipping BloatRemoval.ps1 creation.");
            return string.Empty;
        }

        string scriptContent;
        if (File.Exists(scriptPath))
        {
            scriptContent = await MergeWithExistingScript(scriptPath, packages, capabilities, optionalFeatures, specialApps);
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
            scriptContent = GenerateScriptContent(packages, capabilities, optionalFeatures, specialApps);
        }

        await File.WriteAllTextAsync(scriptPath, scriptContent);
        logService.LogInformation($"Script updated at: {scriptPath}");
        return scriptPath;
    }

    private string CreateScriptName(string appId)
    {
        return appId switch
        {
            "windows-app-edge" => "EdgeRemoval.ps1",
            "windows-app-onedrive" => "OneDriveRemoval.ps1",
            _ => throw new NotSupportedException($"No dedicated script defined for {appId}")
        };
    }

    private async Task CreateDedicatedRemovalScript(
        ItemDefinition app,
        int currentIndex,
        int totalCount,
        IProgress<TaskProgressDetail>? progress = null)
    {
        var scriptName = CreateScriptName(app.Id);
        var scriptPath = Path.Combine(ScriptPaths.ScriptsDirectory, scriptName);
        var scriptContent = app.RemovalScript!();

        int baseProgress = 10 + (currentIndex * 80 / totalCount);
        int scriptProgressRange = 80 / totalCount;

        Directory.CreateDirectory(Path.GetDirectoryName(scriptPath)!);
        await File.WriteAllTextAsync(scriptPath, scriptContent);
        logService.LogInformation($"Dedicated removal script created at: {scriptPath}");

        progress?.Report(new TaskProgressDetail
        {
            Progress = baseProgress + (scriptProgressRange * 10 / 100),
            StatusText = $"Executing {app.Name} removal script..."
        });

        var executionSuccess = await ExecuteRemovalScriptAsync(scriptPath);
        logService.LogInformation($"Script execution result: {executionSuccess}");

        progress?.Report(new TaskProgressDetail
        {
            Progress = baseProgress + (scriptProgressRange * 80 / 100),
            StatusText = $"Registering {app.Name} scheduled task..."
        });

        var script = new RemovalScript
        {
            Name = scriptName.Replace(".ps1", ""),
            Content = scriptContent,
            TargetScheduledTaskName = scriptName.Replace(".ps1", ""),
            RunOnStartup = true,
            ActualScriptPath = scriptPath
        };

        await scheduledTaskService.RegisterScheduledTaskAsync(script);
    }

    private async Task<string> MergeWithExistingScript(string scriptPath, List<string> packages, List<string> capabilities, List<string> optionalFeatures, List<string> specialApps)
    {
        var existingContent = await File.ReadAllTextAsync(scriptPath);

        var existingPackages = ExtractArrayFromScript(existingContent, "packages");
        var existingCapabilities = ExtractArrayFromScript(existingContent, "capabilities");
        var existingFeatures = ExtractArrayFromScript(existingContent, "optionalFeatures");
        var existingSpecialApps = ExtractArrayFromScript(existingContent, "specialApps");

        var mergedPackages = existingPackages.Union(packages).Distinct().ToList();
        var mergedCapabilities = existingCapabilities.Union(capabilities).Distinct().ToList();
        var mergedFeatures = existingFeatures.Union(optionalFeatures).Distinct().ToList();
        var mergedSpecialApps = existingSpecialApps.Union(specialApps).Distinct().ToList();

        return GenerateScriptContent(mergedPackages, mergedCapabilities, mergedFeatures, mergedSpecialApps);
    }

    public async Task<bool> RemoveItemsFromScriptAsync(List<ItemDefinition> itemsToRemove)
    {
        try
        {
            var scriptPath = Path.Combine(ScriptPaths.ScriptsDirectory, "BloatRemoval.ps1");

            if (!File.Exists(scriptPath))
            {
                logService.LogInformation("BloatRemoval.ps1 does not exist, nothing to clean up.");
                return true;
            }

            var existingContent = await File.ReadAllTextAsync(scriptPath);
            var itemsToRemoveNames = GetItemNames(itemsToRemove);

            var updatedContent = RemoveItemsFromScriptContent(existingContent, itemsToRemoveNames);

            if (updatedContent != existingContent)
            {
                await File.WriteAllTextAsync(scriptPath, updatedContent);
                logService.LogInformation($"Removed {itemsToRemoveNames.Count} items from BloatRemoval.ps1");

                await RegisterStartupTaskAsync(scriptPath);

                return true;
            }

            return true;
        }
        catch (Exception ex)
        {
            logService.LogError($"Error removing items from script: {ex.Message}", ex);
            return false;
        }
    }

    private List<string> GetItemNames(List<ItemDefinition> items)
    {
        var names = new List<string>();
        foreach (var item in items)
        {
            var name = GetAppName(item);
            if (!string.IsNullOrEmpty(name))
                names.Add(name);
        }
        return names;
    }

    private string RemoveItemsFromScriptContent(string content, List<string> itemsToRemove)
    {
        var existingPackages = ExtractArrayFromScript(content, "packages");
        var existingCapabilities = ExtractArrayFromScript(content, "capabilities");
        var existingFeatures = ExtractArrayFromScript(content, "optionalFeatures");
        var existingSpecialApps = ExtractArrayFromScript(content, "specialApps");

        var cleanedPackages = existingPackages.Except(itemsToRemove, StringComparer.OrdinalIgnoreCase).ToList();
        var cleanedCapabilities = existingCapabilities.Except(itemsToRemove, StringComparer.OrdinalIgnoreCase).ToList();
        var cleanedFeatures = existingFeatures.Except(itemsToRemove, StringComparer.OrdinalIgnoreCase).ToList();
        var cleanedSpecialApps = existingSpecialApps.Where(specialApp =>
        {
            if (itemsToRemove.Any(item => specialApp.Equals(item, StringComparison.OrdinalIgnoreCase)))
                return false;

            return !itemsToRemove.Any(item => IsOneNotePackage(item, specialApp));
        }).ToList();

        return GenerateScriptContent(cleanedPackages, cleanedCapabilities, cleanedFeatures, cleanedSpecialApps);
    }

    private List<string> ExtractArrayFromScript(string content, string arrayName)
    {
        var pattern = $@"\${arrayName}\s*=\s*@\(\s*(.*?)\s*\)";
        var match = Regex.Match(content, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

        if (!match.Success) return new List<string>();

        var arrayContent = match.Groups[1].Value;
        var items = arrayContent
            .Split('\n')
            .Select(line => line.Trim().Trim(',').Trim('\'', '"'))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();

        return items;
    }

    private string GetAppName(ItemDefinition app)
    {
        if (!string.IsNullOrEmpty(app.CapabilityName))
            return app.CapabilityName;

        if (!string.IsNullOrEmpty(app.OptionalFeatureName))
            return app.OptionalFeatureName;

        return app.AppxPackageName!;
    }

    private string GenerateScriptContent(List<string> packages, List<string> capabilities, List<string> features, List<string>? specialApps = null)
    {
        string currentUser = Environment.UserName;

        var sb = new StringBuilder();
        sb.AppendLine("# BloatRemoval.ps1");
        sb.AppendLine("# This script removes Windows bloatware apps, legacy capabilites and optional features and prevents them from reinstalling");
        sb.AppendLine("# Source: Winhance (https://github.com/memstechtips/Winhance)");
        sb.AppendLine();
        sb.AppendLine($"# Generated by Winhance on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
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
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("Write-Log \"Starting bloat removal process\"");
        sb.AppendLine();

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
        foreach (var feature in features)
        {
            sb.AppendLine($"    '{feature}'");
        }
        sb.AppendLine(")");
        sb.AppendLine();

        sb.AppendLine("# Special apps requiring uninstall string execution");
        sb.AppendLine("$specialApps = @(");
        if (specialApps?.Any() == true)
        {
            foreach (var app in specialApps)
            {
                sb.AppendLine($"    '{app}'");
            }
        }
        sb.AppendLine(")");
        sb.AppendLine();

        sb.Append(GetStaticScriptBody());
        sb.Append(GetSpecialAppsScriptBody());

        return sb.ToString();
    }

    private string GetStaticScriptBody()
    {
        return @"# Process Packages
Write-Log ""Discovering all packages...""
$allInstalledPackages = Get-AppxPackage -AllUsers -ErrorAction SilentlyContinue
$allProvisionedPackages = Get-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue

Write-Log ""Processing packages...""
$packagesToRemove = @()
$provisionedPackagesToRemove = @()
$notFoundPackages = @()

foreach ($package in $packages) {
    $foundAny = $false

    # Filter from cached data
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

# Log packages not found in batch
if ($notFoundPackages.Count -gt 0) {
    Write-Log ""Packages not found: $($notFoundPackages -join ', ')""
}

# Remove installed packages
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

# Remove provisioned packages
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

# Process Capabilities
Write-Log ""Processing capabilities...""
foreach ($capability in $capabilities) {
    Write-Log ""Checking capability: $capability""
    try {
        # Get capabilities matching the pattern
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
        Write-Log ""Error Type: $($_.Exception.GetType().Name)""
        if ($_.Exception.HResult) {
            Write-Log ""Error Code: 0x$($_.Exception.HResult.ToString('X8'))""
        }
    }
}

# Process Optional Features
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

";
    }

    private string GetSpecialAppsScriptBody()
    {
        return @"
  # Process Special Apps (Only OneNote - OneDrive has dedicated script)
  Write-Log ""Processing special apps...""

  # Base registry paths for uninstall information
  $uninstallBasePaths = @(
      'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
      'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall'
  )

  foreach ($specialApp in $specialApps) {
      Write-Log ""Processing special app: $specialApp""

      # Define app-specific configuration (OneNote only)
      switch ($specialApp) {
          'OneNote' {
              $processesToStop = @('OneNote', 'ONENOTE', 'ONENOTEM')
              $searchPattern = 'OneNote*'
          }
          default {
              Write-Log ""Unknown or unsupported special app: $specialApp""
              continue
          }
      }

      # Stop processes
      foreach ($processName in $processesToStop) {
          $processes = Get-Process -Name $processName -ErrorAction SilentlyContinue
          if ($processes) {
              $processes | Stop-Process -Force -ErrorAction SilentlyContinue
              Write-Log ""Stopped process: $processName""
          }
      }

      # Find and execute uninstall strings with appropriate silent parameters
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
                          Write-Log ""Found uninstall string for $specialApp in $($key.PSChildName): $uninstallString""

                          if ($uninstallString -match '^\""([^\""]+)\""(.*)$') {
                              $exePath = $matches[1]
                              $args = $matches[2].Trim()

                              # Handle OfficeClickToRun.exe specially for OneNote
                              if ($exePath -like '*OfficeClickToRun.exe') {
                                  $args += ' DisplayLevel=False'
                                  Write-Log ""Using OfficeClickToRun silent parameter: DisplayLevel=False""
                              } else {
                                  $args += ' /silent'
                                  Write-Log ""Using standard silent parameter: /silent""
                              }

                              Write-Log ""Executing: $exePath with args: $args""
                              Start-Process -FilePath $exePath -ArgumentList $args -NoNewWindow -Wait -ErrorAction SilentlyContinue
                          } else {
                              # Handle non-quoted uninstall strings
                              if ($uninstallString -like '*OfficeClickToRun.exe*') {
                                  Write-Log ""Executing: $uninstallString DisplayLevel=False""
                                  Start-Process -FilePath $uninstallString -ArgumentList 'DisplayLevel=False' -NoNewWindow -Wait -ErrorAction SilentlyContinue
                              } else {
                                  Write-Log ""Executing: $uninstallString /silent""
                                  Start-Process -FilePath $uninstallString -ArgumentList '/silent' -NoNewWindow -Wait -ErrorAction SilentlyContinue
                              }
                          }
                          Write-Log ""Completed uninstall execution for $specialApp""
                          $uninstallExecuted = $true
                      }
                  }
                  catch {
                      Write-Log ""Error processing uninstall key $($key.PSChildName): $($_.Exception.Message)""
                  }
              }
          }
          catch {
              Write-Log ""Error searching for $specialApp uninstall keys in $uninstallBasePath : $($_.Exception.Message)""
          }
      }

      if (-not $uninstallExecuted) {
          Write-Log ""No uninstall strings found for $specialApp""
      }
  }

  Write-Log ""Special apps processing completed""

  Write-Log ""Bloat removal process completed""
  ";
    }

    private bool IsOneNote(ItemDefinition app)
    {
        return app.AppxPackageName?.Contains("OneNote", StringComparison.OrdinalIgnoreCase) == true;
    }

    private bool IsOneNotePackage(string packageName, string specialAppType)
    {
        return specialAppType.Equals("OneNote", StringComparison.OrdinalIgnoreCase) &&
               packageName.Contains("OneNote", StringComparison.OrdinalIgnoreCase);
    }
}