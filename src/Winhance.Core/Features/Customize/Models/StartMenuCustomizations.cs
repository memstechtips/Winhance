using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Services;
using Winhance.Core.Features.Customize.Enums;

namespace Winhance.Core.Features.Customize.Models;

public static class StartMenuCustomizations
{
    private const string Win10StartLayoutPath = @"C:\Windows\StartMenuLayout.xml";
    private const string Win11StartBinPath =
        @"AppData\Local\Packages\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\LocalState\start2.bin";

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
                    Id = "start-menu-layout",
                    Name = "Set 'More Pins' Layout",
                    Description = "Controls Start Menu layout configuration",
                    Category = CustomizationCategory.StartMenu,
                    GroupName = "Layout",
                    IsEnabled = true,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "StartMenu",
                            Hive = RegistryHive.CurrentUser,
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced",
                            Name = "Start_Layout",
                            RecommendedValue = 1, // For backward compatibility
                            EnabledValue = 1, // When toggle is ON, more pins layout is used
                            DisabledValue = 0, // When toggle is OFF, default layout is used
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 0, // For backward compatibility
                            Description = "Controls Start Menu layout configuration",
                            IsPrimary = true,
                            AbsenceMeansEnabled = false,
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
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Explorer",
                            Hive = RegistryHive.CurrentUser,
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced",
                            Name = "Start_IrisRecommendations",
                            RecommendedValue = 1,
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
                    Id = "taskbar-clear-mfu",
                    Name = "Show Most Used Apps",
                    Description = "Controls frequently used programs list visibility",
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
                            SubKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Start",
                            Name = "ShowFrequentList",
                            RecommendedValue = 1,
                            EnabledValue = 1, // When toggle is ON, frequently used programs list is shown
                            DisabledValue = 0, // When toggle is OFF, frequently used programs list is hidden
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls frequently used programs list visibility",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new CustomizationSetting
                {
                    Id = "power-lock-option",
                    Name = "Lock Option",
                    Description = "Controls whether the lock option is shown in the Start menu",
                    Category = CustomizationCategory.StartMenu,
                    GroupName = "Start Menu",
                    IsEnabled = false,
                    ControlType = ControlType.BinaryToggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            Category = "Power",
                            Hive = RegistryHive.CurrentUser,
                            SubKey =
                                "Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\FlyoutMenuSettings",
                            Name = "ShowLockOption",
                            RecommendedValue = 1,
                            EnabledValue = 1, // Show lock option
                            DisabledValue = 0, // Hide lock option
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description =
                                "Controls whether the lock option is shown in the Start menu",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
                        },
                    },
                },
                new CustomizationSetting
                {
                    Id = "show-recommended-files",
                    Name = "Show Recommended Files",
                    Description = "Controls visibility of recommended files in Start Menu",
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
                            RecommendedValue = 1,
                            EnabledValue = 1, // When toggle is ON, recommended files are shown
                            DisabledValue = 0, // When toggle is OFF, recommended files are hidden
                            ValueType = RegistryValueKind.DWord,
                            DefaultValue = 1, // Default value when registry key exists but no value is set
                            Description = "Controls visibility of recommended files in Start Menu",
                            IsPrimary = true,
                            AbsenceMeansEnabled = true,
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
            ApplyWindows11Layout();
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

    private static void ApplyWindows11Layout()
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string start2BinPath = Path.Combine(userProfile, Win11StartBinPath);
        string tempPath = Path.GetTempPath();

        // Delete existing start2.bin if it exists
        if (File.Exists(start2BinPath))
        {
            File.Delete(start2BinPath);
        }

        // Create temp file with cert content
        string tempTxtPath = Path.Combine(tempPath, "start2.txt");
        string tempBinPath = Path.Combine(tempPath, "start2.bin");

        try
        {
            // Write certificate content to temp file
            File.WriteAllText(tempTxtPath, StartMenuLayouts.Windows11StartBinCertificate);

            // Use certutil to decode the certificate
            using (var process = new System.Diagnostics.Process())
            {
                process.StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "certutil.exe",
                    Arguments = $"-decode \"{tempTxtPath}\" \"{tempBinPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                };

                process.Start();
                process.WaitForExit();
            }

            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(start2BinPath)!);

            // Copy the decoded binary to the Start Menu location
            File.Copy(tempBinPath, start2BinPath, true);
        }
        finally
        {
            // Clean up temp files
            if (File.Exists(tempTxtPath))
                File.Delete(tempTxtPath);
            if (File.Exists(tempBinPath))
                File.Delete(tempBinPath);
        }
    }

    /// <summary>
    /// Cleans the Start Menu for Windows 10 or Windows 11.
    /// </summary>
    /// <param name="isWindows11">Whether the system is Windows 11.</param>
    /// <param name="windowsService">The system services.</param>
    public static void CleanStartMenu(bool isWindows11, ISystemServices windowsService)
    {
        if (isWindows11)
        {
            CleanWindows11StartMenu();
        }
        else
        {
            CleanWindows10StartMenu(windowsService);
        }
    }

    /// <summary>
    /// Cleans the Windows 11 Start Menu by replacing the start2.bin file.
    /// </summary>
    private static void CleanWindows11StartMenu()
    {
        // This is essentially the same as ApplyWindows11Layout since we're replacing the start2.bin file
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string start2BinPath = Path.Combine(userProfile, Win11StartBinPath);
        string tempPath = Path.GetTempPath();

        // Delete existing start2.bin if it exists
        if (File.Exists(start2BinPath))
        {
            File.Delete(start2BinPath);
        }

        // Create temp file with cert content
        string tempTxtPath = Path.Combine(tempPath, "start2.txt");
        string tempBinPath = Path.Combine(tempPath, "start2.bin");

        try
        {
            // Write certificate content to temp file
            File.WriteAllText(tempTxtPath, StartMenuLayouts.Windows11StartBinCertificate);

            // Use certutil to decode the certificate
            using (var process = new System.Diagnostics.Process())
            {
                process.StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "certutil.exe",
                    Arguments = $"-decode \"{tempTxtPath}\" \"{tempBinPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                };

                process.Start();
                process.WaitForExit();
            }

            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(start2BinPath)!);

            // Copy the decoded binary to the Start Menu location
            File.Copy(tempBinPath, start2BinPath, true);
        }
        finally
        {
            // Clean up temp files
            if (File.Exists(tempTxtPath))
                File.Delete(tempTxtPath);
            if (File.Exists(tempBinPath))
                File.Delete(tempBinPath);
        }
    }

    /// <summary>
    /// Cleans the Windows 10 Start Menu by creating and then removing a StartMenuLayout.xml file.
    /// </summary>
    /// <param name="windowsService">The system services.</param>
    private static void CleanWindows10StartMenu(ISystemServices windowsService)
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

            // Set registry values to lock the Start Menu layout
            using (
                var key = Registry.LocalMachine.CreateSubKey(
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

            using (
                var key = Registry.CurrentUser.CreateSubKey(
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

            // Use the improved RefreshWindowsGUI method to restart Explorer and apply changes
            // This will ensure Explorer is restarted properly with retry logic and fallback
            var result = windowsService.RefreshWindowsGUI(true).GetAwaiter().GetResult();
            if (!result)
            {
                throw new Exception(
                    "Failed to refresh Windows GUI after applying Start Menu layout"
                );
            }

            // Wait for changes to take effect
            System.Threading.Thread.Sleep(3000);

            // Disable the locked Start Menu layout
            using (
                var key = Registry.LocalMachine.CreateSubKey(
                    @"SOFTWARE\Policies\Microsoft\Windows\Explorer"
                )
            )
            {
                if (key != null)
                {
                    key.SetValue("LockedStartLayout", 0, RegistryValueKind.DWord);
                }
            }

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

            // Use the improved RefreshWindowsGUI method again to apply the final changes
            result = windowsService.RefreshWindowsGUI(true).GetAwaiter().GetResult();
            if (!result)
            {
                throw new Exception(
                    "Failed to refresh Windows GUI after unlocking Start Menu layout"
                );
            }

            // Delete the layout file
            if (File.Exists(Win10StartLayoutPath))
            {
                File.Delete(Win10StartLayoutPath);
            }
        }
        catch (Exception ex)
        {
            // Log the exception or handle it as needed
            System.Diagnostics.Debug.WriteLine(
                $"Error cleaning Windows 10 Start Menu: {ex.Message}"
            );
            throw new Exception($"Error cleaning Windows 10 Start Menu: {ex.Message}", ex);
        }
    }
}
