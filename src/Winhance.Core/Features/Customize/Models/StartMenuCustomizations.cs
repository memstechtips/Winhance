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
                    Name = "Show Recommended & Recently Opened Items",
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
                new CustomizationSetting
                {
                    Id = "display-bing-search-results",
                    Name = "Display Bing Search Results",
                    Description = "Controls whether Bing search results are displayed in Start Menu search",
                    Category = CustomizationCategory.StartMenu,
                    GroupName = "Start Menu Settings",
                    IsEnabled = true,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Policies\\Microsoft\\Windows\\Explorer",
                            Name = "DisableSearchBoxSuggestions",
                            RecommendedValue = 1,
                            EnabledValue = 0, // When toggle is ON, Bing search results are displayed (DisableSearchBoxSuggestions = 0)
                            DisabledValue = 1, // When toggle is OFF, Bing search results are disabled (DisableSearchBoxSuggestions = 1)
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            Description = "Controls whether Bing search results are displayed in Start Menu search",
                            IsPrimary = false,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new CustomizationSetting
                {
                    Id = "remove-recommended-section",
                    Name = "Remove Recommended Section",
                    Description = "Removes the recommended section from the Start Menu",
                    Category = CustomizationCategory.StartMenu,
                    GroupName = "Start Menu Settings",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    IsWindows11Only = true,
                    LinkedSettingsLogic = LinkedSettingsLogic.All,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Policies\\Microsoft\\Windows\\Explorer",
                            Name = "HideRecommendedSection",
                            RecommendedValue = 1,
                            EnabledValue = 1, // When toggle is ON, recommended section is hidden
                            DisabledValue = 0, // When toggle is OFF, recommended section is shown
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            Description = "Removes the recommended section from the Start Menu",
                            IsPrimary = true,
                            IsGroupPolicy = true,
                            AbsenceMeansEnabled = false,
                        },
                        new RegistrySetting
                        {
                            Category = "Start",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Microsoft\\PolicyManager\\current\\device\\Start",
                            Name = "HideRecommendedSection",
                            RecommendedValue = 1,
                            EnabledValue = 1, // When toggle is ON, recommended section is hidden
                            DisabledValue = 0, // When toggle is OFF, recommended section is shown
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            Description = "Removes the recommended section from the Start Menu (PolicyManager)",
                            IsPrimary = false,
                            IsGroupPolicy = true,
                            AbsenceMeansEnabled = false,
                        },
                        new RegistrySetting
                        {
                            Category = "Education",
                            Hive = RegistryHive.LocalMachine,
                            SubKey = "SOFTWARE\\Microsoft\\PolicyManager\\current\\device\\Education",
                            Name = "IsEducationEnvironment",
                            RecommendedValue = 1,
                            EnabledValue = 1, // When toggle is ON, education environment is enabled
                            DisabledValue = 0, // When toggle is OFF, education environment is disabled
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0, // Default value when registry key exists but no value is set
                            Description = "Sets education environment flag to help hide recommended section",
                            IsPrimary = false,
                            IsGroupPolicy = true,
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
    /// <param name="logService">The logging service.</param>
    /// <param name="scheduledTaskService">The scheduled task service for creating user-specific tasks.</param>
    public static void CleanStartMenu(
        bool isWindows11,
        ISystemServices windowsService,
        ILogService logService = null,
        IScheduledTaskService scheduledTaskService = null
    )
    {
        if (isWindows11)
        {
            CleanWindows11StartMenu(logService);
        }
        else
        {
            CleanWindows10StartMenu(windowsService, scheduledTaskService, logService);
        }
    }

    /// <summary>
    /// Cleans the Windows 11 Start Menu by setting registry policy and removing start menu files.
    /// Always applies to all users on the system.
    /// </summary>
    /// <param name="logService">The logging service.</param>
    private static void CleanWindows11StartMenu(ILogService logService = null)
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
    private static void CleanWindows10StartMenu(
        ISystemServices windowsService,
        IScheduledTaskService scheduledTaskService = null,
        ILogService logService = null
    )
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

            // Always setup scheduled tasks for all existing users
            if (scheduledTaskService != null)
            {
                logService?.LogInformation("Setting up scheduled tasks for all existing users...");
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
    private static void ApplyWindows10LayoutToCurrentUser()
    {
        // Set registry values to lock the Start Menu layout for current user
        using (var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Explorer"))
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

        // Disable the locked layout so user can customize again
        using (var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Explorer"))
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
    private static void SetupScheduledTasksForAllUsersWindows10(IScheduledTaskService scheduledTaskService, ILogService logService = null)
    {
        try
        {
            var currentUsername = Environment.UserName;
            var otherUsernames = GetOtherUsernames();
            
            logService?.LogInformation($"Creating scheduled tasks for {otherUsernames.Count} other users (excluding current user: {currentUsername})");
            
            if (otherUsernames.Count == 0)
            {
                logService?.LogInformation("No other users found to create scheduled tasks for");
                return;
            }

            foreach (var username in otherUsernames)
            {
                try
                {
                    var taskName = $"StartStTest_{username}";
                    
                    // PowerShell command matching XML template with self-deletion
                    var command = $"-ExecutionPolicy Bypass -WindowStyle Hidden -Command \"$loggedInUser = (Get-WmiObject -Class Win32_ComputerSystem).UserName.Split('\\')[1]; $userSID = (New-Object System.Security.Principal.NTAccount($loggedInUser)).Translate([System.Security.Principal.SecurityIdentifier]).Value; reg add ('HKU\\' + $userSID + '\\SOFTWARE\\Policies\\Microsoft\\Windows\\Explorer') /v LockedStartLayout /t REG_DWORD /d 1 /f; reg add ('HKU\\' + $userSID + '\\SOFTWARE\\Policies\\Microsoft\\Windows\\Explorer') /v StartLayoutFile /t REG_SZ /d 'C:\\Users\\Default\\AppData\\Local\\Microsoft\\Windows\\Shell\\LayoutModification.xml' /f; Stop-Process -Name 'StartMenuExperienceHost' -Force -ErrorAction SilentlyContinue; Start-Sleep 10; Set-ItemProperty -Path ('Registry::HKU\\' + $userSID + '\\SOFTWARE\\Policies\\Microsoft\\Windows\\Explorer') -Name 'LockedStartLayout' -Value 0; Stop-Process -Name 'StartMenuExperienceHost' -Force -ErrorAction SilentlyContinue; schtasks /delete /tn 'Winhance\\{taskName}' /f\"";
                    
                    // Create the scheduled task using the service
                    Task.Run(async () =>
                    {
                        try
                        {
                            await scheduledTaskService.CreateUserLogonTaskAsync(taskName, command, username, false);
                            logService?.LogInformation($"Successfully created scheduled task '{taskName}' for user '{username}'");
                        }
                        catch (Exception ex)
                        {
                            logService?.LogError($"Failed to create scheduled task for user '{username}': {ex.Message}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    logService?.LogError($"Error setting up scheduled task for user '{username}': {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            logService?.LogError($"Error in SetupScheduledTasksForAllUsersWindows10: {ex.Message}");
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
    /// Directly deletes start2.bin files from all other existing user profiles.
    /// Since Winhance runs as administrator, we can access other user directories directly.
    /// </summary>
    /// <param name="logService">The logging service.</param>
    private static void CleanOtherUsersStartMenuFiles(ILogService logService = null)
    {
        try
        {
            var currentUsername = Environment.UserName;
            var otherUsernames = GetOtherUsernames();
            
            logService?.Log(LogLevel.Info, $"Cleaning Start Menu files for {otherUsernames.Count} other users (excluding current user: {currentUsername})");
            
            if (otherUsernames.Count == 0)
            {
                logService?.Log(LogLevel.Info, "No other users found to clean Start Menu files for");
                return;
            }

            foreach (var username in otherUsernames)
            {
                try
                {
                    // Construct path to user's start2.bin file
                    string userProfilePath = $"C:\\Users\\{username}";
                    string start2BinPath = Path.Combine(userProfilePath, "AppData", "Local", "Packages", 
                        "Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy", "LocalState", "start2.bin");
                    
                    logService?.Log(LogLevel.Info, $"Attempting to delete start2.bin for user: {username}");
                    
                    // Delete start2.bin file if it exists
                    if (File.Exists(start2BinPath))
                    {
                        File.Delete(start2BinPath);
                        logService?.Log(LogLevel.Info, $"Successfully deleted start2.bin for user: {username}");
                    }
                    else
                    {
                        logService?.Log(LogLevel.Info, $"start2.bin file not found for user: {username} (may not exist or user hasn't used Start Menu yet)");
                    }
                    
                    // Also delete start.bin if it exists
                    string startBinPath = Path.Combine(userProfilePath, "AppData", "Local", "Packages", 
                        "Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy", "LocalState", "start.bin");
                    
                    if (File.Exists(startBinPath))
                    {
                        File.Delete(startBinPath);
                        logService?.Log(LogLevel.Info, $"Successfully deleted start.bin for user: {username}");
                    }
                }
                catch (Exception ex)
                {
                    logService?.Log(LogLevel.Warning, $"Failed to delete Start Menu files for user {username}: {ex.Message}");
                    // Continue with other users even if one fails
                }
            }
            
            logService?.Log(LogLevel.Info, "Completed cleaning Start Menu files for other users");
        }
        catch (Exception ex)
        {
            logService?.Log(LogLevel.Error, $"Error during other users Start Menu cleaning: {ex.Message}");
            // Don't throw - this is a best-effort feature
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
