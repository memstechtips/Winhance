using Microsoft.Win32;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Constants;

namespace Winhance.Core.Features.Customize.Models;

public static class ExplorerCustomizations
{
    public static SettingGroup GetExplorerCustomizations()
    {
        return new SettingGroup
        {
            Name = "ExplorerCustomizations",
            FeatureId = FeatureIds.ExplorerCustomization,
            Settings = new List<SettingDefinition>
            {
                new SettingDefinition
                {
                    Id = "explorer-customization-shortcut-suffix",
                    Name = "Shortcut Naming",
                    Description = "Controls whether Windows appends '- Shortcut' text to newly created shortcut file names",
                    GroupName = "Desktop",
                    InputType = InputType.Selection,
                    Icon = "LinkVariant",
                    RestartProcess = "Explorer",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer",
                            ValueName = "link",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.Binary,
                            DefaultOption = "Keep '- Shortcut' suffix",
                        },
                    },
                    ComboBox = new ComboBoxMetadata
                    {
                        DisplayNames = new string[]
                        {
                            "Keep '- Shortcut' suffix",
                            "Remove '- Shortcut' suffix",
                        },
                        ValueMappings = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?>
                            {
                                ["link"] = null,
                            },
                            [1] = new Dictionary<string, object?>
                            {
                                ["link"] = new byte[] { 0x00, 0x00, 0x00, 0x00 },
                            },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-shortcut-arrow",
                    Name = "Shortcut Arrow Icon",
                    Description = "Controls the small arrow overlay on desktop shortcut icons",
                    GroupName = "Desktop",
                    InputType = InputType.Selection,
                    Icon = "ArrowTopLeftBoldOutline",
                    AddedInVersion = "26.03.26",
                    RestartProcess = "Explorer",
                    PowerShellScripts = new List<PowerShellScriptSetting>
                    {
                        new PowerShellScriptSetting
                        {
                            EnabledScript = @"
$icoPath = ""$env:SystemRoot\blank.ico""
if (-not (Test-Path $icoPath)) {
    $b64='AAABAAEAAAAAAAEAIAC5BwAAFgAAAIlQTkcNChoKAAAADUlIRFIAAAEAAAABAAgGAAAAXHKoZgAAB4BJREFUeNrt3eGSmzYAhVFnp+//xJlM67ZJ3Y0XkJBA0j1nJn+yDggEnzG2N98eQKxvdw8AuI8AQDABgGACAMEEAIIJAAQTAAgmABBMACCYAEAwAYBgAgDBBACCCQAEEwAIJgAQTAAgmABAMAGAYAIAwQQAggkABBMACCYAEEwAIJgAQDABgGACAMEEAIIJAAQTAAgmABBMACCYAEAwAYBgAgDBBACCJQXg48Bjftw9SLjSSgHYO8GPnNwfBY+F6c0cgM8nfMuT9qPx8mBIMwbgqmdpVwMsb7YA3PHM7GqAZc0UgDtPRBFgSTMEYJRLcRFgOSMHYJQT/3U8o4wFmhg1AKOebKOOC6qMGICRT7KRxwbFBGC98cFhowVghpNrhjHCIQKw9jhh00gBGO2u/95YZxgnbBKAc+OdZazw1igB+HkyzXRSzTRWeGuEAHw+kWY6sWYaK/xGANqOneuZgxPuDsC7yZtpQmca66rMwQkC0GcbuI79f4IA9NkGrmUOKglAn23gWuag0ogB2Pr7Ec001lWZg0oC0G8buJZ5qCAAfbeD65iDCgLQdzu4jjmoIAB9t4NrmYdCowZg72ejmWmsKzMPhQSg/3ZwHfNQ6O4AbJlpMmca68rMQyEByBvrysxDIQHIG+vqzEUBAcgb6+rMRYGRA/A0y2TOMs4E5qKAAGSNM4G5KCAAWeNMYC4KCEDGGJOYjwICsPb4UpmXgwRg7fGlMi8HCcCaY0tnbg4SgLXGxT/Mz0ECUDemx4Dj4j8jHjdDEoC5x8N75ukgATg+jscgY+GYUY6doQnA/vofN4+BOncfO1MQgDHXzXnm7wABeL/Oxw3rpS0BOEAA/r+ux4Xroy8BOGD0ADx97Py8dpI/L9fBsh4R2DF6AD7+/fN95zE/dn7+jgNjfQKwY4YAPO1N4tZVggMglwDsmCEANZNo0nkSgB2jB+CpZhLd0ONJAHasGoDXf/s48e+ZnwhsWD0ALZfBnMz9hhkC8CQC1DLvGwSA1Zn3DUkBaLkc5mHON6QFoPWyGJ/53pAYgB7LY2zm+wsCQALz/YVZAvAkAtQy119IDkCvZTIe8/yF9AD0XO679XzmoLyGAHxBAPove2v5DsxyryEt2Xf29RsC0H/5W8tNOShb/vKV2u93pOzrIgLQd/lHlrn6gfl5+858Qevnsmq/Ibryfq4iAP3WcXRZR36j0dHlvBrhYG/90kcAGhOAPusoPanPvkwoWd5Verz0aXH1wAsB6LOeVs/qPx/7eJSfTHcf8C0DUHvjb7R9MpyZAvA0w1XA0Wfsx8H1bD221Ul29YesagLgy2AdCEDb9ZRerp+NRYsTrcdvTbryCqjluHrvl+EIQNv19ArAuxtfLd5hKB1Lj32w9ZhH4/EJwCcC0G5dtQf+0ZO05mQteYlw1w3QksdcFYEz7zZMRQDarW/EAGw9tmYsLfZDi58/TozzjquiYQlAm/X1fFZ7d0COGoDak7vmLn+Pl2m9ojis2QLwNOLLgLOve0sv1R+F+6D15w5Kt/HMjcya9dWOccS3U7sSgDbr63nZ2+qjtEevHlqdkHvP6q1usrW4V9NznwxNANqs7+obX2ee+a4IwF7QHifWcXbMtTc/l4yAALRZX82l794yWl82l9w/6PFR3aP7oudbsO8eJwCTuWMiau9M3/WR15Ix1mzv1pg/vnhcz5t7tdvW6q3eaT8zIADt1tnqN/70PJhava3ZM1Sfl39E7Ul8xf2Doc0YgDtMPckNt3naZ7ovtqvFuwhTHxsCcMzUk3xyu1+tsA8E4IUAHDf1RPNLi4/5LvNpQQE4buqJ5pcWL2OW+a6AAJRZ4TVwupYfQGqxnFsJQJklJj3c9M/ajfx9LAsAaQTgZR8IAGR4+9kNAYC1bb5sFQBYS9FnNwQA5nX64+cCAPNo/slMAYAxtfpy2SYBgPtsfevxkrcqBQDaO/p15ts/jyAAUG7vBL/9xD5KAOBrZ3+70fAEgHS3vw6/kwCQIPok3yIArGT5S/bWBIDZeDZvSAAYzTJ32GcgAFzNCT4QAaA1J/hEBIBabrgtQADY4obb4gSAJ8/mj8cff/35fvcgriYAOTyb8xsBWI9ncw4TgPmt+P/3cREBgGACAMEEAIIJAAQTAAgmABBMACCYAEAwAYBgAgDBBACCCQAEEwAIJgAQTAAgmABAMAGAYAIAwQQAggkABBMACCYAEEwAIJgAQDABgGACAMEEAIIJAAQTAAgmABBMACCYAEAwAYBgAgDBBACCCQAEEwAIJgAQTAAgmABAMAGAYAIAwQQAggkABBMACCYAEEwAIJgAQDABgGACAMEEAIIJAAQTAAgmABBMACCYAEAwAYBgAgDBBACCCQAEEwAIJgAQTAAgmABAMAGAYAIAwQQAggkABBMACCYAEEwAIJgAQDABgGACAMEEAIIJAAQTAAgmABBMACCYAEAwAYBgAgDBBACCCQAEEwAIJgAQTAAgmABAMAGAYAIAwQQAggkABBMACCYAEEwAIJgAQDABgGACAMEEAIIJAAQTAAgmABBMACCYAEAwAYBgAgDB/gRG/ewS3uwoeAAAAABJRU5ErkJggg=='
    [IO.File]::WriteAllBytes($icoPath,[Convert]::FromBase64String($b64))
}",
                            // Don't delete blank.ico on disable — only the registry value is removed.
                            // Deleting the file invalidates the Windows icon cache, causing black
                            // squares on shortcut icons when the setting is re-enabled.
                            DisabledScript = null,
                            RequiresElevation = true,
                        },
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons",
                            ValueName = "29",
                            RecommendedValue = null,
                            DefaultValue = null,
                            ValueType = RegistryValueKind.String,
                            DefaultOption = "Show arrow icon",
                        },
                    },
                    ComboBox = new ComboBoxMetadata
                    {
                        DisplayNames = new string[]
                        {
                            "Show arrow icon",
                            "Remove arrow icon",
                        },
                        ValueMappings = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?>
                            {
                                ["29"] = null,
                            },
                            [1] = new Dictionary<string, object?>
                            {
                                ["29"] = @"C:\Windows\blank.ico",
                            },
                        },
                        ScriptMappings = new Dictionary<int, ScriptOption>
                        {
                            [0] = ScriptOption.Disabled,
                            [1] = ScriptOption.Enabled,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-context-menu",
                    Name = "Use Classic Context Menu",
                    Description = "Use the Windows 10-style right-click menu with all options visible instead of the simplified Windows 11 menu",
                    GroupName = "Context Menu",
                    InputType = InputType.Toggle,
                    IconPack = "Fluent",
                    Icon = "Navigation",
                    IsWindows11Only = true,
                    RestartProcess = "Explorer",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32",
                            ValueName = "",
                            RecommendedValue = "",
                            EnabledValue = [""],
                            DisabledValue = [null],
                            DefaultValue = "",
                            ValueType = RegistryValueKind.String,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-take-ownership",
                    Name = "Add 'Take Ownership' to Context Menu",
                    Description = "Adds a right-click option to take ownership of files, folders, and drives with automatic permission elevation. May require temporarily disabling Windows Defender for protected files",
                    GroupName = "Context Menu",
                    InputType = InputType.Toggle,
                    Icon = "Security",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CLASSES_ROOT\*\shell\TakeOwnership",
                            ValueName = "",
                            EnabledValue = ["Take Ownership"],
                            DisabledValue = [null],
                            DefaultValue = null,
                            RecommendedValue = "Take Ownership",
                            ValueType = RegistryValueKind.String,
                        }
                    },
                    RegContents = new List<RegContentSetting>
                    {
                        new RegContentSetting
                        {
                            EnabledContent = @"Windows Registry Editor Version 5.00

; Created by: Shawn Brink
; Created on: January 28, 2015
; Updated on: February 25, 2024
; Tutorial: https://www.tenforums.com/tutorials/3841-add-take-ownership-context-menu-windows-10-a.html

[-HKEY_CLASSES_ROOT\*\shell\TakeOwnership]
[-HKEY_CLASSES_ROOT\*\shell\runas]

[HKEY_CLASSES_ROOT\*\shell\TakeOwnership]
@=""Take Ownership""
""Extended""=-
""HasLUAShield""=""""
""NoWorkingDirectory""=""""
""NeverDefault""=""""

[HKEY_CLASSES_ROOT\*\shell\TakeOwnership\command]
@=""powershell -windowstyle hidden -command \""Start-Process cmd -ArgumentList '/c takeown /f \\\""%1\\\"" && icacls \\\""%1\\\"" /grant *S-1-3-4:F /t /c /l & pause' -Verb runAs\""""
""IsolatedCommand""=""powershell -windowstyle hidden -command \""Start-Process cmd -ArgumentList '/c takeown /f \\\""%1\\\"" && icacls \\\""%1\\\"" /grant *S-1-3-4:F /t /c /l & pause' -Verb runAs\""""

[HKEY_CLASSES_ROOT\Directory\shell\TakeOwnership]
@=""Take Ownership""
""AppliesTo""=""NOT (System.ItemPathDisplay:=\""C:\\Users\"" OR System.ItemPathDisplay:=\""C:\\ProgramData\"" OR System.ItemPathDisplay:=\""C:\\Windows\"" OR System.ItemPathDisplay:=\""C:\\Windows\\System32\"" OR System.ItemPathDisplay:=\""C:\\Program Files\"" OR System.ItemPathDisplay:=\""C:\\Program Files (x86)\"")""
""Extended""=-
""HasLUAShield""=""""
""NoWorkingDirectory""=""""
""Position""=""middle""

[HKEY_CLASSES_ROOT\Directory\shell\TakeOwnership\command]
@=""powershell -windowstyle hidden -command \""$Y = ($null | choice).Substring(1,1); Start-Process cmd -ArgumentList ('/c takeown /f \\\""%1\\\"" /r /d ' + $Y + ' && icacls \\\""%1\\\"" /grant *S-1-3-4:F /t /c /l /q & pause') -Verb runAs\""""
""IsolatedCommand""=""powershell -windowstyle hidden -command \""$Y = ($null | choice).Substring(1,1); Start-Process cmd -ArgumentList ('/c takeown /f \\\""%1\\\"" /r /d ' + $Y + ' && icacls \\\""%1\\\"" /grant *S-1-3-4:F /t /c /l /q & pause') -Verb runAs\""""

[HKEY_CLASSES_ROOT\Drive\shell\runas]
@=""Take Ownership""
""Extended""=-
""HasLUAShield""=""""
""NoWorkingDirectory""=""""
""Position""=""middle""
""AppliesTo""=""NOT (System.ItemPathDisplay:=\""C:\\\"")""

[HKEY_CLASSES_ROOT\Drive\shell\runas\command]
@=""cmd.exe /c takeown /f \""%1\\\"" /r /d y && icacls \""%1\\\"" /grant *S-1-3-4:F /t /c & Pause""
""IsolatedCommand""=""cmd.exe /c takeown /f \""%1\\\"" /r /d y && icacls \""%1\\\"" /grant *S-1-3-4:F /t /c & Pause""
",
                            DisabledContent = @"Windows Registry Editor Version 5.00

[-HKEY_CLASSES_ROOT\*\shell\TakeOwnership]
[-HKEY_CLASSES_ROOT\*\shell\runas]
[-HKEY_CLASSES_ROOT\Directory\shell\TakeOwnership]
[-HKEY_CLASSES_ROOT\Drive\shell\runas]
",
                            RequiresElevation = true
                        }
                    }
                },
                new SettingDefinition
                {
                    Id = "explorer-context-menu-toggle-extensions",
                    Name = "Add 'Show/Hide Extensions' to Context Menu",
                    Description = "Adds a right-click menu option to quickly toggle file extension visibility in File Explorer (only visible on the Classic Context Menu or Show More Options Menu in Windows 11)",
                    GroupName = "Context Menu",
                    InputType = InputType.Toggle,
                    IconPack = "Fluent",
                    Icon = "DocumentQuestionMark",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CLASSES_ROOT\AllFilesystemObjects\shell\Windows.ShowFileExtensions",
                            ValueName = "ExplorerCommandHandler",
                            EnabledValue = ["{4ac6c205-2853-4bf5-b47c-919a42a48a16}"],
                            DisabledValue = [null],
                            DefaultValue = null,
                            RecommendedValue = null,
                            ValueType = RegistryValueKind.String,
                        }
                    },
                    RegContents = new List<RegContentSetting>
                    {
                        new RegContentSetting
                        {
                            EnabledContent = @"Windows Registry Editor Version 5.00

[HKEY_CLASSES_ROOT\AllFilesystemObjects\shell\Windows.ShowFileExtensions]
""CommandStateSync""=""""
""Description""=""@shell32.dll,-37571""
""ExplorerCommandHandler""=""{4ac6c205-2853-4bf5-b47c-919a42a48a16}""
""MUIVerb""=""@shell32.dll,-37570""

[HKEY_CLASSES_ROOT\Directory\Background\shell\Windows.ShowFileExtensions]
""CommandStateSync""=""""
""Description""=""@shell32.dll,-37571""
""ExplorerCommandHandler""=""{4ac6c205-2853-4bf5-b47c-919a42a48a16}""
""MUIVerb""=""@shell32.dll,-37570""
",
                            DisabledContent = @"Windows Registry Editor Version 5.00

[-HKEY_CLASSES_ROOT\AllFilesystemObjects\shell\Windows.ShowFileExtensions]
[-HKEY_CLASSES_ROOT\Directory\Background\shell\Windows.ShowFileExtensions]
",
                            RequiresElevation = true
                        }
                    },
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresEnabled,
                            DependentSettingId = "explorer-context-menu-toggle-extensions",
                            RequiredSettingId = "explorer-customization-context-menu",
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-context-menu-windows-terminal",
                    Name = "Show 'Open in Windows Terminal' in Context Menu",
                    Description = "Displays the Windows Terminal option when right-clicking folders and backgrounds in File Explorer",
                    GroupName = "Context Menu",
                    InputType = InputType.Toggle,
                    Icon = "Console",
                    IsWindows11Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked",
                            ValueName = "{9F156763-7844-4DC4-B2B1-901F640F5155}",
                            RecommendedValue = null,
                            EnabledValue = [null],
                            DisabledValue = [""],
                            ValueType = RegistryValueKind.String,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-context-menu-sfc",
                    Name = "Add 'SFC /SCANNOW' to Context Menu",
                    Description = "Adds right-click options to run System File Checker (SFC /SCANNOW) and view scan details from the desktop or folder background",
                    GroupName = "Context Menu",
                    InputType = InputType.Toggle,
                    Icon = "MagnifyScan",
                    AddedInVersion = "25.04.09",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CLASSES_ROOT\Directory\Background\shell\SFC",
                            ValueName = "MUIVerb",
                            EnabledValue = ["SFC /SCANNOW"],
                            DisabledValue = [null],
                            DefaultValue = null,
                            RecommendedValue = null,
                            ValueType = RegistryValueKind.String,
                        }
                    },
                    RegContents = new List<RegContentSetting>
                    {
                        new RegContentSetting
                        {
                            EnabledContent = @"Windows Registry Editor Version 5.00

; Created by: Shawn Brink
; Created on: March 12, 2020
; Tutorial: https://www.tenforums.com/tutorials/152128-how-add-sfc-scannow-context-menu-windows-10-a.html

[HKEY_CLASSES_ROOT\Directory\Background\shell\SFC]
""Icon""=""WmiPrvSE.exe""
""MUIVerb""=""SFC /SCANNOW""
""Position""=""Bottom""
""Extended""=-
""SubCommands""=""""

[HKEY_CLASSES_ROOT\Directory\Background\shell\SFC\shell\001menu]
""HasLUAShield""=""""
""MUIVerb""=""Run SFC /SCANNOW""

[HKEY_CLASSES_ROOT\Directory\Background\shell\SFC\shell\001menu\command]
@=""PowerShell -ExecutionPolicy Bypass -windowstyle hidden -command \""Start-Process cmd -ArgumentList '/s,/k, sfc /scannow' -Verb runAs\""""

[HKEY_CLASSES_ROOT\Directory\Background\shell\SFC\shell\002menu]
""MUIVerb""=""SFC scan details log""

[HKEY_CLASSES_ROOT\Directory\Background\shell\SFC\shell\002menu\command]
@=""PowerShell -ExecutionPolicy Bypass (sls [SR] $env:windir\\Logs\\CBS\\CBS.log -s).Line >\""$env:userprofile\\Desktop\\sfcdetails.txt\""""
",
                            DisabledContent = @"Windows Registry Editor Version 5.00

[-HKEY_CLASSES_ROOT\Directory\Background\shell\SFC]
",
                            RequiresElevation = true
                        }
                    },
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresEnabled,
                            DependentSettingId = "explorer-context-menu-sfc",
                            RequiredSettingId = "explorer-customization-context-menu",
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-context-menu-dism",
                    Name = "Add 'Repair Windows Image' to Context Menu",
                    Description = "Adds a right-click option to run DISM /RestoreHealth to repair the Windows system image from the desktop or folder background",
                    GroupName = "Context Menu",
                    InputType = InputType.Toggle,
                    Icon = "MedicalBag",
                    AddedInVersion = "25.04.09",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CLASSES_ROOT\Directory\Background\shell\RepairWindowsImage",
                            ValueName = "MUIVerb",
                            EnabledValue = ["Repair Windows Image"],
                            DisabledValue = [null],
                            DefaultValue = null,
                            RecommendedValue = null,
                            ValueType = RegistryValueKind.String,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CLASSES_ROOT\Directory\Background\shell\RepairWindowsImage",
                            ValueName = "Icon",
                            EnabledValue = ["WmiPrvSE.exe"],
                            DisabledValue = [null],
                            DefaultValue = null,
                            RecommendedValue = null,
                            ValueType = RegistryValueKind.String,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CLASSES_ROOT\Directory\Background\shell\RepairWindowsImage",
                            ValueName = "HasLUAShield",
                            EnabledValue = [""],
                            DisabledValue = [null],
                            DefaultValue = null,
                            RecommendedValue = null,
                            ValueType = RegistryValueKind.String,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CLASSES_ROOT\Directory\Background\shell\RepairWindowsImage\command",
                            ValueName = "",
                            EnabledValue = ["PowerShell -ExecutionPolicy Bypass -windowstyle hidden -command \"Start-Process cmd -ArgumentList '/s,/k, DISM /Online /Cleanup-Image /RestoreHealth' -Verb runAs\""],
                            DisabledValue = [null],
                            DefaultValue = null,
                            RecommendedValue = null,
                            ValueType = RegistryValueKind.String,
                        },
                    },
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresEnabled,
                            DependentSettingId = "explorer-context-menu-dism",
                            RequiredSettingId = "explorer-customization-context-menu",
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-context-menu-chkdsk",
                    Name = "Add 'CHKDSK' to Context Menu",
                    Description = "Adds right-click options to run CHKDSK from the desktop or folder background with a prompt to select the drive letter",
                    GroupName = "Context Menu",
                    InputType = InputType.Toggle,
                    Icon = "Harddisk",
                    AddedInVersion = "25.04.09",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CLASSES_ROOT\Directory\Background\shell\CHKDSK",
                            ValueName = "MUIVerb",
                            EnabledValue = ["CHKDSK"],
                            DisabledValue = [null],
                            DefaultValue = null,
                            RecommendedValue = null,
                            ValueType = RegistryValueKind.String,
                        }
                    },
                    RegContents = new List<RegContentSetting>
                    {
                        new RegContentSetting
                        {
                            EnabledContent = @"Windows Registry Editor Version 5.00

[HKEY_CLASSES_ROOT\Directory\Background\shell\CHKDSK]
""Icon""=""imageres.dll,-36""
""MUIVerb""=""CHKDSK""
""Position""=""Bottom""
""SubCommands""=""""

[HKEY_CLASSES_ROOT\Directory\Background\shell\CHKDSK\shell\001menu]
""HasLUAShield""=""""
""MUIVerb""=""Run CHKDSK (scan only)""

[HKEY_CLASSES_ROOT\Directory\Background\shell\CHKDSK\shell\001menu\command]
@=""PowerShell -ExecutionPolicy Bypass -windowstyle hidden -command \""Start-Process cmd -ArgumentList '/v:on,/s,/k, set /p d=Enter drive letter (e.g. C): & chkdsk !d!:' -Verb runAs\""""

[HKEY_CLASSES_ROOT\Directory\Background\shell\CHKDSK\shell\002menu]
""HasLUAShield""=""""
""MUIVerb""=""Run CHKDSK /F (fix errors)""

[HKEY_CLASSES_ROOT\Directory\Background\shell\CHKDSK\shell\002menu\command]
@=""PowerShell -ExecutionPolicy Bypass -windowstyle hidden -command \""Start-Process cmd -ArgumentList '/v:on,/s,/k, set /p d=Enter drive letter (e.g. C): & chkdsk !d!: /f' -Verb runAs\""""

[HKEY_CLASSES_ROOT\Directory\Background\shell\CHKDSK\shell\003menu]
""HasLUAShield""=""""
""MUIVerb""=""Run CHKDSK /R (locate bad sectors)""

[HKEY_CLASSES_ROOT\Directory\Background\shell\CHKDSK\shell\003menu\command]
@=""PowerShell -ExecutionPolicy Bypass -windowstyle hidden -command \""Start-Process cmd -ArgumentList '/v:on,/s,/k, set /p d=Enter drive letter (e.g. C): & chkdsk !d!: /r' -Verb runAs\""""
",
                            DisabledContent = @"Windows Registry Editor Version 5.00

[-HKEY_CLASSES_ROOT\Directory\Background\shell\CHKDSK]
",
                            RequiresElevation = true
                        }
                    },
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresEnabled,
                            DependentSettingId = "explorer-context-menu-chkdsk",
                            RequiredSettingId = "explorer-customization-context-menu",
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-context-menu-ps1-edit-run",
                    Name = "Add 'Edit or Run with' to PS1 Context Menu",
                    Description = "Adds a right-click cascading menu to .ps1 files with options to run or edit with PowerShell, PowerShell 7, PowerShell ISE, and Notepad (including as administrator). PowerShell 7 must be installed separately for the PowerShell 7 options to work",
                    GroupName = "Context Menu",
                    InputType = InputType.Toggle,
                    Icon = "Powershell",
                    AddedInVersion = "25.04.09",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CLASSES_ROOT\SystemFileAssociations\.ps1\Shell\Edit-Run-with",
                            ValueName = "MUIVerb",
                            EnabledValue = ["Edit or Run with"],
                            DisabledValue = [null],
                            DefaultValue = null,
                            RecommendedValue = null,
                            ValueType = RegistryValueKind.String,
                        }
                    },
                    RegContents = new List<RegContentSetting>
                    {
                        new RegContentSetting
                        {
                            EnabledContent = @"Windows Registry Editor Version 5.00

; Created by: Shawn Brink
; Created on: December 4, 2023
; Tutorial: https://www.elevenforum.com/t/add-edit-or-run-with-to-ps1-file-context-menu-in-windows-11.20366/

[HKEY_CLASSES_ROOT\SystemFileAssociations\.ps1\Shell\Edit-Run-with]
""MUIVerb""=""Edit or Run with""
""SubCommands""=""""

[HKEY_CLASSES_ROOT\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\001flyout]
""MUIVerb""=""Run with PowerShell""
""Icon""=""powershell.exe""

[HKEY_CLASSES_ROOT\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\001flyout\Command]
@=""\""C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe\"" \""-Command\"" \""if((Get-ExecutionPolicy ) -ne 'AllSigned') { Set-ExecutionPolicy -Scope Process Bypass }; & '%1'\""""

[HKEY_CLASSES_ROOT\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\002flyout]
""MUIVerb""=""Run with PowerShell as administrator""
""HasLUAShield""=""""
""Icon""=""powershell.exe""

[HKEY_CLASSES_ROOT\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\002flyout\Command]
@=""\""C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe\"" \""-Command\"" \""\""& {Start-Process PowerShell.exe -ArgumentList '-ExecutionPolicy RemoteSigned -File \\\\""%1\\\\""' -Verb RunAs}\""""

[HKEY_CLASSES_ROOT\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\003flyout]
""MUIVerb""=""Run with PowerShell 7""
""Icon""=""pwsh.exe""
""CommandFlags""=dword:00000020

[HKEY_CLASSES_ROOT\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\003flyout\Command]
@=""\""C:\\Program Files\\PowerShell\\7\\pwsh.exe\"" \""-Command\"" \""if((Get-ExecutionPolicy ) -ne 'AllSigned') { Set-ExecutionPolicy -Scope Process Bypass }; & '%1'\""""

[HKEY_CLASSES_ROOT\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\004flyout]
""MUIVerb""=""Run with PowerShell 7 as administrator""
""HasLUAShield""=""""
""Icon""=""pwsh.exe""

[HKEY_CLASSES_ROOT\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\004flyout\Command]
@=""\""C:\\Program Files\\PowerShell\\7\\pwsh.exe\"" \""-Command\"" \""\""& {Start-Process pwsh.exe -ArgumentList '-ExecutionPolicy RemoteSigned -File \\\\""%1\\\\""' -Verb RunAs}\""""

[HKEY_CLASSES_ROOT\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\005flyout]
""MUIVerb""=""Edit with PowerShell ISE""
""Icon""=""powershell_ise.exe""
""CommandFlags""=dword:00000020

[HKEY_CLASSES_ROOT\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\005flyout\Command]
@=""\""C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell_ise.exe\"" \""%1\""""

[HKEY_CLASSES_ROOT\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\006flyout]
""MUIVerb""=""Edit with PowerShell ISE as administrator""
""HasLUAShield""=""""
""Icon""=""powershell_ise.exe""

[HKEY_CLASSES_ROOT\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\006flyout\Command]
@=""PowerShell -windowstyle hidden -Command \""Start-Process cmd -ArgumentList '/s,/c,start PowerShell_ISE.exe \\""%1\\""'  -Verb RunAs\""""

[HKEY_CLASSES_ROOT\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\007flyout]
""MUIVerb""=""Edit with PowerShell ISE (x86)""
""Icon""=""powershell_ise.exe""
""CommandFlags""=dword:00000020

[HKEY_CLASSES_ROOT\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\007flyout\Command]
@=""\""C:\\WINDOWS\\syswow64\\WindowsPowerShell\\v1.0\\powershell_ise.exe\"" \""%1\""""

[HKEY_CLASSES_ROOT\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\008flyout]
""MUIVerb""=""Edit with PowerShell ISE (x86) as administrator""
""HasLUAShield""=""""
""Icon""=""powershell_ise.exe""

[HKEY_CLASSES_ROOT\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\008flyout\Command]
@=""PowerShell -windowstyle hidden -Command \""Start-Process cmd -ArgumentList '/s,/c,start C:\\WINDOWS\\syswow64\\WindowsPowerShell\\v1.0\\powershell_ise.exe \\""%1\\""'  -Verb RunAs\""""

[HKEY_CLASSES_ROOT\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\009flyout]
""MUIVerb""=""Edit with Notepad""
""Icon""=""notepad.exe""
""CommandFlags""=dword:00000020

[HKEY_CLASSES_ROOT\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\009flyout\Command]
@=""\""C:\\Windows\\System32\\notepad.exe\"" \""%1\""""

[HKEY_CLASSES_ROOT\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\010flyout]
""MUIVerb""=""Edit with Notepad as administrator""
""HasLUAShield""=""""
""Icon""=""notepad.exe""

[HKEY_CLASSES_ROOT\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\010flyout\Command]
@=""PowerShell -windowstyle hidden -Command \""Start-Process cmd -ArgumentList '/s,/c,start C:\\Windows\\System32\\notepad.exe \\""%1\\""'  -Verb RunAs\""""
",
                            DisabledContent = @"Windows Registry Editor Version 5.00

[-HKEY_CLASSES_ROOT\SystemFileAssociations\.ps1\Shell\Edit-Run-with]
",
                            RequiresElevation = true
                        }
                    },
                    Dependencies = new List<SettingDependency>
                    {
                        new SettingDependency
                        {
                            DependencyType = SettingDependencyType.RequiresEnabled,
                            DependentSettingId = "explorer-context-menu-ps1-edit-run",
                            RequiredSettingId = "explorer-customization-context-menu",
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "devices-dynamic-lighting-ambient",
                    Name = "Use Dynamic Lighting on my devices",
                    Description = "Allow Windows Dynamic Lighting to control ambient RGB effects on compatible devices",
                    GroupName = "Devices and Peripherals",
                    InputType = InputType.Toggle,
                    Icon = "TelevisionAmbientLight",
                    IsWindows11Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Lighting",
                            ValueName = "AmbientLightingEnabled",
                            RecommendedValue = 0,
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "devices-dynamic-lighting-foreground-app",
                    Name = "Compatible apps in the foreground always control lighting",
                    Description = "Allow compatible apps to control device lighting effects",
                    GroupName = "Devices and Peripherals",
                    InputType = InputType.Toggle,
                    Icon = "StringLightsOff",
                    IsWindows11Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Lighting",
                            ValueName = "ControlledByForegroundApp",
                            RecommendedValue = 0,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "devices-default-printer-management",
                    Name = "Automatic Default Printer Management",
                    Description = "Let Windows automatically set your default printer based on your location or last used printer",
                    GroupName = "Devices and Peripherals",
                    InputType = InputType.Toggle,
                    Icon = "PrinterOff",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows NT\CurrentVersion\Windows",
                            ValueName = "LegacyDefaultPrinterMode",
                            RecommendedValue = 0,
                            EnabledValue = [0],
                            DisabledValue = [1],
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-launch-to",
                    Name = "Open File Explorer to",
                    Description = "Choose what happens when File Explorer is opened",
                    GroupName = "General",
                    InputType = InputType.Selection,
                    IconPack = "Fluent",
                    Icon = "FolderOpen",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "LaunchTo",
                            RecommendedValue = 1,
                            DefaultValue = 2,
                            ValueType = RegistryValueKind.DWord,
                            DefaultOption = "Home",
                        },
                    },
                    ComboBox = new ComboBoxMetadata
                    {
                        DisplayNames = new string[]
                        {
                            "Home",
                            "This PC",
                            "Downloads",
                            "OneDrive (If Available)",
                        },
                        ValueMappings = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?>
                            {
                                ["LaunchTo"] = 2,
                            },
                            [1] = new Dictionary<string, object?>
                            {
                                ["LaunchTo"] = 1,
                            },
                            [2] = new Dictionary<string, object?>
                            {
                                ["LaunchTo"] = 3,
                            },
                            [3] = new Dictionary<string, object?>
                            {
                                ["LaunchTo"] = 4,
                            },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-browse-folders",
                    Name = "Browse folders",
                    Description = "Choose whether each folder opens in the same window or in its own window",
                    GroupName = "General",
                    InputType = InputType.Selection,
                    IconPack = "Fluent",
                    Icon = "FolderList",
                    RestartProcess = "Explorer",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\CabinetState",
                            ValueName = "Settings",
                            RecommendedValue = 0,
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.Binary,
                            BinaryByteIndex = 4,
                            BitMask = 0x20,
                        },
                    },
                    ComboBox = new ComboBoxMetadata
                    {
                        DisplayNames = new string[]
                        {
                            "Open each folder in the same window",
                            "Open each folder in its own window",
                        },
                        ValueMappings = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?>
                            {
                                ["Settings"] = 0,
                            },
                            [1] = new Dictionary<string, object?>
                            {
                                ["Settings"] = 1,
                            },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-click-items",
                    Name = "Click items as follows",
                    Description = "Choose whether to open files and folders with a single click (like web links) or double-click (traditional)",
                    GroupName = "General",
                    InputType = InputType.Selection,
                    IconPack = "Fluent",
                    Icon = "CursorClick",
                    RestartProcess = "Explorer",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer",
                            ValueName = "ShellState",
                            ValueType = RegistryValueKind.Binary,
                            BinaryByteIndex = 4,
                            BitMask = 0x20,
                            RecommendedValue = 1,
                            DefaultValue = 1,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer",
                            ValueName = "IconUnderline",
                            ValueType = RegistryValueKind.DWord,
                            RecommendedValue = 3,
                            DefaultValue = 3,
                        },
                    },
                    ComboBox = new ComboBoxMetadata
                    {
                        DisplayNames = new string[]
                        {
                            "Double-click to open an item (single-click to select)",
                            "Single-click to open (underline icon titles consistent with browser)",
                            "Single-click to open (underline icon titles only when pointing)",
                        },
                        ValueMappings = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?>
                            {
                                ["ShellState"] = 1,
                                ["IconUnderline"] = 3,
                            },
                            [1] = new Dictionary<string, object?>
                            {
                                ["ShellState"] = 0,
                                ["IconUnderline"] = 3,
                            },
                            [2] = new Dictionary<string, object?>
                            {
                                ["ShellState"] = 0,
                                ["IconUnderline"] = 2,
                            },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-show-recent-files",
                    Name = "Show recently used files",
                    Description = "Displays recently accessed files and recommendations in Quick Access",
                    GroupName = "General",
                    InputType = InputType.Toggle,
                    IconPack = "Fluent",
                    Icon = "DocumentTextClock",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer",
                            ValueName = "ShowRecent",
                            RecommendedValue = 0,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer",
                            ValueName = "ShowRecommendations",
                            RecommendedValue = 0,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-show-frequent-folders",
                    Name = "Show frequently used folders",
                    Description = "Displays your most accessed folders in Quick Access section",
                    GroupName = "General",
                    InputType = InputType.Toggle,
                    Icon = "FolderClockOutline",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer",
                            ValueName = "ShowFrequent",
                            RecommendedValue = 0,
                            EnabledValue = [1, null],
                            DisabledValue = [0],
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-show-office-files",
                    Name = "Show files from Office.com",
                    Description = "Displays cloud files from your Office.com account in Quick Access",
                    GroupName = "General",
                    InputType = InputType.Toggle,
                    Icon = "FileCloud",
                    IsWindows11Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer",
                            ValueName = "ShowCloudFilesInQuickAccess",
                            RecommendedValue = 0,
                            EnabledValue = [null],
                            DisabledValue = [0],
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-show-thumbnails",
                    Name = "Always show icons, never thumbnails",
                    Description = "Displays generic file icons instead of image/document previews",
                    GroupName = "Files and Folders",
                    InputType = InputType.Toggle,
                    Icon = "ImageOff",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "IconsOnly",
                            RecommendedValue = 0,
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-show-menus",
                    Name = "Always show menus",
                    Description = "Shows the Menu bar (File, Edit etc.) on all windows that support it",
                    GroupName = "Files and Folders",
                    InputType = InputType.Toggle,
                    IconPack = "Fluent",
                    Icon = "WindowApps",
                    IsWindows10Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "AlwaysShowMenus",
                            RecommendedValue = 0,
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-item-space",
                    Name = "Decrease space between items (compact view)",
                    Description = "Reduces vertical spacing between files and folders for denser view",
                    GroupName = "Files and Folders",
                    InputType = InputType.Toggle,
                    Icon = "ViewCompact",
                    IsWindows11Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "UseCompactMode",
                            RecommendedValue = 0,
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-icon-thumbnails",
                    Name = "Display file icon on thumbnails",
                    Description = "Shows file type icon overlay on bottom-right corner of thumbnail previews",
                    GroupName = "Files and Folders",
                    InputType = InputType.Toggle,
                    IconPack = "Fluent",
                    Icon = "DocumentImage",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ShowTypeOverlay",
                            RecommendedValue = 1,
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-folder-tips",
                    Name = "Display file size information in folder tips",
                    Description = "Shows total size and file count when hovering over folders",
                    GroupName = "Files and Folders",
                    InputType = InputType.Toggle,
                    IconPack = "Fluent",
                    Icon = "DocumentEndnote",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "FolderContentsInfoTip",
                            RecommendedValue = null,
                            EnabledValue = [null],
                            DisabledValue = [0],
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-full-path",
                    Name = "Display the full path in the title bar",
                    Description = "Shows complete directory path in window title instead of folder name only",
                    GroupName = "Files and Folders",
                    InputType = InputType.Toggle,
                    IconPack = "Fluent",
                    Icon = "PanelTopExpand",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\CabinetState",
                            ValueName = "FullPath",
                            RecommendedValue = 1,
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-show-hidden-files",
                    Name = "Show hidden files, folders & drives",
                    Description = "Displays items with the hidden attribute set",
                    GroupName = "Files and Folders",
                    InputType = InputType.Toggle,
                    Icon = "FileEyeOutline",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "Hidden",
                            RecommendedValue = 1,
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-hide-empty-drives",
                    Name = "Hide empty drives",
                    Description = "Hides drives with no media inserted like empty card readers or optical drives",
                    GroupName = "Files and Folders",
                    InputType = InputType.Toggle,
                    Icon = "HarddiskRemove",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "HideDrivesWithNoMedia",
                            RecommendedValue = null,
                            EnabledValue = [null],
                            DisabledValue = [0],
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-show-file-ext",
                    Name = "Show file extensions",
                    Description = "Displays file type extensions (like .txt, .pdf) after file names",
                    GroupName = "Files and Folders",
                    InputType = InputType.Toggle,
                    IconPack = "Fluent",
                    Icon = "DocumentQuestionMark",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "HideFileExt",
                            RecommendedValue = 0,
                            EnabledValue = [0],
                            DisabledValue = [1],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-enable-photo-viewer",
                    Name = "Enable Windows Photo Viewer",
                    Description = "Restore the legacy Windows Photo Viewer and set it as the default program for common image file formats",
                    GroupName = "File Associations",
                    InputType = InputType.Toggle,
                    Icon = "ImageOutline",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Classes\.bmp",
                            ValueName = "",
                            EnabledValue = ["PhotoViewer.FileAssoc.Tiff"],
                            DisabledValue = [null],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.String,
                        }
                    },
                    RegContents = new List<RegContentSetting>
                    {
                        new RegContentSetting
                        {
                            EnabledContent = @"Windows Registry Editor Version 5.00

[HKEY_CURRENT_USER\SOFTWARE\Classes\.bmp]
@=""PhotoViewer.FileAssoc.Tiff""

[HKEY_CURRENT_USER\SOFTWARE\Classes\.cr2]
@=""PhotoViewer.FileAssoc.Tiff""

[HKEY_CURRENT_USER\SOFTWARE\Classes\.dib]
@=""PhotoViewer.FileAssoc.Tiff""

[HKEY_CURRENT_USER\SOFTWARE\Classes\.gif]
@=""PhotoViewer.FileAssoc.Tiff""

[HKEY_CURRENT_USER\SOFTWARE\Classes\.ico]
@=""PhotoViewer.FileAssoc.Tiff""

[HKEY_CURRENT_USER\SOFTWARE\Classes\.jfif]
@=""PhotoViewer.FileAssoc.Tiff""

[HKEY_CURRENT_USER\SOFTWARE\Classes\.jpe]
@=""PhotoViewer.FileAssoc.Tiff""

[HKEY_CURRENT_USER\SOFTWARE\Classes\.jpeg]
@=""PhotoViewer.FileAssoc.Tiff""

[HKEY_CURRENT_USER\SOFTWARE\Classes\.jpg]
@=""PhotoViewer.FileAssoc.Tiff""

[HKEY_CURRENT_USER\SOFTWARE\Classes\.jxr]
@=""PhotoViewer.FileAssoc.Tiff""

[HKEY_CURRENT_USER\SOFTWARE\Classes\.png]
@=""PhotoViewer.FileAssoc.Tiff""

[HKEY_CURRENT_USER\SOFTWARE\Classes\.tif]
@=""PhotoViewer.FileAssoc.Tiff""

[HKEY_CURRENT_USER\SOFTWARE\Classes\.tiff]
@=""PhotoViewer.FileAssoc.Tiff""

[HKEY_CURRENT_USER\SOFTWARE\Classes\.wdp]
@=""PhotoViewer.FileAssoc.Tiff""

[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.bmp\OpenWithProgids]
""PhotoViewer.FileAssoc.Tiff""=hex(0):

[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.cr2\OpenWithProgids]
""PhotoViewer.FileAssoc.Tiff""=hex(0):

[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.dib\OpenWithProgids]
""PhotoViewer.FileAssoc.Tiff""=hex(0):

[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.gif\OpenWithProgids]
""PhotoViewer.FileAssoc.Tiff""=hex(0):

[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.ico\OpenWithProgids]
""PhotoViewer.FileAssoc.Tiff""=hex(0):

[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.jfif\OpenWithProgids]
""PhotoViewer.FileAssoc.Tiff""=hex(0):

[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.jpe\OpenWithProgids]
""PhotoViewer.FileAssoc.Tiff""=hex(0):

[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.jpeg\OpenWithProgids]
""PhotoViewer.FileAssoc.Tiff""=hex(0):

[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.jpg\OpenWithProgids]
""PhotoViewer.FileAssoc.Tiff""=hex(0):

[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.jxr\OpenWithProgids]
""PhotoViewer.FileAssoc.Tiff""=hex(0):

[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.png\OpenWithProgids]
""PhotoViewer.FileAssoc.Tiff""=hex(0):

[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.tif\OpenWithProgids]
""PhotoViewer.FileAssoc.Tiff""=hex(0):

[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.tiff\OpenWithProgids]
""PhotoViewer.FileAssoc.Tiff""=hex(0):

[HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.wdp\OpenWithProgids]
""PhotoViewer.FileAssoc.Tiff""=hex(0):
",
                            DisabledContent = @"Windows Registry Editor Version 5.00

[-HKEY_CURRENT_USER\SOFTWARE\Classes\.bmp]
[-HKEY_CURRENT_USER\SOFTWARE\Classes\.cr2]
[-HKEY_CURRENT_USER\SOFTWARE\Classes\.dib]
[-HKEY_CURRENT_USER\SOFTWARE\Classes\.gif]
[-HKEY_CURRENT_USER\SOFTWARE\Classes\.ico]
[-HKEY_CURRENT_USER\SOFTWARE\Classes\.jfif]
[-HKEY_CURRENT_USER\SOFTWARE\Classes\.jpe]
[-HKEY_CURRENT_USER\SOFTWARE\Classes\.jpeg]
[-HKEY_CURRENT_USER\SOFTWARE\Classes\.jpg]
[-HKEY_CURRENT_USER\SOFTWARE\Classes\.jxr]
[-HKEY_CURRENT_USER\SOFTWARE\Classes\.png]
[-HKEY_CURRENT_USER\SOFTWARE\Classes\.tif]
[-HKEY_CURRENT_USER\SOFTWARE\Classes\.tiff]
[-HKEY_CURRENT_USER\SOFTWARE\Classes\.wdp]

[-HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.bmp\OpenWithProgids]
[-HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.cr2\OpenWithProgids]
[-HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.dib\OpenWithProgids]
[-HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.gif\OpenWithProgids]
[-HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.ico\OpenWithProgids]
[-HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.jfif\OpenWithProgids]
[-HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.jpe\OpenWithProgids]
[-HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.jpeg\OpenWithProgids]
[-HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.jpg\OpenWithProgids]
[-HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.jxr\OpenWithProgids]
[-HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.png\OpenWithProgids]
[-HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.tif\OpenWithProgids]
[-HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.tiff\OpenWithProgids]
[-HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.wdp\OpenWithProgids]
",
                            RequiresElevation = false
                        }
                    }
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-legacy-notepad",
                    Name = "Use Legacy Notepad for text files",
                    Description = "Makes legacy Notepad available as a file handler and disables the Store Notepad redirect. Requires Notepad (Legacy) capability to be installed",
                    GroupName = "File Associations",
                    InputType = InputType.Toggle,
                    IconPack = "Fluent",
                    Icon = "NotepadEdit",
                    IsWindows11Only = true,
                    AddedInVersion = "26.04.03",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Applications\notepad.exe",
                            ValueName = "NoOpenWith",
                            EnabledValue = [null],
                            DisabledValue = [""],
                            DefaultValue = "",
                            ValueType = RegistryValueKind.String,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\notepad.exe",
                            ValueName = "UseFilter",
                            EnabledValue = [0],
                            DisabledValue = [1],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                    PowerShellScripts = new List<PowerShellScriptSetting>
                    {
                        new PowerShellScriptSetting
                        {
                            EnabledScript = @"
$appPathsKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\App Paths\notepad.exe'
if (Test-Path $appPathsKey) {
    Remove-Item -Path $appPathsKey -Force
}",
                            DisabledScript = null,
                            RequiresElevation = false,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-hide-merge-conflicts",
                    Name = "Hide folder merge conflicts",
                    Description = "Automatically merges folders with same name without confirmation dialog",
                    GroupName = "Files and Folders",
                    InputType = InputType.Toggle,
                    Icon = "FolderAlert",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "HideMergeConflicts",
                            RecommendedValue = 0,
                            EnabledValue = [null],
                            DisabledValue = [0],
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-hide-protected-files",
                    Name = "Show protected operating system files",
                    Description = "Displays system files marked with the SuperHidden attribute",
                    GroupName = "Files and Folders",
                    InputType = InputType.Toggle,
                    Icon = "FileHidden",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ShowSuperHidden",
                            RecommendedValue = 0,
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-separate-process",
                    Name = "Launch folder windows in a separate process",
                    Description = "Runs each Explorer window in its own process to prevent crashes affecting all windows",
                    GroupName = "Files and Folders",
                    InputType = InputType.Toggle,
                    Icon = "WindowRestore",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "SeparateProcess",
                            RecommendedValue = 0,
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-persist-browsers",
                    Name = "Restore previous folder windows at logon",
                    Description = "Reopens Explorer windows that were open when you last shut down or logged off",
                    GroupName = "Files and Folders",
                    InputType = InputType.Toggle,
                    IconPack = "Fluent",
                    Icon = "WindowAd",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "PersistBrowsers",
                            RecommendedValue = 0,
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-show-drive-letters",
                    Name = "Show drive letters",
                    Description = "Displays drive letters (C:, D:) before drive names in This PC",
                    GroupName = "Files and Folders",
                    InputType = InputType.Toggle,
                    Icon = "AlphaCBox",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ShowDriveLettersFirst",
                            RecommendedValue = null,
                            EnabledValue = [null],
                            DisabledValue = [2],
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-compressed-color",
                    Name = "Show encrypted or compressed NTFS files in color",
                    Description = "Displays encrypted files in green and compressed files in blue",
                    GroupName = "Files and Folders",
                    InputType = InputType.Toggle,
                    IconPack = "Fluent",
                    Icon = "DocumentLock",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ShowEncryptCompressedColor",
                            RecommendedValue = 1,
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-popup-descriptions",
                    Name = "Show pop-up description for folder and desktop items",
                    Description = "Displays tooltip with item details when hovering over files and folders",
                    GroupName = "Files and Folders",
                    InputType = InputType.Toggle,
                    Icon = "TooltipText",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ShowInfoTip",
                            RecommendedValue = 1,
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-preview-handlers",
                    Name = "Show preview handlers in preview pane",
                    Description = "Enables file content preview when selecting files in Explorer",
                    GroupName = "Files and Folders",
                    InputType = InputType.Toggle,
                    Icon = "TableEye",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ShowPreviewHandlers",
                            RecommendedValue = 0,
                            EnabledValue = [null],
                            DisabledValue = [0],
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-status-bar",
                    Name = "Show status bar",
                    Description = "Displays bar at bottom showing item count and selected file sizes",
                    GroupName = "Files and Folders",
                    InputType = InputType.Toggle,
                    Icon = "DockBottom",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ShowStatusBar",
                            RecommendedValue = 1,
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-disable-sync-provider-notifications",
                    Name = "Show sync provider notifications",
                    Description = "Displays cloud sync status notifications from OneDrive and other sync providers",
                    GroupName = "Files and Folders",
                    Icon = "CloudSync",
                    InputType = InputType.Toggle,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "ShowSyncProviderNotifications",
                            RecommendedValue = 0,
                            EnabledValue = [null],
                            DisabledValue = [0],
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-checkbox-select",
                    Name = "Use check boxes to select items",
                    Description = "Adds checkboxes next to items for easier multi-selection",
                    GroupName = "Files and Folders",
                    InputType = InputType.Toggle,
                    Icon = "CheckboxMarked",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "AutoCheckSelect",
                            RecommendedValue = 0,
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-sharing-wizard",
                    Name = "Use sharing wizard",
                    Description = "Shows simplified sharing dialog instead of advanced security permissions",
                    GroupName = "Files and Folders",
                    InputType = InputType.Toggle,
                    IconPack = "Fluent",
                    Icon = "ShareAndroid",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "SharingWizardOn",
                            RecommendedValue = 0,
                            EnabledValue = [null],
                            DisabledValue = [0],
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-typing-behavior",
                    Name = "When typing into list view",
                    Description = "Chooses whether typing selects matching items or searches automatically",
                    GroupName = "Files and Folders",
                    InputType = InputType.Selection,
                    Icon = "KeyboardOutline",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "TypeAhead",
                            RecommendedValue = 0,
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                            DefaultOption = "Select the typed item in the view",
                        },
                    },
                    ComboBox = new ComboBoxMetadata
                    {
                        DisplayNames = new string[]
                        {
                            "Select the typed item in the view",
                            "Automatically type into the Search Box",
                        },
                        ValueMappings = new Dictionary<int, Dictionary<string, object?>>
                        {
                            [0] = new Dictionary<string, object?>
                            {
                                ["TypeAhead"] = 0,
                            },
                            [1] = new Dictionary<string, object?>
                            {
                                ["TypeAhead"] = 1,
                            },
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-3d-objects",
                    Name = "Show 3D Objects",
                    Description = "Display the 3D Objects folder alongside Documents, Pictures, and other default folders",
                    GroupName = "Navigation Pane",
                    InputType = InputType.Toggle,
                    Icon = "Printer3d",
                    IsWindows10Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{0DB7E03F-FC29-4DC6-9020-FF41B59E513A}",
                            ValueName = null,
                            RecommendedValue = null,
                            EnabledValue = [null], // When toggle is ON, 3D Objects folder is shown (key exists)
                            DisabledValue = [null], // When toggle is OFF, 3D Objects folder is hidden (key removed)
                            DefaultValue = null,
                            ValueType = RegistryValueKind.None,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{0DB7E03F-FC29-4DC6-9020-FF41B59E513A}",
                            ValueName = null,
                            RecommendedValue = null,
                            EnabledValue = [null], // When toggle is ON, 3D Objects folder is shown (key exists)
                            DisabledValue = [null], // When toggle is OFF, 3D Objects folder is hidden (key removed)
                            DefaultValue = null,
                            ValueType = RegistryValueKind.None,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-home-folder",
                    Name = "Show Home Folder",
                    Description = "Display the Home folder in the navigation pane as a shortcut to your user profile folder",
                    GroupName = "Navigation Pane",
                    InputType = InputType.Toggle,
                    Icon = "Home",
                    IsWindows11Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{f874310e-b6b7-47dc-bc84-b9e6b38f5903}",
                            ValueName = null,
                            RecommendedValue = null,
                            EnabledValue = [null], // When toggle is ON, Home Folder is shown (key exists)
                            DisabledValue = [null], // When toggle is OFF, Home Folder is hidden (key removed)
                            DefaultValue = null,
                            ValueType = RegistryValueKind.None,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-gallery",
                    Name = "Show Gallery",
                    Description = "Display the Gallery folder in the navigation pane for quick access to all your photos and videos",
                    GroupName = "Navigation Pane",
                    InputType = InputType.Toggle,
                    Icon = "ImageMultiple",
                    IsWindows11Only = true,
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}",
                            ValueName = null,
                            RecommendedValue = null,
                            EnabledValue = [null],
                            DisabledValue = [null],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.None,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-show-availability-status",
                    Name = "Always show availability status",
                    Description = "Shows cloud sync status icons for OneDrive files in navigation pane",
                    GroupName = "Navigation Pane",
                    InputType = InputType.Toggle,
                    Icon = "ArchiveSync",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "NavPaneShowAllCloudStates",
                            RecommendedValue = 0,
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-nav-expand-current",
                    Name = "Expand to open folder",
                    Description = "Automatically expands navigation tree to highlight current folder location",
                    GroupName = "Navigation Pane",
                    InputType = InputType.Toggle,
                    Icon = "FileTree",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "NavPaneExpandToCurrentFolder",
                            RecommendedValue = 0,
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 1,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-nav-show-all-folders",
                    Name = "Show all folders",
                    Description = "Shows all folders in the navigation pane",
                    GroupName = "Navigation Pane",
                    InputType = InputType.Toggle,
                    Icon = "FolderMultiple",
                    RestartProcess = "Explorer",
                    AutoEnableSettingIds = new List<string>
                    {
                        "explorer-customization-nav-saf-desktop",
                        "explorer-customization-nav-saf-documents",
                        "explorer-customization-nav-saf-downloads",
                        "explorer-customization-nav-saf-music",
                        "explorer-customization-nav-saf-pictures",
                        "explorer-customization-nav-saf-videos",
                        "explorer-customization-nav-show-libraries",
                    },
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
                            ValueName = "NavPaneShowAllFolders",
                            RecommendedValue = 0,
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-nav-saf-desktop",
                    Name = "Show Desktop folder",
                    Description = "Shows the Desktop folder in the navigation pane",
                    GroupName = "Navigation Pane",
                    InputType = InputType.Toggle,
                    ParentSettingId = "explorer-customization-nav-show-all-folders",
                    AddedInVersion = "26.04.07",
                    Icon = "Monitor",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\NonEnum",
                            ValueName = "{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}",
                            RecommendedValue = 1,
                            EnabledValue = [null],
                            DisabledValue = [1, null],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}",
                            ValueName = "HiddenByDefault",
                            RecommendedValue = 1,
                            EnabledValue = [null],
                            DisabledValue = [1, null],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-nav-saf-documents",
                    Name = "Show Documents folder",
                    Description = "Shows the Documents folder in the navigation pane",
                    GroupName = "Navigation Pane",
                    InputType = InputType.Toggle,
                    ParentSettingId = "explorer-customization-nav-show-all-folders",
                    AddedInVersion = "26.04.07",
                    Icon = "FileDocument",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\NonEnum",
                            ValueName = "{A8CDFF1C-4878-43be-B5FD-F8091C1C60D0}",
                            RecommendedValue = 1,
                            EnabledValue = [null],
                            DisabledValue = [1, null],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{A8CDFF1C-4878-43be-B5FD-F8091C1C60D0}",
                            ValueName = "HiddenByDefault",
                            RecommendedValue = 1,
                            EnabledValue = [null],
                            DisabledValue = [1, null],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-nav-saf-downloads",
                    Name = "Show Downloads folder",
                    Description = "Shows the Downloads folder in the navigation pane",
                    GroupName = "Navigation Pane",
                    InputType = InputType.Toggle,
                    ParentSettingId = "explorer-customization-nav-show-all-folders",
                    AddedInVersion = "26.04.07",
                    Icon = "Download",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\NonEnum",
                            ValueName = "{374DE290-123F-4565-9164-39C4925E467B}",
                            RecommendedValue = 1,
                            EnabledValue = [null],
                            DisabledValue = [1, null],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{374DE290-123F-4565-9164-39C4925E467B}",
                            ValueName = "HiddenByDefault",
                            RecommendedValue = 1,
                            EnabledValue = [null],
                            DisabledValue = [1, null],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-nav-saf-music",
                    Name = "Show Music folder",
                    Description = "Shows the Music folder in the navigation pane",
                    GroupName = "Navigation Pane",
                    InputType = InputType.Toggle,
                    ParentSettingId = "explorer-customization-nav-show-all-folders",
                    AddedInVersion = "26.04.07",
                    Icon = "Music",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\NonEnum",
                            ValueName = "{1CF1260C-4DD0-4ebb-811F-33C572699FDE}",
                            RecommendedValue = 1,
                            EnabledValue = [null],
                            DisabledValue = [1, null],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{1CF1260C-4DD0-4ebb-811F-33C572699FDE}",
                            ValueName = "HiddenByDefault",
                            RecommendedValue = 1,
                            EnabledValue = [null],
                            DisabledValue = [1, null],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-nav-saf-pictures",
                    Name = "Show Pictures folder",
                    Description = "Shows the Pictures folder in the navigation pane",
                    GroupName = "Navigation Pane",
                    InputType = InputType.Toggle,
                    ParentSettingId = "explorer-customization-nav-show-all-folders",
                    AddedInVersion = "26.04.07",
                    Icon = "Image",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\NonEnum",
                            ValueName = "{3ADD1653-EB32-4cb0-BBD7-DFA0ABB5ACCA}",
                            RecommendedValue = 1,
                            EnabledValue = [null],
                            DisabledValue = [1, null],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{3ADD1653-EB32-4cb0-BBD7-DFA0ABB5ACCA}",
                            ValueName = "HiddenByDefault",
                            RecommendedValue = 1,
                            EnabledValue = [null],
                            DisabledValue = [1, null],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-nav-saf-videos",
                    Name = "Show Videos folder",
                    Description = "Shows the Videos folder in the navigation pane",
                    GroupName = "Navigation Pane",
                    InputType = InputType.Toggle,
                    ParentSettingId = "explorer-customization-nav-show-all-folders",
                    AddedInVersion = "26.04.07",
                    Icon = "Video",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\NonEnum",
                            ValueName = "{A0953C92-50DC-43bf-BE83-3742FED03C9C}",
                            RecommendedValue = 1,
                            EnabledValue = [null],
                            DisabledValue = [1, null],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{A0953C92-50DC-43bf-BE83-3742FED03C9C}",
                            ValueName = "HiddenByDefault",
                            RecommendedValue = 1,
                            EnabledValue = [null],
                            DisabledValue = [1, null],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-nav-show-libraries",
                    Name = "Show Libraries",
                    Description = "Pins the Libraries folder as a top-level item in the navigation pane. Has no effect when Show All Folders is enabled, as Libraries becomes part of the folder tree instead",
                    GroupName = "Navigation Pane",
                    InputType = InputType.Toggle,
                    Icon = "FolderTable",
                    RestartProcess = "Explorer",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_CURRENT_USER\Software\Classes\CLSID\{031E4825-7B94-4dc3-B131-E946B44C8DD5}",
                            ValueName = "System.IsPinnedToNameSpaceTree",
                            RecommendedValue = 0,
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\NonEnum",
                            ValueName = "{031E4825-7B94-4dc3-B131-E946B44C8DD5}",
                            RecommendedValue = 1,
                            EnabledValue = [0],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{031E4825-7B94-4dc3-B131-E946B44C8DD5}",
                            ValueName = "HiddenByDefault",
                            RecommendedValue = 1,
                            EnabledValue = [0],
                            DisabledValue = [1],
                            DefaultValue = null,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-long-file-paths",
                    Name = "Enable Long File Paths",
                    Description = "Enables support for file paths with up to 32,767 characters instead of the traditional 260-character limit",
                    GroupName = "Files and Folders",
                    InputType = InputType.Toggle,
                    Icon = "ScriptTextOutline",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FileSystem",
                            ValueName = "LongPathsEnabled",
                            RecommendedValue = 1,
                            EnabledValue = [1],
                            DisabledValue = [0],
                            DefaultValue = 0,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
                new SettingDefinition
                {
                    Id = "explorer-customization-netplwiz-auto-login",
                    Name = "Show Auto-Login Option in User Accounts",
                    Description = "Shows the classic 'Users must enter a user name and password to use this computer' checkbox in the User Accounts (netplwiz) window, allowing you to configure automatic logon through the standard Windows UI",
                    GroupName = "Network",
                    InputType = InputType.Toggle,
                    IconPack = "Fluent",
                    Icon = "PersonKey",
                    AddedInVersion = "26.04.03",
                    RegistrySettings = new List<RegistrySetting>
                    {
                        new RegistrySetting
                        {
                            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\PasswordLess\Device",
                            ValueName = "DevicePasswordLessBuildVersion",
                            RecommendedValue = 0,
                            EnabledValue = [0],
                            DisabledValue = [2],
                            DefaultValue = 2,
                            ValueType = RegistryValueKind.DWord,
                        },
                    },
                },
            },
        };
    }
}
