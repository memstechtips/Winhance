using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Native;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Infrastructure.Features.Optimize.Services
{
    public class UpdateService(
        ILogService logService,
        IWindowsRegistryService registryService,
        IServiceProvider serviceProvider,
        ICompatibleSettingsRegistry compatibleSettingsRegistry,
        IScheduledTaskService scheduledTaskService) : IDomainService
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

        private async Task DisableUpdateServicesAsync()
        {
            var services = new[] { "wuauserv", "UsoSvc", "WaaSMedicSvc" };

            foreach (var service in services)
            {
                try
                {
                    await StopServiceAsync(service);
                    SetServiceStartType(service, Advapi32.SERVICE_DISABLED);
                    ClearServiceFailureActions(service);
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
                ("wuauserv", Advapi32.SERVICE_AUTO_START),
                ("UsoSvc", Advapi32.SERVICE_AUTO_START),
                ("WaaSMedicSvc", Advapi32.SERVICE_DEMAND_START)
            };

            foreach (var (service, startType) in services)
            {
                try
                {
                    SetServiceStartType(service, startType);
                    await StartServiceAsync(service);
                    logService.Log(LogLevel.Info, $"Enabled service: {service}");
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Warning, $"Failed to enable {service}: {ex.Message}");
                }
            }
        }

        private Task SetUpdateServicesManualAsync()
        {
            var services = new[] { "wuauserv", "UsoSvc", "WaaSMedicSvc" };

            foreach (var service in services)
            {
                try
                {
                    SetServiceStartType(service, Advapi32.SERVICE_DEMAND_START);
                    logService.Log(LogLevel.Info, $"Set {service} to manual");
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Warning, $"Failed to set {service} to manual: {ex.Message}");
                }
            }

            return Task.CompletedTask;
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
                    await scheduledTaskService.DisableTasksByFolderAsync(folderPath);
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
                    await scheduledTaskService.EnableTasksByFolderAsync(folderPath);
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Warning, $"Failed to process tasks in {folderPath}: {ex.Message}");
                }
            }
        }

        private Task RenameCriticalDllsAsync()
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
                            TakeOwnershipAndGrantFullControl(backupPath);
                            File.Delete(backupPath);
                        }
                        else
                        {
                            continue;
                        }
                    }

                    if (!File.Exists(dllPath) || File.Exists(backupPath))
                        continue;

                    TakeOwnershipAndGrantFullControl(dllPath);

                    File.Move(dllPath, backupPath);
                    logService.Log(LogLevel.Info, $"Renamed {dll} to backup");
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Warning, $"Failed to rename {dll}: {ex.Message}");
                }
            }

            return Task.CompletedTask;
        }

        private Task RestoreCriticalDllsAsync()
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
                            TakeOwnershipAndGrantFullControl(backupPath);
                            File.Delete(backupPath);
                        }
                        else
                        {
                            TakeOwnershipAndGrantFullControl(backupPath);

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

            return Task.CompletedTask;
        }

        private async Task CleanupUpdateFilesAsync()
        {
            try
            {
                var softwareDistPath = @"C:\Windows\SoftwareDistribution";

                if (Directory.Exists(softwareDistPath))
                {
                    await Task.Run(() =>
                    {
                        foreach (var dir in Directory.GetDirectories(softwareDistPath))
                        {
                            try { Directory.Delete(dir, true); }
                            catch { }
                        }
                        foreach (var file in Directory.GetFiles(softwareDistPath))
                        {
                            try { File.Delete(file); }
                            catch { }
                        }
                    });

                    logService.Log(LogLevel.Info, "Cleaned SoftwareDistribution folder");
                }
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

        private static async Task StopServiceAsync(string serviceName)
        {
            try
            {
                using var sc = new ServiceController(serviceName);
                if (sc.Status != ServiceControllerStatus.Stopped)
                {
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                }
            }
            catch (InvalidOperationException)
            {
                // Service doesn't exist or can't be controlled
            }

            await Task.CompletedTask;
        }

        private static async Task StartServiceAsync(string serviceName)
        {
            try
            {
                using var sc = new ServiceController(serviceName);
                if (sc.Status != ServiceControllerStatus.Running)
                {
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                }
            }
            catch (InvalidOperationException)
            {
                // Service doesn't exist or can't be controlled
            }

            await Task.CompletedTask;
        }

        private void SetServiceStartType(string serviceName, uint startType)
        {
            var scmHandle = Advapi32.OpenSCManager(null, null, Advapi32.SC_MANAGER_ALL_ACCESS);
            if (scmHandle == IntPtr.Zero)
            {
                logService.Log(LogLevel.Warning, $"Failed to open SCManager for {serviceName}: {Marshal.GetLastWin32Error()}");
                return;
            }

            try
            {
                var svcHandle = Advapi32.OpenService(scmHandle, serviceName, Advapi32.SERVICE_CHANGE_CONFIG);
                if (svcHandle == IntPtr.Zero)
                {
                    logService.Log(LogLevel.Warning, $"Failed to open service {serviceName}: {Marshal.GetLastWin32Error()}");
                    return;
                }

                try
                {
                    Advapi32.ChangeServiceConfig(svcHandle,
                        Advapi32.SERVICE_NO_CHANGE,
                        startType,
                        Advapi32.SERVICE_NO_CHANGE,
                        null, null, IntPtr.Zero, null, null, null, null);
                }
                finally
                {
                    Advapi32.CloseServiceHandle(svcHandle);
                }
            }
            finally
            {
                Advapi32.CloseServiceHandle(scmHandle);
            }
        }

        private void ClearServiceFailureActions(string serviceName)
        {
            var scmHandle = Advapi32.OpenSCManager(null, null, Advapi32.SC_MANAGER_ALL_ACCESS);
            if (scmHandle == IntPtr.Zero) return;

            try
            {
                var svcHandle = Advapi32.OpenService(scmHandle, serviceName, Advapi32.SERVICE_ALL_ACCESS);
                if (svcHandle == IntPtr.Zero) return;

                try
                {
                    var action = new Advapi32.SC_ACTION { Type = Advapi32.SC_ACTION_NONE, Delay = 0 };
                    var actionPtr = Marshal.AllocHGlobal(Marshal.SizeOf(action));

                    try
                    {
                        Marshal.StructureToPtr(action, actionPtr, false);

                        var failureActions = new Advapi32.SERVICE_FAILURE_ACTIONS
                        {
                            dwResetPeriod = 0,
                            lpRebootMsg = null,
                            lpCommand = null,
                            cActions = 1,
                            lpsaActions = actionPtr
                        };

                        Advapi32.ChangeServiceConfig2(svcHandle, Advapi32.SERVICE_CONFIG_FAILURE_ACTIONS, ref failureActions);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(actionPtr);
                    }
                }
                finally
                {
                    Advapi32.CloseServiceHandle(svcHandle);
                }
            }
            finally
            {
                Advapi32.CloseServiceHandle(scmHandle);
            }
        }

        private void TakeOwnershipAndGrantFullControl(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            var security = fileInfo.GetAccessControl();

            // Take ownership
            security.SetOwner(WindowsIdentity.GetCurrent().User!);
            fileInfo.SetAccessControl(security);

            // Reload after ownership change
            security = fileInfo.GetAccessControl();

            // Grant full control to Everyone (S-1-1-0)
            var everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
            security.AddAccessRule(new FileSystemAccessRule(
                everyone,
                FileSystemRights.FullControl,
                AccessControlType.Allow));
            fileInfo.SetAccessControl(security);
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
