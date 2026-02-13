using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Utilities;

namespace Winhance.Infrastructure.Features.Optimize.Services
{
    public class UpdateService(
        ILogService logService,
        IWindowsRegistryService registryService,
        IServiceProvider serviceProvider,
        ICompatibleSettingsRegistry compatibleSettingsRegistry) : IDomainService
    {
        public string DomainName => FeatureIds.Update;

        public async Task<bool> TryApplySpecialSettingAsync(SettingDefinition setting, object value, bool additionalContext = false)
        {
            if (setting.Id == "updates-policy-mode" && value is int index)
            {
                await ApplyUpdatesPolicyModeAsync(setting, index);
                return true;
            }
            return false;
        }

        public async Task<Dictionary<string, Dictionary<string, object?>>> DiscoverSpecialSettingsAsync(IEnumerable<SettingDefinition> settings)
        {
            var results = new Dictionary<string, Dictionary<string, object?>>();

            var updatesSetting = settings.FirstOrDefault(s => s.Id == "updates-policy-mode");
            if (updatesSetting != null)
            {
                var currentIndex = await GetCurrentUpdatePolicyIndexAsync();
                results["updates-policy-mode"] = new Dictionary<string, object?> { ["CurrentPolicyIndex"] = currentIndex };
            }

            return results;
        }

        public async Task<IEnumerable<SettingDefinition>> GetSettingsAsync()
        {
            try
            {
                return compatibleSettingsRegistry.GetFilteredSettings(FeatureIds.Update);
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"Error loading Update settings: {ex.Message}");
                return Enumerable.Empty<SettingDefinition>();
            }
        }

        public async Task ApplyUpdatesPolicyModeAsync(SettingDefinition setting, object value)
        {
            if (value is not int selectionIndex)
                throw new ArgumentException("Expected integer selection index");

            logService.Log(LogLevel.Info, $"[UpdateService] Applying updates-policy-mode with index: {selectionIndex}");

            switch (selectionIndex)
            {
                case 0:
                    await ApplyNormalModeAsync(setting);
                    break;
                case 1:
                    await ApplySecurityOnlyModeAsync(setting);
                    break;
                case 2:
                    await ApplyPausedModeAsync(setting);
                    break;
                case 3:
                    await ApplyDisabledModeAsync(setting);
                    break;
                default:
                    throw new ArgumentException($"Invalid selection index: {selectionIndex}");
            }

            logService.Log(LogLevel.Info, $"[UpdateService] Successfully applied updates-policy-mode index {selectionIndex}");
        }

        private async Task ApplyNormalModeAsync(SettingDefinition setting)
        {
            await RestoreCriticalDllsAsync();
            await EnableUpdateServicesAsync();
            await EnableUpdateTasksAsync();
            ApplyRegistrySettingsForIndex(setting, 0);
        }

        private async Task ApplySecurityOnlyModeAsync(SettingDefinition setting)
        {
            await RestoreCriticalDllsAsync();
            await EnableUpdateServicesAsync();
            ApplyRegistrySettingsForIndex(setting, 1);
        }

        // Based on work by Aetherinox: https://github.com/Aetherinox/pause-windows-updates/blob/main/windows-updates-pause.reg
        private async Task ApplyPausedModeAsync(SettingDefinition setting)
        {
            var recommendedService = serviceProvider.GetService<IRecommendedSettingsService>();
            if (recommendedService != null)
            {
                logService.Log(LogLevel.Info, "[UpdateService] Applying recommended settings before pausing updates");
                try
                {
                    await recommendedService.ApplyRecommendedSettingsAsync(setting.Id);
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Warning, $"[UpdateService] Failed to apply some recommended settings: {ex.Message}");
                }
            }

            await RestoreCriticalDllsAsync();
            await SetUpdateServicesManualAsync();
            ApplyRegistrySettingsForIndex(setting, 2);
        }

        // Based on work by Chris Titus: https://github.com/ChrisTitusTech/winutil/blob/main/functions/public/Invoke-WPFUpdatesdisable.ps1
        private async Task ApplyDisabledModeAsync(SettingDefinition setting)
        {
            var recommendedService = serviceProvider.GetService<IRecommendedSettingsService>();
            if (recommendedService != null)
            {
                logService.Log(LogLevel.Info, "[UpdateService] Applying recommended settings before disabling updates");
                try
                {
                    await recommendedService.ApplyRecommendedSettingsAsync(setting.Id);
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Warning, $"[UpdateService] Failed to apply some recommended settings: {ex.Message}");
                }
            }

            await DisableUpdateServicesAsync();
            await DisableUpdateTasksAsync();
            await RenameCriticalDllsAsync();
            await CleanupUpdateFilesAsync();
            ApplyRegistrySettingsForIndex(setting, 3);
        }

        private async Task<(bool Success, string Output, string Error)> RunCommandAsync(string command)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c chcp 65001 && {command}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = Process.Start(psi)!;
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                return (process.ExitCode == 0, output, error);
            }
            catch (Exception ex)
            {
                return (false, string.Empty, ex.Message);
            }
        }

        private async Task DisableUpdateServicesAsync()
        {
            var services = new[] { "wuauserv", "UsoSvc", "WaaSMedicSvc" };

            foreach (var service in services)
            {
                try
                {
                    await RunCommandAsync($"net stop {service}");
                    await RunCommandAsync($"sc config {service} start= disabled");
                    await RunCommandAsync($"sc failure {service} reset= 0 actions= \"\"");
                    logService.Log(LogLevel.Info, $"Disabled service: {service}");
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Warning, $"Failed to disable {service}: {ex.Message}");
                }
            }
        }

        private async Task EnableUpdateServicesAsync()
        {
            var services = new[]
            {
                ("wuauserv", "auto"),
                ("UsoSvc", "auto"),
                ("WaaSMedicSvc", "demand")
            };

            foreach (var (service, startType) in services)
            {
                try
                {
                    await RunCommandAsync($"sc config {service} start= {startType}");
                    await RunCommandAsync($"net start {service}");
                    logService.Log(LogLevel.Info, $"Enabled service: {service}");
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Warning, $"Failed to enable {service}: {ex.Message}");
                }
            }
        }

        private async Task SetUpdateServicesManualAsync()
        {
            var services = new[] { "wuauserv", "UsoSvc", "WaaSMedicSvc" };

            foreach (var service in services)
            {
                try
                {
                    await RunCommandAsync($"sc config {service} start= demand");
                    logService.Log(LogLevel.Info, $"Set {service} to manual");
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Warning, $"Failed to set {service} to manual: {ex.Message}");
                }
            }
        }

        private async Task DisableUpdateTasksAsync()
        {
            var folderPaths = new[]
            {
                @"\Microsoft\Windows\InstallService\",
                @"\Microsoft\Windows\UpdateOrchestrator\",
                @"\Microsoft\Windows\UpdateAssistant\",
                @"\Microsoft\Windows\WaaSMedic\",
                @"\Microsoft\Windows\WindowsUpdate\",
            };

            foreach (var folderPath in folderPaths)
            {
                try
                {
                    var script = $"Get-ScheduledTask -TaskPath '{folderPath}' -ErrorAction SilentlyContinue | Disable-ScheduledTask -ErrorAction SilentlyContinue";
                    await PowerShellRunner.RunScriptAsync(script);
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Warning, $"Failed to process tasks in {folderPath}: {ex.Message}");
                }
            }
        }

        private async Task EnableUpdateTasksAsync()
        {
            var folderPaths = new[]
            {
                @"\Microsoft\Windows\UpdateOrchestrator\",
                @"\Microsoft\Windows\WindowsUpdate\",
            };

            foreach (var folderPath in folderPaths)
            {
                try
                {
                    var script = $"Get-ScheduledTask -TaskPath '{folderPath}' -ErrorAction SilentlyContinue | Enable-ScheduledTask -ErrorAction SilentlyContinue";
                    await PowerShellRunner.RunScriptAsync(script);
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Warning, $"Failed to process tasks in {folderPath}: {ex.Message}");
                }
            }
        }

        private async Task RenameCriticalDllsAsync()
        {
            var dlls = new[] { "WaaSMedicSvc.dll", "wuaueng.dll" };

            foreach (var dll in dlls)
            {
                try
                {
                    var dllPath = $@"C:\Windows\System32\{dll}";
                    var backupPath = $@"C:\Windows\System32\{Path.GetFileNameWithoutExtension(dll)}_BAK.dll";

                    if (File.Exists(backupPath))
                    {
                        if (File.Exists(dllPath))
                        {
                            logService.Log(LogLevel.Info, $"Conflict detected for {dll}. Deleting stale backup.");
                            await RunCommandAsync($"takeown /f \"{backupPath}\"");
                            await RunCommandAsync($"icacls \"{backupPath}\" /grant *S-1-1-0:F");
                            File.Delete(backupPath);
                        }
                        else
                        {
                            continue;
                        }
                    }

                    if (!File.Exists(dllPath) || File.Exists(backupPath))
                        continue;

                    await RunCommandAsync($"takeown /f \"{dllPath}\"");
                    await RunCommandAsync($"icacls \"{dllPath}\" /grant *S-1-1-0:F");

                    File.Move(dllPath, backupPath);
                    logService.Log(LogLevel.Info, $"Renamed {dll} to backup");
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Warning, $"Failed to rename {dll}: {ex.Message}");
                }
            }
        }

        private async Task RestoreCriticalDllsAsync()
        {
            var dlls = new[] { "WaaSMedicSvc.dll", "wuaueng.dll" };

            foreach (var dll in dlls)
            {
                try
                {
                    var dllPath = $@"C:\Windows\System32\{dll}";
                    var backupPath = $@"C:\Windows\System32\{Path.GetFileNameWithoutExtension(dll)}_BAK.dll";

                    if (File.Exists(backupPath))
                    {
                        if (File.Exists(dllPath))
                        {
                            logService.Log(LogLevel.Info, $"System already restored {dll}. Removing backup.");
                            await RunCommandAsync($"takeown /f \"{backupPath}\"");
                            await RunCommandAsync($"icacls \"{backupPath}\" /grant *S-1-1-0:F");
                            File.Delete(backupPath);
                        }
                        else
                        {
                            await RunCommandAsync($"takeown /f \"{backupPath}\"");
                            await RunCommandAsync($"icacls \"{backupPath}\" /grant *S-1-1-0:F");

                            File.Move(backupPath, dllPath);
                            logService.Log(LogLevel.Info, $"Restored {dll} from backup");
                        }
                    }
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Warning, $"Failed to restore {dll}: {ex.Message}");
                }
            }
        }

        private async Task CleanupUpdateFilesAsync()
        {
            try
            {
                var script = "Remove-Item 'C:\\Windows\\SoftwareDistribution\\*' -Recurse -Force -ErrorAction SilentlyContinue";
                await PowerShellRunner.RunScriptAsync(script);
                logService.Log(LogLevel.Info, "Cleaned SoftwareDistribution folder");
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"Failed to cleanup update files: {ex.Message}");
            }
        }

        private void ApplyRegistrySettingsForIndex(SettingDefinition setting, int index)
        {
            if (!setting.CustomProperties.TryGetValue(CustomPropertyKeys.ValueMappings, out var mappingsObj))
                return;

            var mappings = (Dictionary<int, Dictionary<string, object?>>)mappingsObj;
            if (!mappings.TryGetValue(index, out var valueMapping))
                return;

            foreach (var registrySetting in setting.RegistrySettings)
            {
                try
                {
                    if (valueMapping.TryGetValue(registrySetting.ValueName, out var value))
                    {
                        if (value == null)
                        {
                            registryService.ApplySetting(registrySetting, false);
                        }
                        else
                        {
                            registryService.ApplySetting(registrySetting, true, value);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Warning, $"Failed to apply registry setting {registrySetting.ValueName}: {ex.Message}");
                }
            }
        }

        public async Task<int> GetCurrentUpdatePolicyIndexAsync()
        {
            if (AreCriticalDllsRenamed())
                return 3;

            if (IsUpdatesPaused())
                return 2;

            var deferFeature = registryService.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                "DeferFeatureUpdates");

            if (deferFeature is int defer && defer == 1)
                return 1;

            return 0;
        }

        private bool AreCriticalDllsRenamed()
        {
            var dlls = new[] { "WaaSMedicSvc.dll", "wuaueng.dll" };

            foreach (var dll in dlls)
            {
                var dllPath = $@"C:\Windows\System32\{dll}";
                var backupPath = $@"C:\Windows\System32\{Path.GetFileNameWithoutExtension(dll)}_BAK.dll";

                if (File.Exists(backupPath) && !File.Exists(dllPath))
                    return true;
            }

            return false;
        }

        private bool IsUpdatesPaused()
        {
            var pauseStart = registryService.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                "PauseUpdatesStartTime");

            var pauseExpiry = registryService.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                "PauseUpdatesExpiryTime");

            if (pauseStart != null || pauseExpiry != null)
                return true;

            var pausedQuality = registryService.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                "PausedQualityDate");

            var pausedFeature = registryService.GetValue(
                @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
                "PausedFeatureDate");

            if (pausedQuality != null || pausedFeature != null)
                return true;

            return false;
        }
    }
}
