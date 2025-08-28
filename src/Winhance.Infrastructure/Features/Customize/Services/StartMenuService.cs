using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.Customize.Models;
using Winhance.Infrastructure.Features.Common.Services;

namespace Winhance.Infrastructure.Features.Customize.Services
{
    /// <summary>
    /// Service implementation for managing Start Menu customization settings.
    /// Handles Start Menu layout, search, and behavior customizations.
    /// Maintains exact same method signatures and behavior for compatibility.
    /// </summary>
    public class StartMenuService : IDomainService
    {
        private readonly IScheduledTaskService _scheduledTaskService;
        private readonly SettingControlHandler _controlHandler;
        private readonly ISystemSettingsDiscoveryService _discoveryService;
        private readonly ILogService _logService;
        private readonly ISystemServices _systemServices;

        public string DomainName => FeatureIds.StartMenu;

        public StartMenuService(
            IScheduledTaskService scheduledTaskService,
            SettingControlHandler controlHandler,
            ISystemSettingsDiscoveryService discoveryService,
            ILogService logService,
            ISystemServices systemServices
        )
        {
            _scheduledTaskService =
                scheduledTaskService
                ?? throw new ArgumentNullException(nameof(scheduledTaskService));
            _controlHandler = controlHandler ?? throw new ArgumentNullException(nameof(controlHandler));
            _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _systemServices =
                systemServices ?? throw new ArgumentNullException(nameof(systemServices));
        }

        public async Task<IEnumerable<SettingDefinition>> GetSettingsAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Loading Start Menu settings");

                var group = StartMenuCustomizations.GetStartMenuCustomizations();
                return await _discoveryService.GetSettingsWithSystemStateAsync(
                    group.Settings,
                    DomainName
                );
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error loading Start Menu settings: {ex.Message}");
                return Enumerable.Empty<SettingDefinition>();
            }
        }

        /// <summary>
        /// Applies a setting.
        /// </summary>
        public async Task ApplySettingAsync(string settingId, bool enable, object? value = null)
        {
            var settings = await GetRawSettingsAsync();
            var setting = settings.FirstOrDefault(s => s.Id == settingId);
            if (setting == null)
                throw new ArgumentException($"Setting '{settingId}' not found");

            switch (setting.InputType)
            {
                case SettingInputType.Toggle:
                    await _controlHandler.ApplyBinaryToggleAsync(setting, enable);
                    break;
                case SettingInputType.Selection when value is int index:
                    await _controlHandler.ApplyComboBoxIndexAsync(setting, index);
                    break;
                case SettingInputType.NumericRange when value != null:
                    await _controlHandler.ApplyNumericUpDownAsync(setting, value);
                    break;
                default:
                    throw new NotSupportedException($"Input type '{setting.InputType}' not supported");
            }
        }

        /// <summary>
        /// Checks if a setting is enabled.
        /// </summary>
        public async Task<bool> IsSettingEnabledAsync(string settingId)
        {
            var settings = await GetRawSettingsAsync();
            return await _controlHandler.GetSettingStatusAsync(settingId, settings);
        }

        /// <summary>
        /// Gets the current value of a setting.
        /// </summary>
        public async Task<object?> GetSettingValueAsync(string settingId)
        {
            var settings = await GetRawSettingsAsync();
            return await _controlHandler.GetSettingValueAsync(settingId, settings);
        }

        /// <summary>
        /// Gets raw setting configurations without expensive system state discovery.
        /// This method returns only the setting metadata (registry paths, values, dependencies)
        /// without resolving current system state, ComboBox options, or current registry values.
        /// Use this for performance-critical operations where only configuration data is needed.
        /// </summary>
        public async Task<IEnumerable<SettingDefinition>> GetRawSettingsAsync()
        {
            var group = StartMenuCustomizations.GetStartMenuCustomizations();
            return await Task.FromResult(group.Settings);
        }

        public async Task ApplyMultipleSettingsAsync(
            IEnumerable<SettingDefinition> settings,
            bool isEnabled
        )
        {
            try
            {
                _logService.Log(
                    LogLevel.Info,
                    $"Applying multiple Start Menu settings: enabled={isEnabled}"
                );

                foreach (var setting in settings)
                {
                    await ApplySettingAsync(setting.Id, isEnabled);
                }

                _logService.Log(LogLevel.Info, "Successfully applied multiple Start Menu settings");
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error applying multiple Start Menu settings: {ex.Message}"
                );
                throw;
            }
        }

        public async Task CleanWindows10StartMenuAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Starting Windows 10 Start Menu cleaning process");

                await Task.Run(() =>
                    CleanWindows10StartMenu(_systemServices, _scheduledTaskService, _logService)
                );

                _logService.Log(LogLevel.Info, "Windows 10 Start Menu cleaned successfully");
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error cleaning Windows 10 Start Menu: {ex.Message}"
                );
                throw;
            }
        }

        public async Task CleanWindows11StartMenuAsync()
        {
            try
            {
                _logService.Log(LogLevel.Info, "Starting Windows 11 Start Menu cleaning process");

                await Task.Run(() => CleanWindows11StartMenu(_logService));

                _logService.Log(LogLevel.Info, "Windows 11 Start Menu cleaned successfully");
            }
            catch (Exception ex)
            {
                _logService.Log(
                    LogLevel.Error,
                    $"Error cleaning Windows 11 Start Menu: {ex.Message}"
                );
                throw;
            }
        }

        /// <summary>
        /// Cleans the Windows 11 Start Menu by setting registry policy and removing start menu files.
        /// Always applies to all users on the system.
        /// </summary>
        /// <param name="logService">The logging service.</param>
        private void CleanWindows11StartMenu(ILogService logService = null)
        {
            try
            {
                // Step 1: Add registry entry to configure empty pinned list
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = "reg.exe",
                        Arguments =
                            "add \"HKLM\\SOFTWARE\\Microsoft\\PolicyManager\\current\\device\\Start\" /v \"ConfigureStartPins\" /t REG_SZ /d \"{\\\"pinnedList\\\":[]}\" /f",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        Verb = "runas", // Run as administrator
                    };

                    process.Start();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        string error = process.StandardError.ReadToEnd();
                        throw new Exception(
                            $"Failed to add registry entry. Exit code: {process.ExitCode}. Error: {error}"
                        );
                    }
                }

                // Step 2: Delete start.bin and start2.bin files from LocalState directory
                string localAppData = Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData
                );
                string startMenuLocalStatePath = Path.Combine(
                    localAppData,
                    "Packages",
                    "Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy",
                    "LocalState"
                );

                if (Directory.Exists(startMenuLocalStatePath))
                {
                    // Delete start.bin if it exists
                    string startBinPath = Path.Combine(startMenuLocalStatePath, "start.bin");
                    if (File.Exists(startBinPath))
                    {
                        File.Delete(startBinPath);
                    }

                    // Delete start2.bin if it exists
                    string start2BinPath = Path.Combine(startMenuLocalStatePath, "start2.bin");
                    if (File.Exists(start2BinPath))
                    {
                        File.Delete(start2BinPath);
                    }
                }

                // Step 3: Always clean other users' Start Menu files
                CleanOtherUsersStartMenuFiles(logService);

                // Step 4: End the StartMenuExperienceHost process (it will automatically restart)
                TerminateStartMenuExperienceHost();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error cleaning Windows 11 Start Menu: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Cleans the Windows 10 Start Menu by creating a LayoutModification.xml file in the Default user profile.
        /// Always applies to all users on the system by creating scheduled tasks for existing users.
        /// </summary>
        /// <param name="windowsService">The system services.</param>
        /// <param name="scheduledTaskService">The scheduled task service for creating user-specific tasks.</param>
        /// <param name="logService">The logging service.</param>
        private void CleanWindows10StartMenu(
            ISystemServices windowsService,
            IScheduledTaskService scheduledTaskService = null,
            ILogService logService = null
        )
        {
            try
            {
                // Delete existing layout file if it exists
                if (File.Exists(StartMenuLayouts.Win10StartLayoutPath))
                {
                    File.Delete(StartMenuLayouts.Win10StartLayoutPath);
                }

                // Create new layout file with clean layout
                File.WriteAllText(
                    StartMenuLayouts.Win10StartLayoutPath,
                    StartMenuLayouts.Windows10Layout
                );

                // Ensure the directory exists for the layout file
                Directory.CreateDirectory(
                    Path.GetDirectoryName(StartMenuLayouts.Win10StartLayoutPath)!
                );

                // Always setup scheduled tasks for all existing users
                if (scheduledTaskService != null)
                {
                    logService?.LogInformation(
                        "Setting up scheduled tasks for all existing users..."
                    );
                    SetupScheduledTasksForAllUsersWindows10(scheduledTaskService, logService);
                }

                // Also apply to current user immediately
                ApplyWindows10LayoutToCurrentUser();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error cleaning Windows 10 Start Menu: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Applies Windows 10 Start Menu layout to the current user only.
        /// </summary>
        private void ApplyWindows10LayoutToCurrentUser()
        {
            // Set registry values to lock the Start Menu layout for current user
            using (
                var key = Registry.CurrentUser.CreateSubKey(
                    @"SOFTWARE\Policies\Microsoft\Windows\Explorer"
                )
            )
            {
                if (key != null)
                {
                    key.SetValue("LockedStartLayout", 1, RegistryValueKind.DWord);
                    key.SetValue(
                        "StartLayoutFile",
                        StartMenuLayouts.Win10StartLayoutPath,
                        RegistryValueKind.String
                    );
                }
            }

            // End the StartMenuExperienceHost process to apply changes immediately
            TerminateStartMenuExperienceHost();

            // Wait for changes to take effect
            System.Threading.Thread.Sleep(3000);

            // Disable the locked layout so user can customize again
            using (
                var key = Registry.CurrentUser.CreateSubKey(
                    @"SOFTWARE\Policies\Microsoft\Windows\Explorer"
                )
            )
            {
                if (key != null)
                {
                    key.SetValue("LockedStartLayout", 0, RegistryValueKind.DWord);
                }
            }

            // End the StartMenuExperienceHost process again to apply final changes
            TerminateStartMenuExperienceHost();
        }

        /// <summary>
        /// Sets up scheduled tasks for all existing users (Windows 10).
        /// </summary>
        /// <param name="scheduledTaskService">The scheduled task service.</param>
        /// <param name="logService">The logging service.</param>
        private void SetupScheduledTasksForAllUsersWindows10(
            IScheduledTaskService scheduledTaskService,
            ILogService logService = null
        )
        {
            try
            {
                var currentUsername = Environment.UserName;
                var otherUsernames = GetOtherUsernames();

                logService?.LogInformation(
                    $"Creating scheduled tasks for {otherUsernames.Count} other users (excluding current user: {currentUsername})"
                );

                if (otherUsernames.Count == 0)
                {
                    logService?.LogInformation(
                        "No other users found to create scheduled tasks for"
                    );
                    return;
                }

                foreach (var username in otherUsernames)
                {
                    try
                    {
                        var taskName = $"CleanStartMenu_{username}";

                        // PowerShell command matching XML template with self-deletion
                        var command =
                            $"-ExecutionPolicy Bypass -WindowStyle Hidden -Command \"$loggedInUser = (Get-WmiObject -Class Win32_ComputerSystem).UserName.Split('\\')[1]; $userSID = (New-Object System.Security.Principal.NTAccount($loggedInUser)).Translate([System.Security.Principal.SecurityIdentifier]).Value; reg add ('HKU\\' + $userSID + '\\SOFTWARE\\Policies\\Microsoft\\Windows\\Explorer') /v LockedStartLayout /t REG_DWORD /d 1 /f; reg add ('HKU\\' + $userSID + '\\SOFTWARE\\Policies\\Microsoft\\Windows\\Explorer') /v StartLayoutFile /t REG_SZ /d 'C:\\Users\\Default\\AppData\\Local\\Microsoft\\Windows\\Shell\\LayoutModification.xml' /f; Stop-Process -Name 'StartMenuExperienceHost' -Force -ErrorAction SilentlyContinue; Start-Sleep 10; Set-ItemProperty -Path ('Registry::HKU\\' + $userSID + '\\SOFTWARE\\Policies\\Microsoft\\Windows\\Explorer') -Name 'LockedStartLayout' -Value 0; Stop-Process -Name 'StartMenuExperienceHost' -Force -ErrorAction SilentlyContinue; schtasks /delete /tn 'Winhance\\{taskName}' /f\"";

                        // Create the scheduled task using the service
                        Task.Run(async () =>
                        {
                            try
                            {
                                await scheduledTaskService.CreateUserLogonTaskAsync(
                                    taskName,
                                    command,
                                    username,
                                    false
                                );
                                logService?.LogInformation(
                                    $"Successfully created scheduled task '{taskName}' for user '{username}'"
                                );
                            }
                            catch (Exception ex)
                            {
                                logService?.LogError(
                                    $"Failed to create scheduled task for user '{username}': {ex.Message}"
                                );
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        logService?.LogError(
                            $"Error setting up scheduled task for user '{username}': {ex.Message}"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                logService?.LogError(
                    $"Error in SetupScheduledTasksForAllUsersWindows10: {ex.Message}"
                );
            }
        }

        /// <summary>
        /// Terminates all StartMenuExperienceHost processes.
        /// </summary>
        public void TerminateStartMenuExperienceHost()
        {
            var startMenuProcesses = Process.GetProcessesByName("StartMenuExperienceHost");
            foreach (var process in startMenuProcesses)
            {
                try
                {
                    process.Kill();
                    process.WaitForExit(5000); // Wait up to 5 seconds for the process to exit
                }
                catch
                {
                    // Ignore errors - the process might have already exited or be inaccessible
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        /// <summary>
        /// Directly deletes start2.bin files from all other existing user profiles.
        /// Since Winhance runs as administrator, we can access other user directories directly.
        /// </summary>
        /// <param name="logService">The logging service.</param>
        private void CleanOtherUsersStartMenuFiles(ILogService logService = null)
        {
            try
            {
                var currentUsername = Environment.UserName;
                var otherUsernames = GetOtherUsernames();

                logService?.Log(
                    LogLevel.Info,
                    $"Cleaning Start Menu files for {otherUsernames.Count} other users (excluding current user: {currentUsername})"
                );

                if (otherUsernames.Count == 0)
                {
                    logService?.Log(
                        LogLevel.Info,
                        "No other users found to clean Start Menu files for"
                    );
                    return;
                }

                foreach (var username in otherUsernames)
                {
                    try
                    {
                        // Construct path to user's start2.bin file
                        string userProfilePath = $"C:\\Users\\{username}";
                        string start2BinPath = Path.Combine(
                            userProfilePath,
                            "AppData",
                            "Local",
                            "Packages",
                            "Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy",
                            "LocalState",
                            "start2.bin"
                        );

                        logService?.Log(
                            LogLevel.Info,
                            $"Attempting to delete start2.bin for user: {username}"
                        );

                        // Delete start2.bin file if it exists
                        if (File.Exists(start2BinPath))
                        {
                            File.Delete(start2BinPath);
                            logService?.Log(
                                LogLevel.Info,
                                $"Successfully deleted start2.bin for user: {username}"
                            );
                        }
                        else
                        {
                            logService?.Log(
                                LogLevel.Info,
                                $"start2.bin file not found for user: {username} (may not exist or user hasn't used Start Menu yet)"
                            );
                        }

                        // Also delete start.bin if it exists
                        string startBinPath = Path.Combine(
                            userProfilePath,
                            "AppData",
                            "Local",
                            "Packages",
                            "Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy",
                            "LocalState",
                            "start.bin"
                        );

                        if (File.Exists(startBinPath))
                        {
                            File.Delete(startBinPath);
                            logService?.Log(
                                LogLevel.Info,
                                $"Successfully deleted start.bin for user: {username}"
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        logService?.Log(
                            LogLevel.Warning,
                            $"Failed to delete Start Menu files for user {username}: {ex.Message}"
                        );
                        // Continue with other users even if one fails
                    }
                }

                logService?.Log(
                    LogLevel.Info,
                    "Completed cleaning Start Menu files for other users"
                );
            }
            catch (Exception ex)
            {
                logService?.Log(
                    LogLevel.Error,
                    $"Error during other users Start Menu cleaning: {ex.Message}"
                );
                // Don't throw - this is a best-effort feature
            }
        }

        /// <summary>
        /// Gets the current user's SID.
        /// </summary>
        /// <returns>The current user's SID as a string.</returns>
        private string GetCurrentUserSid()
        {
            try
            {
                using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
                {
                    return identity.User?.Value ?? string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets a list of other usernames (excluding current user and system accounts) from the ProfileList registry.
        /// This works for all users regardless of whether they are currently logged in.
        /// </summary>
        /// <returns>A list of usernames for other existing user accounts.</returns>
        private List<string> GetOtherUsernames()
        {
            var usernames = new List<string>();
            string currentUsername = Environment.UserName;

            try
            {
                // Use ProfileList registry to get ALL users (logged in or not)
                using (
                    var profileList = Registry.LocalMachine.OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList"
                    )
                )
                {
                    if (profileList != null)
                    {
                        foreach (string sidKey in profileList.GetSubKeyNames())
                        {
                            if (sidKey.StartsWith("S-1-5-21-")) // User SID pattern
                            {
                                using (var userKey = profileList.OpenSubKey(sidKey))
                                {
                                    string profilePath = userKey
                                        ?.GetValue("ProfileImagePath")
                                        ?.ToString();
                                    if (!string.IsNullOrEmpty(profilePath))
                                    {
                                        string username = Path.GetFileName(profilePath);
                                        // Skip current user and system accounts
                                        if (
                                            username != currentUsername
                                            && !IsSystemAccount(username)
                                        )
                                        {
                                            usernames.Add(username);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // Return empty list if we can't enumerate users
            }

            return usernames;
        }

        /// <summary>
        /// Checks if a username represents a system account that should be excluded.
        /// </summary>
        /// <param name="username">The username to check.</param>
        /// <returns>True if it's a system account, false otherwise.</returns>
        private bool IsSystemAccount(string username)
        {
            string[] systemAccounts = { "Public", "Default", "All Users", "Default User" };
            return systemAccounts.Contains(username, StringComparer.OrdinalIgnoreCase);
        }
    }
}
