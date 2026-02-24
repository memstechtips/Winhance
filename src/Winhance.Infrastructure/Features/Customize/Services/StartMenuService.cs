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
    public class StartMenuService(
        IScheduledTaskService scheduledTaskService,
        ILogService logService,
        ICompatibleSettingsRegistry compatibleSettingsRegistry,
        IInteractiveUserService interactiveUserService,
        IProcessExecutor processExecutor,
        IFileSystemService fileSystemService) : IDomainService, IActionCommandProvider
    {
        // Caching fields
        private IEnumerable<SettingDefinition>? _cachedSettings;
        private readonly object _cacheLock = new object();

        public string DomainName => FeatureIds.StartMenu;

        public async Task<IEnumerable<SettingDefinition>> GetSettingsAsync()
        {
            // Return cached settings if available
            if (_cachedSettings != null)
                return _cachedSettings;

            lock (_cacheLock)
            {
                // Double-check locking pattern
                if (_cachedSettings != null)
                    return _cachedSettings;

                try
                {
                    logService.Log(LogLevel.Info, "Loading Start Menu settings");

                    _cachedSettings = compatibleSettingsRegistry.GetFilteredSettings(FeatureIds.StartMenu);
                    return _cachedSettings;
                }
                catch (Exception ex)
                {
                    logService.Log(LogLevel.Error, $"Error loading Start Menu settings: {ex.Message}");
                    return Enumerable.Empty<SettingDefinition>();
                }
            }
        }

        public void InvalidateCache()
        {
            lock (_cacheLock)
            {
                _cachedSettings = null;
                logService.Log(LogLevel.Debug, "Start Menu settings cache cleared");
            }
        }

        public async Task CleanWindows10StartMenuAsync()
        {
            try
            {
                logService.Log(LogLevel.Info, "Starting Windows 10 Start Menu cleaning process");

                await Task.Run(async () =>
                    await CleanWindows10StartMenu(scheduledTaskService, logService).ConfigureAwait(false)
                ).ConfigureAwait(false);

                logService.Log(LogLevel.Info, "Windows 10 Start Menu cleaned successfully");
            }
            catch (Exception ex)
            {
                logService.Log(
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
                logService.Log(LogLevel.Info, "Starting Windows 11 Start Menu cleaning process");

                await Task.Run(() => CleanWindows11StartMenu(logService)).ConfigureAwait(false);

                logService.Log(LogLevel.Info, "Windows 11 Start Menu cleaned successfully");
            }
            catch (Exception ex)
            {
                logService.Log(
                    LogLevel.Error,
                    $"Error cleaning Windows 11 Start Menu: {ex.Message}"
                );
                throw;
            }
        }

        private void CleanWindows11StartMenu(ILogService? logService = null)
        {
            try
            {
                // Step 1: Add registry entry to configure empty pinned list
                var regArgs = "add \"HKLM\\SOFTWARE\\Microsoft\\PolicyManager\\current\\device\\Start\" /v \"ConfigureStartPins\" /t REG_SZ /d \"{\\\"pinnedList\\\":[]}\" /f";
                var result = processExecutor.ExecuteAsync("reg.exe", regArgs).GetAwaiter().GetResult();

                if (result.ExitCode != 0)
                {
                    throw new Exception(
                        $"Failed to add registry entry. Exit code: {result.ExitCode}. Error: {result.StandardError}"
                    );
                }

                // Step 2: Delete start.bin and start2.bin files from LocalState directory
                string localAppData = interactiveUserService.GetInteractiveUserFolderPath(
                    Environment.SpecialFolder.LocalApplicationData
                );
                string startMenuLocalStatePath = fileSystemService.CombinePath(
                    localAppData,
                    "Packages",
                    "Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy",
                    "LocalState"
                );

                if (fileSystemService.DirectoryExists(startMenuLocalStatePath))
                {
                    // Delete start.bin if it exists
                    string startBinPath = fileSystemService.CombinePath(startMenuLocalStatePath, "start.bin");
                    if (fileSystemService.FileExists(startBinPath))
                    {
                        fileSystemService.DeleteFile(startBinPath);
                    }

                    // Delete start2.bin if it exists
                    string start2BinPath = fileSystemService.CombinePath(startMenuLocalStatePath, "start2.bin");
                    if (fileSystemService.FileExists(start2BinPath))
                    {
                        fileSystemService.DeleteFile(start2BinPath);
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

        private async Task CleanWindows10StartMenu(
            IScheduledTaskService? scheduledTaskService = null,
            ILogService? logService = null
        )
        {
            try
            {
                // Delete existing layout file if it exists
                if (fileSystemService.FileExists(StartMenuLayouts.Win10StartLayoutPath))
                {
                    fileSystemService.DeleteFile(StartMenuLayouts.Win10StartLayoutPath);
                }

                // Create new layout file with clean layout
                fileSystemService.WriteAllText(
                    StartMenuLayouts.Win10StartLayoutPath,
                    StartMenuLayouts.Windows10Layout
                );

                // Ensure the directory exists for the layout file
                fileSystemService.CreateDirectory(
                    fileSystemService.GetDirectoryName(StartMenuLayouts.Win10StartLayoutPath)!
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
                await ApplyWindows10LayoutToCurrentUser().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error cleaning Windows 10 Start Menu: {ex.Message}", ex);
            }
        }

        private async Task ApplyWindows10LayoutToCurrentUser()
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
            await Task.Delay(3000).ConfigureAwait(false);

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

        private void SetupScheduledTasksForAllUsersWindows10(
            IScheduledTaskService scheduledTaskService,
            ILogService? logService = null
        )
        {
            try
            {
                var currentUsername = interactiveUserService.InteractiveUserName;
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
                                ).ConfigureAwait(false);
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

        private void CleanOtherUsersStartMenuFiles(ILogService? logService = null)
        {
            try
            {
                var currentUsername = interactiveUserService.InteractiveUserName;
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
                        string start2BinPath = fileSystemService.CombinePath(
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
                        if (fileSystemService.FileExists(start2BinPath))
                        {
                            fileSystemService.DeleteFile(start2BinPath);
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
                        string startBinPath = fileSystemService.CombinePath(
                            userProfilePath,
                            "AppData",
                            "Local",
                            "Packages",
                            "Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy",
                            "LocalState",
                            "start.bin"
                        );

                        if (fileSystemService.FileExists(startBinPath))
                        {
                            fileSystemService.DeleteFile(startBinPath);
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

        private List<string> GetOtherUsernames()
        {
            var usernames = new List<string>();
            string currentUsername = interactiveUserService.InteractiveUserName;

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
                                    string? profilePath = userKey
                                        ?.GetValue("ProfileImagePath")
                                        ?.ToString();
                                    if (!string.IsNullOrEmpty(profilePath))
                                    {
                                        string username = fileSystemService.GetFileName(profilePath);
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

        private bool IsSystemAccount(string username)
        {
            string[] systemAccounts = { "Public", "Default", "All Users", "Default User" };
            return systemAccounts.Contains(username, StringComparer.OrdinalIgnoreCase);
        }
        private static readonly HashSet<string> _supportedCommands = new(StringComparer.Ordinal)
        {
            nameof(CleanWindows10StartMenuAsync),
            nameof(CleanWindows11StartMenuAsync)
        };

        public IReadOnlySet<string> SupportedCommands => _supportedCommands;

        public Task ExecuteCommandAsync(string commandName) => commandName switch
        {
            nameof(CleanWindows10StartMenuAsync) => CleanWindows10StartMenuAsync(),
            nameof(CleanWindows11StartMenuAsync) => CleanWindows11StartMenuAsync(),
            _ => throw new NotSupportedException($"Command '{commandName}' is not supported by {nameof(StartMenuService)}")
        };
    }
}