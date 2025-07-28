using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Services;
using Winhance.Core.Features.Customize.Enums;

namespace Winhance.Core.Features.Customize.Models;

public static class StartMenuCustomizations
{
    private const string Win10StartLayoutPath = @"C:\Users\Default\AppData\Local\Microsoft\Windows\Shell\LayoutModification.xml";

    public static CustomizationGroup GetStartMenuCustomizations()
    {
        return new CustomizationGroup
        {
            Name = "Start Menu",
            Category = CustomizationCategory.StartMenu,
            Settings = new List<CustomizationSetting>
            {
                new CustomizationSetting
                {
                    Id = "start-menu-layout",
                    Name = "Start Layout",
                    Description = "Controls Start Menu layout configuration (Windows 11 24H2 only, removed in build 26120.4250+)",
                    Category = CustomizationCategory.StartMenu,
                    GroupName = "Layout",
                    IsEnabled = false,
                    ControlType = ControlType.ComboBox,
                    IsWindows11Only = true,
                    MinimumBuildNumber = 22000, // Windows 11 24H2 starts around build 26100
                    MaximumBuildNumber = 26120, // Removed in build 26120.4250, so max 26120
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "StartMenu",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced",
                            Name = "Start_Layout",
                            RecommendedValue = 1,  // More Pins
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0,      // Windows default is default layout
                            Description = "Controls Start Menu layout configuration. 0=Default, 1=More pins, 2=More recommendations",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                            // ComboBox options mapping:
                            // Value 0 = "Default"
                            // Value 1 = "More pins" 
                            // Value 2 = "More recommendations"
                            CustomProperties = new Dictionary<string, object>
                            {
                                ["ComboBoxOptions"] = new Dictionary<string, int>
                                {
                                    ["Default"] = 0,
                                    ["More pins"] = 1,
                                    ["More recommendations"] = 2
                                },
                                ["DefaultOption"] = "Default"
                            }
                        }
                    }
                },
                new CustomizationSetting
                {
                    Id = "show-all-pins-by-default",
                    Name = "Show all pins by default",
                    Description = "Controls whether all pins are shown by default in Start Menu (Windows 11 24H2 build 26120.4250+ and 25H2 build 26200.5670+)",
                    Category = CustomizationCategory.StartMenu,
                    GroupName = "Start Menu Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    IsWindows11Only = true,
                    SupportedBuildRanges = new List<(int, int)>
                    {
                        (26120, int.MaxValue), // Windows 11 24H2 build 26120.4250 and later
                        (26200, int.MaxValue)  // Windows 11 25H2 build 26200.5670 and later
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "StartMenu",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Start",
                            Name = "ShowAllPinsList",
                            RecommendedValue = 1,
                            EnabledValue = 1, // When toggle is ON, all pins are shown
                            DisabledValue = 0, // When toggle is OFF, all pins are not shown
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls whether all pins are shown by default in Start Menu",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new CustomizationSetting
                {
                    Id = "show-recently-added-apps",
                    Name = "Show Recently Added Apps",
                    Description = "Controls visibility of recently added apps in Start Menu",
                    Category = CustomizationCategory.StartMenu,
                    GroupName = "Start Menu Settings",
                    IsEnabled = true,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Policies\\Microsoft\\Windows\\Explorer",
                            Name = "HideRecentlyAddedApps",
                            RecommendedValue = 0,
                            EnabledValue = 0, // When toggle is ON, recently added apps are shown
                            DisabledValue = 1, // When toggle is OFF, recently added apps are hidden
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            Description =
                                "Controls visibility of recently added apps in Start Menu",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Policies\\Microsoft\\Windows\\Explorer",
                            Name = "HideRecentlyAddedApps",
                            RecommendedValue = 0,
                            EnabledValue = 0, // When toggle is ON, recently added apps are shown
                            DisabledValue = 1, // When toggle is OFF, recently added apps are hidden
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            Description =
                                "Controls visibility of recently added apps in Start Menu",
                            IsPrimary = false,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
                new CustomizationSetting
                {
                    Id = "start-track-progs",
                    Name = "Show Most Used Apps",
                    Description = "Controls Show Most Used Apps Setting in Start Menu",
                    Category = CustomizationCategory.StartMenu,
                    GroupName = "Start Menu",
                    IsEnabled = true,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "StartMenu",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced",
                            Name = "Start_TrackProgs",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, frequently used programs list is shown
                            DisabledValue = 0, // When toggle is OFF, frequently used programs list is hidden
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls Show Most Used Apps Setting in Start Menu",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new CustomizationSetting
                {
                    Id = "show-suggestions-in-start",
                    Name = "Show suggestions in Start",
                    Description = "Controls visibility of suggestions in Start Menu",
                    Category = CustomizationCategory.StartMenu,
                    GroupName = "Start Menu Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    IsWindows10Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "ContentDelivery",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager",
                            Name = "SubscribedContent-338388Enabled",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, suggestions are shown
                            DisabledValue = 0, // When toggle is OFF, suggestions are hidden
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls visibility of suggestions in Start Menu",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new CustomizationSetting
                {
                    Id = "show-recommended-files",
                    Name = "Show Recommended Files/Recently Opened Items",
                    Description = "Controls visibility of recommended files/recently opened items in Start Menu",
                    Category = CustomizationCategory.StartMenu,
                    GroupName = "Start Menu Settings",
                    IsEnabled = true,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = RegistryHive.CurrentUser,
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced",
                            Name = "Start_TrackDocs",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, recommended files are shown
                            DisabledValue = 0, // When toggle is OFF, recommended files are hidden
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls visibility of recommended files/recently opened items in Start Menu",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new CustomizationSetting
                {
                    Id = "start-menu-recommendations",
                    Name = "Show Recommended Tips, Shortcuts etc.",
                    Description = "Controls recommendations for tips and shortcuts",
                    Category = CustomizationCategory.StartMenu,
                    GroupName = "Start Menu Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    IsWindows11Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = RegistryHive.CurrentUser,
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced",
                            Name = "Start_IrisRecommendations",
                            RecommendedValue = 0,
                            EnabledValue = 1,
                            DisabledValue = 0,
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls recommendations for tips and shortcuts",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new CustomizationSetting
                {
                    Id = "show-account-notifications",
                    Name = "Show Account-related Notifications",
                    Description = "Controls visibility of account-related notifications in Start Menu",
                    Category = CustomizationCategory.StartMenu,
                    GroupName = "Start Menu Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = RegistryHive.CurrentUser,
                            SubKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced",
                            Name = "Start_AccountNotifications",
                            RecommendedValue = 0,
                            EnabledValue = 1, // When toggle is ON, account notifications are shown
                            DisabledValue = 0, // When toggle is OFF, account notifications are hidden
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls visibility of account-related notifications in Start Menu",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new CustomizationSetting
                {
                    Id = "power-lock-option",
                    Name = "Hide Lock Option",
                    Description = "Controls whether the lock option is hidden in the Start menu power flyout",
                    Category = CustomizationCategory.StartMenu,
                    GroupName = "Start Menu",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Power",
                            Hive = RegistryHive.LocalMachine,
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\System",
                            Name = "DisableLockWorkstation",
                            RecommendedValue = 1,
                            EnabledValue = 1, // Hide lock option
                            DisabledValue = 0, // Show lock option
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            Description =
                                "Controls whether the lock option is hidden in the Start menu power flyout",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
                        },
                    },
                },
            },
        };
    }

    public static void ApplyStartMenuLayout(bool isWindows11, ISystemServices windowsService)
    {
        if (isWindows11)
        {
            // For Windows 11, use the same approach as CleanWindows11StartMenu
            // This applies an empty pinned list layout
            CleanWindows11StartMenu();
        }
        else
        {
            ApplyWindows10Layout(windowsService);
        }
    }

    private static void ApplyWindows10Layout(ISystemServices windowsService)
    {
        // Delete existing layout file
        if (File.Exists(Win10StartLayoutPath))
        {
            File.Delete(Win10StartLayoutPath);
        }

        // Create new layout file
        File.WriteAllText(Win10StartLayoutPath, StartMenuLayouts.Windows10Layout);

        // Use the improved RefreshWindowsGUI method to restart Explorer and apply changes
        // This will ensure Explorer is restarted properly with retry logic and fallback
        var result = windowsService.RefreshWindowsGUI(true).GetAwaiter().GetResult();
        if (!result)
        {
            throw new Exception("Failed to refresh Windows GUI after applying Start Menu layout");
        }
    }

    /// <summary>
    /// Cleans the Start Menu for Windows 10 or Windows 11.
    /// </summary>
    /// <param name="isWindows11">Whether the system is Windows 11.</param>
    /// <param name="windowsService">The system services.</param>
    /// <param name="applyToAllUsers">Whether to apply cleaning to all existing user accounts.</param>
    /// <param name="logService">The logging service.</param>
    /// <param name="scheduledTaskService">The scheduled task service.</param>
    public static void CleanStartMenu(bool isWindows11, ISystemServices windowsService, bool applyToAllUsers = false, ILogService logService = null, IScheduledTaskService scheduledTaskService = null)
    {
        if (isWindows11)
        {
            CleanWindows11StartMenu(applyToAllUsers, logService, scheduledTaskService);
        }
        else
        {
            CleanWindows10StartMenu(windowsService, applyToAllUsers);
        }
    }

    /// <summary>
    /// Cleans the Windows 11 Start Menu by setting registry policy and removing start menu files.
    /// </summary>
    /// <param name="applyToAllUsers">Whether to apply cleaning to all existing user accounts.</param>
    /// <param name="logService">The logging service.</param>
    /// <param name="scheduledTaskService">The scheduled task service.</param>
    private static void CleanWindows11StartMenu(bool applyToAllUsers = false, ILogService logService = null, IScheduledTaskService scheduledTaskService = null)
    {
        try
        {
            // Step 1: Add registry entry to configure empty pinned list
            using (var process = new System.Diagnostics.Process())
            {
                process.StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "reg.exe",
                    Arguments = "add \"HKLM\\SOFTWARE\\Microsoft\\PolicyManager\\current\\device\\Start\" /v \"ConfigureStartPins\" /t REG_SZ /d \"{\\\"pinnedList\\\":[]}\" /f",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    Verb = "runas" // Run as administrator
                };

                process.Start();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string error = process.StandardError.ReadToEnd();
                    throw new Exception($"Failed to add registry entry. Exit code: {process.ExitCode}. Error: {error}");
                }
            }

            // Step 2: Delete start.bin and start2.bin files from LocalState directory
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string startMenuLocalStatePath = Path.Combine(localAppData, "Packages", "Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy", "LocalState");

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

            // Step 3: If applying to all users, set up scheduled tasks for existing user accounts
            if (applyToAllUsers)
            {
                SetupScheduledTasksForAllUsers(logService, scheduledTaskService);
            }

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
    /// This applies to the current user immediately and ensures new accounts inherit the clean layout.
    /// </summary>
    /// <param name="windowsService">The system services.</param>
    /// <param name="applyToAllUsers">Whether to apply cleaning to all existing user accounts.</param>
    private static void CleanWindows10StartMenu(ISystemServices windowsService, bool applyToAllUsers = false)
    {
        try
        {
            // Delete existing layout file if it exists
            if (File.Exists(Win10StartLayoutPath))
            {
                File.Delete(Win10StartLayoutPath);
            }

            // Create new layout file with clean layout
            File.WriteAllText(Win10StartLayoutPath, StartMenuLayouts.Windows10Layout);

            // Ensure the directory exists for the layout file
            Directory.CreateDirectory(Path.GetDirectoryName(Win10StartLayoutPath)!);

            // Set registry values to lock the Start Menu layout
            // Use HKLM for all users, HKCU for current user only
            var registryKey = applyToAllUsers ? Registry.LocalMachine : Registry.CurrentUser;
            using (
                var key = registryKey.CreateSubKey(
                    @"SOFTWARE\Policies\Microsoft\Windows\Explorer"
                )
            )
            {
                if (key != null)
                {
                    key.SetValue("LockedStartLayout", 1, RegistryValueKind.DWord);
                    key.SetValue("StartLayoutFile", Win10StartLayoutPath, RegistryValueKind.String);
                }
            }

            // End the StartMenuExperienceHost process to apply changes immediately
            TerminateStartMenuExperienceHost();

            // Wait for changes to take effect
            System.Threading.Thread.Sleep(3000);

            // For current user only: disable the locked layout so user can customize again
            // For all users: keep the layout locked to ensure it applies to everyone
            if (!applyToAllUsers)
            {
                using (
                    var key = registryKey.CreateSubKey(
                        @"SOFTWARE\Policies\Microsoft\Windows\Explorer"
                    )
                )
                {
                    if (key != null)
                    {
                        key.SetValue("LockedStartLayout", 0, RegistryValueKind.DWord);
                    }
                }
            }

            // End the StartMenuExperienceHost process again to apply final changes
            TerminateStartMenuExperienceHost();

            // Keep the layout file in place so new user accounts inherit the clean layout
        }
        catch (Exception ex)
        {
            throw new Exception($"Error cleaning Windows 10 Start Menu: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Terminates all StartMenuExperienceHost processes.
    /// </summary>
    private static void TerminateStartMenuExperienceHost()
    {
        var startMenuProcesses = System.Diagnostics.Process.GetProcessesByName("StartMenuExperienceHost");
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
    /// Sets up scheduled tasks for all existing user accounts to clean their Start Menu on next login.
    /// Uses scheduled tasks instead of RunOnce registry entries to work with users who are not currently logged in.
    /// </summary>
    /// <param name="logService">The logging service.</param>
    /// <param name="scheduledTaskService">The scheduled task service.</param>
    private static async void SetupScheduledTasksForAllUsers(ILogService logService = null, IScheduledTaskService scheduledTaskService = null)
    {
        try
        {
            var currentUsername = Environment.UserName;
            var otherUsernames = GetOtherUsernames();
            
            logService?.Log(LogLevel.Info, $"Setting up scheduled tasks for {otherUsernames.Count} other users (excluding current user: {currentUsername})");
            
            if (otherUsernames.Count == 0)
            {
                logService?.Log(LogLevel.Info, "No other users found to create scheduled tasks for");
                return;
            }

            // If no scheduled task service is provided, fall back to manual creation
            if (scheduledTaskService == null)
            {
                logService?.Log(LogLevel.Warning, "No scheduled task service provided, cannot create user-specific tasks");
                return;
            }

            foreach (var username in otherUsernames)
            {
                try
                {
                    string taskName = $"WinhanceStartMenuClean_{username}";
                    // Command with full user path that cleans Start Menu and deletes the task itself
                    string userProfilePath = $"C:\\Users\\{username}";
                    string start2BinPath = $"{userProfilePath}\\AppData\\Local\\Packages\\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\\LocalState\\start2.bin";
                    string command = $"/c \"del /f /q \"{start2BinPath}\" 2>nul & taskkill /f /im StartMenuExperienceHost.exe 2>nul & schtasks /delete /tn \"{taskName}\" /f 2>nul\"";
                    
                    logService?.Log(LogLevel.Info, $"Creating scheduled task for user: {username}");
                    
                    // Use the scheduled task service to create user-specific logon task
                    bool success = await scheduledTaskService.CreateUserLogonTaskAsync(taskName, command, username, deleteAfterRun: true);
                    
                    if (success)
                    {
                        logService?.Log(LogLevel.Info, $"Successfully created scheduled task for user: {username}");
                    }
                    else
                    {
                        logService?.Log(LogLevel.Warning, $"Failed to create scheduled task for user: {username}");
                    }
                }
                catch (Exception ex)
                {
                    logService?.Log(LogLevel.Error, $"Exception creating scheduled task for user {username}: {ex.Message}");
                    // Continue with other users even if one fails
                }
            }
        }
        catch
        {
            // Ignore errors in the overall process - this is a best-effort feature
        }
    }

    /// <summary>
    /// Gets the current user's SID.
    /// </summary>
    /// <returns>The current user's SID as a string.</returns>
    private static string GetCurrentUserSid()
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
    private static List<string> GetOtherUsernames()
    {
        var usernames = new List<string>();
        string currentUsername = Environment.UserName;
        
        try
        {
            // Use ProfileList registry to get ALL users (logged in or not)
            using (var profileList = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList"))
            {
                if (profileList != null)
                {
                    foreach (string sidKey in profileList.GetSubKeyNames())
                    {
                        if (sidKey.StartsWith("S-1-5-21-")) // User SID pattern
                        {
                            using (var userKey = profileList.OpenSubKey(sidKey))
                            {
                                string profilePath = userKey?.GetValue("ProfileImagePath")?.ToString();
                                if (!string.IsNullOrEmpty(profilePath))
                                {
                                    string username = Path.GetFileName(profilePath);
                                    // Skip current user and system accounts
                                    if (username != currentUsername && !IsSystemAccount(username))
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
    private static bool IsSystemAccount(string username)
    {
        string[] systemAccounts = { "Public", "Default", "All Users", "Default User" };
        return systemAccounts.Contains(username, StringComparer.OrdinalIgnoreCase);
    }
}
