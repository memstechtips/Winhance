using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using static Winhance.Core.Features.Common.Catalog.StateValue;

namespace Winhance.Core.Features.Customize.Models;

public static class ExplorerCustomizationsCatalog
{
    public const string FeatureId = FeatureIds.ExplorerCustomization;
    public const string FeatureName = "ExplorerCustomizations";

    public static IReadOnlyList<Setting> All { get; } = new Setting[]
    {
        new()
        {
            Id = "explorer-customization-shortcut-suffix",
            Display = new()
            {
                Name = "Shortcut Naming",
                Description = "Controls whether Windows appends '- Shortcut' text to newly created shortcut file names",
                GroupName = "Desktop",
                Icon = MaterialIcons.LinkVariant,
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("link", new[] { @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer" }, "link", RegistryValueKind.Binary),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Keep '- Shortcut' suffix",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["link"] = Absent },
                },
                new SettingState
                {
                    Label = "Remove '- Shortcut' suffix",
                    Set = new Dictionary<string, StateValue> { ["link"] = Of(new byte[] { 0x00, 0x00, 0x00, 0x00 }) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-shortcut-arrow",
            Display = new()
            {
                Name = "Shortcut Arrow Icon",
                Description = "Controls the small arrow overlay on desktop shortcut icons",
                GroupName = "Desktop",
                Icon = MaterialIcons.ArrowTopLeftBoldOutline,
                AddedInVersion = "26.03.26",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("29", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons" }, "29", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Show arrow icon",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["29"] = Absent },
                },
                new SettingState
                {
                    Label = "Remove arrow icon",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["29"] = Of(@"C:\Windows\blank.ico") },
                    Effects = new Effect[] { new ScriptEffect(@"
$icoPath = ""$env:SystemRoot\blank.ico""
if (-not (Test-Path $icoPath)) {
    $b64='AAABAAEAAAAAAAEAIAC5BwAAFgAAAIlQTkcNChoKAAAADUlIRFIAAAEAAAABAAgGAAAAXHKoZgAAB4BJREFUeNrt3eGSmzYAhVFnp+//xJlM67ZJ3Y0XkJBA0j1nJn+yDggEnzG2N98eQKxvdw8AuI8AQDABgGACAMEEAIIJAAQTAAgmABBMACCYAEAwAYBgAgDBBACCCQAEEwAIJgAQTAAgmABAMAGAYAIAwQQAggkABBMACCYAEEwAIJgAQDABgGACAMEEAIIJAAQTAAgmABBMACCYAEAwAYBgAgDBBACCJQXg48Bjftw9SLjSSgHYO8GPnNwfBY+F6c0cgM8nfMuT9qPx8mBIMwbgqmdpVwMsb7YA3PHM7GqAZc0UgDtPRBFgSTMEYJRLcRFgOSMHYJQT/3U8o4wFmhg1AKOebKOOC6qMGICRT7KRxwbFBGC98cFhowVghpNrhjHCIQKw9jhh00gBGO2u/95YZxgnbBKAc+OdZazw1igB+HkyzXRSzTRWeGuEAHw+kWY6sWYaK/xGANqOneuZgxPuDsC7yZtpQmca66rMwQkC0GcbuI79f4IA9NkGrmUOKglAn23gWuag0ogB2Pr7Ec001lWZg0oC0G8buJZ5qCAAfbeD65iDCgLQdzu4jjmoIAB9t4NrmYdCowZg72ejmWmsKzMPhQSg/3ZwHfNQ6O4AbJlpMmca68rMQyEByBvrysxDIQHIG+vqzEUBAcgb6+rMRYGRA/A0y2TOMs4E5qKAAGSNM4G5KCAAWeNMYC4KCEDGGJOYjwICsPb4UpmXgwRg7fGlMi8HCcCaY0tnbg4SgLXGxT/Mz0ECUDemx4Dj4j8jHjdDEoC5x8N75ukgATg+jscgY+GYUY6doQnA/vofN4+BOncfO1MQgDHXzXnm7wABeL/Oxw3rpS0BOEAA/r+ux4Xroy8BOGD0ADx97Py8dpI/L9fBsh4R2DF6AD7+/fN95zE/dn7+jgNjfQKwY4YAPO1N4tZVggMglwDsmCEANZNo0nkSgB2jB+CpZhLd0ONJAHasGoDXf/s48e+ZnwhsWD0ALZfBnMz9hhkC8CQC1DLvGwSA1Zn3DUkBaLkc5mHON6QFoPWyGJ/53pAYgB7LY2zm+wsCQALz/YVZAvAkAtQy119IDkCvZTIe8/yF9AD0XO679XzmoLyGAHxBAPove2v5DsxyryEt2Xf29RsC0H/5W8tNOShb/vKV2u93pOzrIgLQd/lHlrn6gfl5+858Qevnsmq/Ibryfq4iAP3WcXRZR36j0dHlvBrhYG/90kcAGhOAPusoPanPvkwoWd5Verz0aXH1wAsB6LOeVs/qPx/7eJSfTHcf8C0DUHvjb7R9MpyZAvA0w1XA0Wfsx8H1bD221Ul29YesagLgy2AdCEDb9ZRerp+NRYsTrcdvTbryCqjluHrvl+EIQNv19ArAuxtfLd5hKB1Lj32w9ZhH4/EJwCcC0G5dtQf+0ZO05mQteYlw1w3QksdcFYEz7zZMRQDarW/EAGw9tmYsLfZDi58/TozzjquiYQlAm/X1fFZ7d0COGoDak7vmLn+Pl2m9ojis2QLwNOLLgLOve0sv1R+F+6D15w5Kt/HMjcya9dWOccS3U7sSgDbr63nZ2+qjtEevHlqdkHvP6q1usrW4V9NznwxNANqs7+obX2ee+a4IwF7QHifWcXbMtTc/l4yAALRZX82l794yWl82l9w/6PFR3aP7oudbsO8eJwCTuWMiau9M3/WR15Ix1mzv1pg/vnhcz5t7tdvW6q3eaT8zIADt1tnqN/70PJhava3ZM1Sfl39E7Ul8xf2Doc0YgDtMPckNt3naZ7ovtqvFuwhTHxsCcMzUk3xyu1+tsA8E4IUAHDf1RPNLi4/5LvNpQQE4buqJ5pcWL2OW+a6AAJRZ4TVwupYfQGqxnFsJQJklJj3c9M/ajfx9LAsAaQTgZR8IAGR4+9kNAYC1bb5sFQBYS9FnNwQA5nX64+cCAPNo/slMAYAxtfpy2SYBgPtsfevxkrcqBQDaO/p15ts/jyAAUG7vBL/9xD5KAOBrZ3+70fAEgHS3vw6/kwCQIPok3yIArGT5S/bWBIDZeDZvSAAYzTJ32GcgAFzNCT4QAaA1J/hEBIBabrgtQADY4obb4gSAJ8/mj8cff/35fvcgriYAOTyb8xsBWI9ncw4TgPmt+P/3cREBgGACAMEEAIIJAAQTAAgmABBMACCYAEAwAYBgAgDBBACCCQAEEwAIJgAQTAAgmABAMAGAYAIAwQQAggkABBMACCYAEEwAIJgAQDABgGACAMEEAIIJAAQTAAgmABBMACCYAEAwAYBgAgDBBACCCQAEEwAIJgAQTAAgmABAMAGAYAIAwQQAggkABBMACCYAEEwAIJgAQDABgGACAMEEAIIJAAQTAAgmABBMACCYAEAwAYBgAgDBBACCCQAEEwAIJgAQTAAgmABAMAGAYAIAwQQAggkABBMACCYAEEwAIJgAQDABgGACAMEEAIIJAAQTAAgmABBMACCYAEAwAYBgAgDBBACCCQAEEwAIJgAQTAAgmABAMAGAYAIAwQQAggkABBMACCYAEEwAIJgAQDABgGACAMEEAIIJAAQTAAgmABBMACCYAEAwAYBgAgDB/gRG/ewS3uwoeAAAAABJRU5ErkJggg=='
    [IO.File]::WriteAllBytes($icoPath,[Convert]::FromBase64String($b64))
}", RunContext.System) },
                },
            },
            CustomStateScripts = new[] { new ScriptEffect(@"
$icoPath = ""$env:SystemRoot\blank.ico""
if (-not (Test-Path $icoPath)) {
    $b64='AAABAAEAAAAAAAEAIAC5BwAAFgAAAIlQTkcNChoKAAAADUlIRFIAAAEAAAABAAgGAAAAXHKoZgAAB4BJREFUeNrt3eGSmzYAhVFnp+//xJlM67ZJ3Y0XkJBA0j1nJn+yDggEnzG2N98eQKxvdw8AuI8AQDABgGACAMEEAIIJAAQTAAgmABBMACCYAEAwAYBgAgDBBACCCQAEEwAIJgAQTAAgmABAMAGAYAIAwQQAggkABBMACCYAEEwAIJgAQDABgGACAMEEAIIJAAQTAAgmABBMACCYAEAwAYBgAgDBBACCJQXg48Bjftw9SLjSSgHYO8GPnNwfBY+F6c0cgM8nfMuT9qPx8mBIMwbgqmdpVwMsb7YA3PHM7GqAZc0UgDtPRBFgSTMEYJRLcRFgOSMHYJQT/3U8o4wFmhg1AKOebKOOC6qMGICRT7KRxwbFBGC98cFhowVghpNrhjHCIQKw9jhh00gBGO2u/95YZxgnbBKAc+OdZazw1igB+HkyzXRSzTRWeGuEAHw+kWY6sWYaK/xGANqOneuZgxPuDsC7yZtpQmca66rMwQkC0GcbuI79f4IA9NkGrmUOKglAn23gWuag0ogB2Pr7Ec001lWZg0oC0G8buJZ5qCAAfbeD65iDCgLQdzu4jjmoIAB9t4NrmYdCowZg72ejmWmsKzMPhQSg/3ZwHfNQ6O4AbJlpMmca68rMQyEByBvrysxDIQHIG+vqzEUBAcgb6+rMRYGRA/A0y2TOMs4E5qKAAGSNM4G5KCAAWeNMYC4KCEDGGJOYjwICsPb4UpmXgwRg7fGlMi8HCcCaY0tnbg4SgLXGxT/Mz0ECUDemx4Dj4j8jHjdDEoC5x8N75ukgATg+jscgY+GYUY6doQnA/vofN4+BOncfO1MQgDHXzXnm7wABeL/Oxw3rpS0BOEAA/r+ux4Xroy8BOGD0ADx97Py8dpI/L9fBsh4R2DF6AD7+/fN95zE/dn7+jgNjfQKwY4YAPO1N4tZVggMglwDsmCEANZNo0nkSgB2jB+CpZhLd0ONJAHasGoDXf/s48e+ZnwhsWD0ALZfBnMz9hhkC8CQC1DLvGwSA1Zn3DUkBaLkc5mHON6QFoPWyGJ/53pAYgB7LY2zm+wsCQALz/YVZAvAkAtQy119IDkCvZTIe8/yF9AD0XO679XzmoLyGAHxBAPove2v5DsxyryEt2Xf29RsC0H/5W8tNOShb/vKV2u93pOzrIgLQd/lHlrn6gfl5+858Qevnsmq/Ibryfq4iAP3WcXRZR36j0dHlvBrhYG/90kcAGhOAPusoPanPvkwoWd5Verz0aXH1wAsB6LOeVs/qPx/7eJSfTHcf8C0DUHvjb7R9MpyZAvA0w1XA0Wfsx8H1bD221Ul29YesagLgy2AdCEDb9ZRerp+NRYsTrcdvTbryCqjluHrvl+EIQNv19ArAuxtfLd5hKB1Lj32w9ZhH4/EJwCcC0G5dtQf+0ZO05mQteYlw1w3QksdcFYEz7zZMRQDarW/EAGw9tmYsLfZDi58/TozzjquiYQlAm/X1fFZ7d0COGoDak7vmLn+Pl2m9ojis2QLwNOLLgLOve0sv1R+F+6D15w5Kt/HMjcya9dWOccS3U7sSgDbr63nZ2+qjtEevHlqdkHvP6q1usrW4V9NznwxNANqs7+obX2ee+a4IwF7QHifWcXbMtTc/l4yAALRZX82l794yWl82l9w/6PFR3aP7oudbsO8eJwCTuWMiau9M3/WR15Ix1mzv1pg/vnhcz5t7tdvW6q3eaT8zIADt1tnqN/70PJhava3ZM1Sfl39E7Ul8xf2Doc0YgDtMPckNt3naZ7ovtqvFuwhTHxsCcMzUk3xyu1+tsA8E4IUAHDf1RPNLi4/5LvNpQQE4buqJ5pcWL2OW+a6AAJRZ4TVwupYfQGqxnFsJQJklJj3c9M/ajfx9LAsAaQTgZR8IAGR4+9kNAYC1bb5sFQBYS9FnNwQA5nX64+cCAPNo/slMAYAxtfpy2SYBgPtsfevxkrcqBQDaO/p15ts/jyAAUG7vBL/9xD5KAOBrZ3+70fAEgHS3vw6/kwCQIPok3yIArGT5S/bWBIDZeDZvSAAYzTJ32GcgAFzNCT4QAaA1J/hEBIBabrgtQADY4obb4gSAJ8/mj8cff/35fvcgriYAOTyb8xsBWI9ncw4TgPmt+P/3cREBgGACAMEEAIIJAAQTAAgmABBMACCYAEAwAYBgAgDBBACCCQAEEwAIJgAQTAAgmABAMAGAYAIAwQQAggkABBMACCYAEEwAIJgAQDABgGACAMEEAIIJAAQTAAgmABBMACCYAEAwAYBgAgDBBACCCQAEEwAIJgAQTAAgmABAMAGAYAIAwQQAggkABBMACCYAEEwAIJgAQDABgGACAMEEAIIJAAQTAAgmABBMACCYAEAwAYBgAgDBBACCCQAEEwAIJgAQTAAgmABAMAGAYAIAwQQAggkABBMACCYAEEwAIJgAQDABgGACAMEEAIIJAAQTAAgmABBMACCYAEAwAYBgAgDBBACCCQAEEwAIJgAQTAAgmABAMAGAYAIAwQQAggkABBMACCYAEEwAIJgAQDABgGACAMEEAIIJAAQTAAgmABBMACCYAEAwAYBgAgDB/gRG/ewS3uwoeAAAAABJRU5ErkJggg=='
    [IO.File]::WriteAllBytes($icoPath,[Convert]::FromBase64String($b64))
}", RunContext.System) },
        },
        new()
        {
            Id = "explorer-customization-desktop-icon-this-pc",
            Display = new()
            {
                Name = "Show This PC on desktop",
                Description = "Displays the This PC (Computer) icon on the desktop",
                GroupName = "Desktop",
                Icon = MaterialIcons.Monitor,
                AddedInVersion = "26.06.01",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("{20D04FE0-3AEA-1069-A2D8-08002B30309D}", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel", @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\ClassicStartMenu" }, "{20D04FE0-3AEA-1069-A2D8-08002B30309D}", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { ["{20D04FE0-3AEA-1069-A2D8-08002B30309D}"] = Of(0) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["{20D04FE0-3AEA-1069-A2D8-08002B30309D}"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-desktop-icon-recycle-bin",
            Display = new()
            {
                Name = "Show Recycle Bin on desktop",
                Description = "Displays the Recycle Bin icon on the desktop",
                GroupName = "Desktop",
                Icon = MaterialIcons.TrashCanOutline,
                AddedInVersion = "26.06.01",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("{645FF040-5081-101B-9F08-00AA002F954E}", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel", @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\ClassicStartMenu" }, "{645FF040-5081-101B-9F08-00AA002F954E}", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["{645FF040-5081-101B-9F08-00AA002F954E}"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["{645FF040-5081-101B-9F08-00AA002F954E}"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-desktop-icon-users-files",
            Display = new()
            {
                Name = "Show User's Files on desktop",
                Description = "Displays the current user's profile folder icon on the desktop",
                GroupName = "Desktop",
                Icon = MaterialIcons.FolderAccount,
                AddedInVersion = "26.06.01",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("{59031A47-3F72-44A7-89C5-5595FE6B30EE}", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel", @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\ClassicStartMenu" }, "{59031A47-3F72-44A7-89C5-5595FE6B30EE}", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { ["{59031A47-3F72-44A7-89C5-5595FE6B30EE}"] = Of(0) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["{59031A47-3F72-44A7-89C5-5595FE6B30EE}"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-desktop-icon-control-panel",
            Display = new()
            {
                Name = "Show Control Panel on desktop",
                Description = "Displays the Control Panel icon on the desktop",
                GroupName = "Desktop",
                Icon = MaterialIcons.ViewGrid,
                AddedInVersion = "26.06.01",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("{5399E694-6CE5-4D6C-8FCE-1D8870FDCBA0}", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel", @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\ClassicStartMenu" }, "{5399E694-6CE5-4D6C-8FCE-1D8870FDCBA0}", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { ["{5399E694-6CE5-4D6C-8FCE-1D8870FDCBA0}"] = Of(0) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["{5399E694-6CE5-4D6C-8FCE-1D8870FDCBA0}"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-desktop-icon-network",
            Display = new()
            {
                Name = "Show Network on desktop",
                Description = "Displays the Network icon on the desktop",
                GroupName = "Desktop",
                Icon = MaterialIcons.NetworkOutline,
                AddedInVersion = "26.06.01",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel", @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\ClassicStartMenu" }, "{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { ["{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}"] = Of(0) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-context-menu",
            Display = new()
            {
                Name = "Use Classic Context Menu",
                Description = "Use the Windows 10-style right-click menu with all options visible instead of the simplified Windows 11 menu",
                GroupName = "Context Menu",
                Icon = FluentIcons.Navigation,
                IsSubjectivePreference = true,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("", new[] { @"HKEY_CURRENT_USER\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32" }, "", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { [""] = Of("") },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { [""] = Absent },
                },
            },
        },
        new()
        {
            Id = "explorer-take-ownership",
            Display = new()
            {
                Name = "Add 'Take Ownership' to Context Menu",
                Description = "Adds a right-click option to take ownership of files, folders, and drives with automatic permission elevation. May require temporarily disabling Windows Defender for protected files",
                GroupName = "Context Menu",
                Icon = MaterialIcons.Security,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("", new[] { @"HKEY_CLASSES_ROOT\*\shell\TakeOwnership" }, "", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { [""] = Of("Take Ownership") },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

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
") },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { [""] = Absent },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_CLASSES_ROOT\*\shell\TakeOwnership]
[-HKEY_CLASSES_ROOT\*\shell\runas]
[-HKEY_CLASSES_ROOT\Directory\shell\TakeOwnership]
[-HKEY_CLASSES_ROOT\Drive\shell\runas]
") },
                },
            },
        },
        new()
        {
            Id = "explorer-context-menu-toggle-extensions",
            Display = new()
            {
                Name = "Add 'Show/Hide Extensions' to Context Menu",
                Description = "Adds a right-click menu option to quickly toggle file extension visibility in File Explorer (only visible on the Classic Context Menu or Show More Options Menu in Windows 11)",
                GroupName = "Context Menu",
                Icon = FluentIcons.DocumentQuestionMark,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("ExplorerCommandHandler", new[] { @"HKEY_CLASSES_ROOT\AllFilesystemObjects\shell\Windows.ShowFileExtensions" }, "ExplorerCommandHandler", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Links = new[] { new Link("explorer-customization-context-menu", LinkKind.Requires, "Enabled") },
                    Set = new Dictionary<string, StateValue> { ["ExplorerCommandHandler"] = Of("{4ac6c205-2853-4bf5-b47c-919a42a48a16}") },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

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
") },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ExplorerCommandHandler"] = Absent },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_CLASSES_ROOT\AllFilesystemObjects\shell\Windows.ShowFileExtensions]
[-HKEY_CLASSES_ROOT\Directory\Background\shell\Windows.ShowFileExtensions]
") },
                },
            },
        },
        new()
        {
            Id = "explorer-context-menu-windows-terminal",
            Display = new()
            {
                Name = "Show 'Open in Windows Terminal' in Context Menu",
                Description = "Displays the Windows Terminal option when right-clicking folders and backgrounds in File Explorer",
                GroupName = "Context Menu",
                Icon = MaterialIcons.Console,
                IsSubjectivePreference = true,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("{9F156763-7844-4DC4-B2B1-901F640F5155}", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked" }, "{9F156763-7844-4DC4-B2B1-901F640F5155}", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["{9F156763-7844-4DC4-B2B1-901F640F5155}"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["{9F156763-7844-4DC4-B2B1-901F640F5155}"] = Of("") },
                },
            },
        },
        new()
        {
            Id = "explorer-context-menu-sfc",
            Display = new()
            {
                Name = "Add 'SFC /SCANNOW' to Context Menu",
                Description = "Adds right-click options to run System File Checker (SFC /SCANNOW) and view scan details from the desktop or folder background",
                GroupName = "Context Menu",
                Icon = MaterialIcons.MagnifyScan,
                AddedInVersion = "25.04.09",
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("MUIVerb", new[] { @"HKEY_CLASSES_ROOT\Directory\Background\shell\SFC" }, "MUIVerb", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Links = new[] { new Link("explorer-customization-context-menu", LinkKind.Requires, "Enabled") },
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Of("SFC /SCANNOW") },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

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
") },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Absent },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_CLASSES_ROOT\Directory\Background\shell\SFC]
") },
                },
            },
        },
        new()
        {
            Id = "explorer-context-menu-dism",
            Display = new()
            {
                Name = "Add 'Repair Windows Image' to Context Menu",
                Description = "Adds a right-click option to run DISM /RestoreHealth to repair the Windows system image from the desktop or folder background",
                GroupName = "Context Menu",
                Icon = MaterialIcons.MedicalBag,
                AddedInVersion = "25.04.09",
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("MUIVerb", new[] { @"HKEY_CLASSES_ROOT\Directory\Background\shell\RepairWindowsImage" }, "MUIVerb", RegistryValueKind.String),
                new RegTarget("Icon", new[] { @"HKEY_CLASSES_ROOT\Directory\Background\shell\RepairWindowsImage" }, "Icon", RegistryValueKind.String),
                new RegTarget("HasLUAShield", new[] { @"HKEY_CLASSES_ROOT\Directory\Background\shell\RepairWindowsImage" }, "HasLUAShield", RegistryValueKind.String),
                new RegTarget("", new[] { @"HKEY_CLASSES_ROOT\Directory\Background\shell\RepairWindowsImage\command" }, "", RegistryValueKind.String),
                new RegTarget("KeyExists", new[] { @"HKEY_CLASSES_ROOT\Directory\Background\shell\RepairWindowsImage" }, null, RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Links = new[] { new Link("explorer-customization-context-menu", LinkKind.Requires, "Enabled") },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["MUIVerb"] = Of("Repair Windows Image"),
                        ["Icon"] = Of("WmiPrvSE.exe"),
                        ["HasLUAShield"] = Of(""),
                        [""] = Of("PowerShell -ExecutionPolicy Bypass -windowstyle hidden -command \"Start-Process cmd -ArgumentList '/s,/k, DISM /Online /Cleanup-Image /RestoreHealth' -Verb runAs\""),
                        ["KeyExists"] = Exists,
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["MUIVerb"] = Absent,
                        ["Icon"] = Absent,
                        ["HasLUAShield"] = Absent,
                        [""] = Absent,
                        ["KeyExists"] = Absent,
                    },
                },
            },
        },
        new()
        {
            Id = "explorer-context-menu-chkdsk",
            Display = new()
            {
                Name = "Add 'CHKDSK' to Context Menu",
                Description = "Adds right-click options to run CHKDSK from the desktop or folder background with a prompt to select the drive letter",
                GroupName = "Context Menu",
                Icon = MaterialIcons.Harddisk,
                AddedInVersion = "25.04.09",
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("MUIVerb", new[] { @"HKEY_CLASSES_ROOT\Directory\Background\shell\CHKDSK" }, "MUIVerb", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Links = new[] { new Link("explorer-customization-context-menu", LinkKind.Requires, "Enabled") },
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Of("CHKDSK") },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

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
") },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Absent },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_CLASSES_ROOT\Directory\Background\shell\CHKDSK]
") },
                },
            },
        },
        new()
        {
            Id = "explorer-context-menu-ps1-edit-run",
            Display = new()
            {
                Name = "Add 'Edit or Run with' to PS1 Context Menu",
                Description = "Adds a right-click cascading menu to .ps1 files with options to run or edit with PowerShell, PowerShell 7, PowerShell ISE, and Notepad (including as administrator). PowerShell 7 must be installed separately for the PowerShell 7 options to work",
                GroupName = "Context Menu",
                Icon = MaterialIcons.Powershell,
                AddedInVersion = "25.04.09",
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("MUIVerb", new[] { @"HKEY_CLASSES_ROOT\SystemFileAssociations\.ps1\Shell\Edit-Run-with" }, "MUIVerb", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Links = new[] { new Link("explorer-customization-context-menu", LinkKind.Requires, "Enabled") },
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Of("Edit or Run with") },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

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
@=""\""C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe\"" \""-Command\"" \""\""& {Start-Process PowerShell.exe -ArgumentList '-ExecutionPolicy RemoteSigned -File \\\""%1\\\""' -Verb RunAs}\""""

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
@=""\""C:\\Program Files\\PowerShell\\7\\pwsh.exe\"" \""-Command\"" \""\""& {Start-Process pwsh.exe -ArgumentList '-ExecutionPolicy RemoteSigned -File \\\""%1\\\""' -Verb RunAs}\""""

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
@=""PowerShell -windowstyle hidden -Command \""Start-Process cmd -ArgumentList '/s,/c,start PowerShell_ISE.exe \""\""%1\""\""'  -Verb RunAs\""""

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
@=""PowerShell -windowstyle hidden -Command \""Start-Process cmd -ArgumentList '/s,/c,start C:\\WINDOWS\\syswow64\\WindowsPowerShell\\v1.0\\powershell_ise.exe \""\""%1\""\""'  -Verb RunAs\""""

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
@=""PowerShell -windowstyle hidden -Command \""Start-Process cmd -ArgumentList '/s,/c,start C:\\Windows\\System32\\notepad.exe \""\""%1\""\""'  -Verb RunAs\""""
") },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Absent },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_CLASSES_ROOT\SystemFileAssociations\.ps1\Shell\Edit-Run-with]
") },
                },
            },
        },
        new()
        {
            Id = "explorer-context-menu-compress-to",
            Display = new()
            {
                Name = "Add 'Compress To' to Context Menu",
                Description = "Adds a right-click option to compress files and folders into various archive formats (ZIP, 7z, TAR) directly from the classic context menu",
                GroupName = "Context Menu",
                Icon = FluentIcons.FolderZip,
                AddedInVersion = "25.04.09",
                IsSubjectivePreference = true,
            },
            Availability = new() { Builds = new[] { BuildRange.Between(26100, int.MaxValue) } },
            Targets = new Target[]
            {
                new RegTarget("ExplorerCommandHandler", new[] { @"HKEY_CLASSES_ROOT\*\shell\CompressToFullMenu_ForOldContextMenu" }, "ExplorerCommandHandler", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Links = new[] { new Link("explorer-customization-context-menu", LinkKind.Requires, "Enabled") },
                    Set = new Dictionary<string, StateValue> { ["ExplorerCommandHandler"] = Of("{7AE6900F-6EB0-44A2-9CA1-DB2F7EF352AF}") },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

; Credit: ThioJoe - https://github.com/ThioJoe/
; Source: https://gist.github.com/ThioJoe/f4b0799e2f0d95466f4c2bd4e46d1e67

[HKEY_CLASSES_ROOT\*\shell\CompressToFullMenu_ForOldContextMenu]
""CommandStateSync""=""""
""ExplorerCommandHandler""=""{7AE6900F-6EB0-44A2-9CA1-DB2F7EF352AF}""
""MUIVerb""=""@Windows.UI.FileExplorer.dll,-51797""
""Note""=""Copied from original Command Store command: Windows.CompressTo""

[HKEY_CLASSES_ROOT\Folder\shell\CompressToFullMenu_ForOldContextMenu]
""CommandStateSync""=""""
""ExplorerCommandHandler""=""{7AE6900F-6EB0-44A2-9CA1-DB2F7EF352AF}""
""MUIVerb""=""@Windows.UI.FileExplorer.dll,-51797""
""Note""=""Copied from original Command Store command: Windows.CompressTo""
") },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ExplorerCommandHandler"] = Absent },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_CLASSES_ROOT\*\shell\CompressToFullMenu_ForOldContextMenu]

[-HKEY_CLASSES_ROOT\Folder\shell\CompressToFullMenu_ForOldContextMenu]
") },
                },
            },
        },
        new()
        {
            Id = "devices-dynamic-lighting-ambient",
            Display = new()
            {
                Name = "Use Dynamic Lighting on my devices",
                Description = "Allow Windows Dynamic Lighting to control ambient RGB effects on compatible devices",
                GroupName = "Devices and Peripherals",
                Icon = MaterialIcons.TelevisionAmbientLight,
                IsSubjectivePreference = true,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("AmbientLightingEnabled", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Lighting" }, "AmbientLightingEnabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["AmbientLightingEnabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["AmbientLightingEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "devices-dynamic-lighting-foreground-app",
            Display = new()
            {
                Name = "Compatible apps in the foreground always control lighting",
                Description = "Allow compatible apps to control device lighting effects",
                GroupName = "Devices and Peripherals",
                Icon = MaterialIcons.StringLightsOff,
                IsSubjectivePreference = true,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("ControlledByForegroundApp", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Lighting" }, "ControlledByForegroundApp", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ControlledByForegroundApp"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ControlledByForegroundApp"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "devices-default-printer-management",
            Display = new()
            {
                Name = "Automatic Default Printer Management",
                Description = "Let Windows automatically set your default printer based on your location or last used printer",
                GroupName = "Devices and Peripherals",
                Icon = MaterialIcons.PrinterOff,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("LegacyDefaultPrinterMode", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows NT\CurrentVersion\Windows" }, "LegacyDefaultPrinterMode", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["LegacyDefaultPrinterMode"] = OneOf(0, -1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["LegacyDefaultPrinterMode"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-launch-to",
            Display = new()
            {
                Name = "Open File Explorer to",
                Description = "Choose what happens when File Explorer is opened",
                GroupName = "General",
                Icon = FluentIcons.FolderOpen,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("LaunchTo", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "LaunchTo", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Home",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["LaunchTo"] = Of(2).OrAbsent() },
                },
                new SettingState
                {
                    Label = "This PC",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["LaunchTo"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Downloads",
                    Set = new Dictionary<string, StateValue> { ["LaunchTo"] = Of(3) },
                },
                new SettingState
                {
                    Label = "OneDrive (If Available)",
                    Set = new Dictionary<string, StateValue> { ["LaunchTo"] = Of(4) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-browse-folders",
            Display = new()
            {
                Name = "Browse folders",
                Description = "Choose whether each folder opens in the same window or in its own window",
                GroupName = "General",
                Icon = FluentIcons.FolderList,
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("Settings", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\CabinetState" }, "Settings", RegistryValueKind.Binary) { ByteIndex = 4, BitMask = 0x20 },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Open each folder in the same window",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Settings"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Open each folder in its own window",
                    Set = new Dictionary<string, StateValue> { ["Settings"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-click-items",
            Display = new()
            {
                Name = "Click items as follows",
                Description = "Choose whether to open files and folders with a single click (like web links) or double-click (traditional)",
                GroupName = "General",
                Icon = FluentIcons.CursorClick,
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("ShellState", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer" }, "ShellState", RegistryValueKind.Binary) { ByteIndex = 4, BitMask = 0x20 },
                new RegTarget("IconUnderline", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer" }, "IconUnderline", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Double-click to open an item (single-click to select)",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["ShellState"] = Of(1),
                        ["IconUnderline"] = Of(3).OrAbsent(),
                    },
                },
                new SettingState
                {
                    Label = "Single-click to open (underline icon titles consistent with browser)",
                    Set = new Dictionary<string, StateValue>
                    {
                        ["ShellState"] = Of(0),
                        ["IconUnderline"] = Of(3).OrAbsent(),
                    },
                },
                new SettingState
                {
                    Label = "Single-click to open (underline icon titles only when pointing)",
                    Set = new Dictionary<string, StateValue>
                    {
                        ["ShellState"] = Of(0),
                        ["IconUnderline"] = Of(2),
                    },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-show-recent-files",
            Display = new()
            {
                Name = "Show recently used files",
                Description = "Displays recently accessed files and recommendations in Quick Access",
                GroupName = "General",
                Icon = FluentIcons.DocumentTextClock,
            },
            Targets = new Target[]
            {
                new RegTarget("ShowRecent", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer" }, "ShowRecent", RegistryValueKind.DWord),
                new RegTarget("ShowRecommendations", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer" }, "ShowRecommendations", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["ShowRecent"] = Of(1).OrAbsent(),
                        ["ShowRecommendations"] = Of(1).OrAbsent(),
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["ShowRecent"] = Of(0),
                        ["ShowRecommendations"] = Of(0),
                    },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-show-frequent-folders",
            Display = new()
            {
                Name = "Show frequently used folders",
                Description = "Displays your most accessed folders in Quick Access section",
                GroupName = "General",
                Icon = MaterialIcons.FolderClockOutline,
            },
            Targets = new Target[]
            {
                new RegTarget("ShowFrequent", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer" }, "ShowFrequent", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShowFrequent"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ShowFrequent"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-show-office-files",
            Display = new()
            {
                Name = "Show files from Office.com",
                Description = "Displays cloud files from your Office.com account in Quick Access",
                GroupName = "General",
                Icon = MaterialIcons.FileCloud,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("ShowCloudFilesInQuickAccess", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer" }, "ShowCloudFilesInQuickAccess", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShowCloudFilesInQuickAccess"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ShowCloudFilesInQuickAccess"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-show-thumbnails",
            Display = new()
            {
                Name = "Always show icons, never thumbnails",
                Description = "Displays generic file icons instead of image/document previews",
                GroupName = "Files and Folders",
                Icon = MaterialIcons.ImageOff,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("IconsOnly", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "IconsOnly", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { ["IconsOnly"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["IconsOnly"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-show-menus",
            Display = new()
            {
                Name = "Always show menus",
                Description = "Shows the Menu bar (File, Edit etc.) on all windows that support it",
                GroupName = "Files and Folders",
                Icon = FluentIcons.WindowApps,
                IsSubjectivePreference = true,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows10 } },
            Targets = new Target[]
            {
                new RegTarget("AlwaysShowMenus", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "AlwaysShowMenus", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { ["AlwaysShowMenus"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["AlwaysShowMenus"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-item-space",
            Display = new()
            {
                Name = "Decrease space between items (compact view)",
                Description = "Reduces vertical spacing between files and folders for denser view",
                GroupName = "Files and Folders",
                Icon = MaterialIcons.ViewCompact,
                IsSubjectivePreference = true,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("UseCompactMode", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "UseCompactMode", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { ["UseCompactMode"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["UseCompactMode"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-icon-thumbnails",
            Display = new()
            {
                Name = "Display file icon on thumbnails",
                Description = "Shows file type icon overlay on bottom-right corner of thumbnail previews",
                GroupName = "Files and Folders",
                Icon = FluentIcons.DocumentImage,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("ShowTypeOverlay", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "ShowTypeOverlay", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShowTypeOverlay"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ShowTypeOverlay"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-icon-cache-size",
            Display = new()
            {
                Name = "Icon cache size",
                Description = "Sets the maximum number of icons Explorer keeps cached, so it reloads them from disk less often when browsing folders with many files",
                GroupName = "Files and Folders",
                Icon = MaterialIcons.Cached,
                AddedInVersion = "26.06.08",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("MaxCachedIcons", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer" }, "MaxCachedIcons", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Windows default",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["MaxCachedIcons"] = Absent },
                },
                new SettingState
                {
                    Label = "Large (4,096 icons)",
                    Set = new Dictionary<string, StateValue> { ["MaxCachedIcons"] = Of("4096") },
                },
                new SettingState
                {
                    Label = "Very large (8,192 icons)",
                    Set = new Dictionary<string, StateValue> { ["MaxCachedIcons"] = Of("8192") },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-thumbnail-cache-cleanup",
            Display = new()
            {
                Name = "Automatic thumbnail cache cleanup",
                Description = "Lets Windows clear the thumbnail cache during automatic disk maintenance. Turn this off to keep cached thumbnails so Explorer does not have to regenerate them when you reopen folders",
                GroupName = "Files and Folders",
                Icon = FluentIcons.ImageMultiple,
                AddedInVersion = "26.06.08",
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("Autorun", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VolumeCaches\Thumbnail Cache", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\VolumeCaches\Thumbnail Cache" }, "Autorun", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Autorun"] = OneOf(3, 1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Autorun"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-folder-tips",
            Display = new()
            {
                Name = "Display file size information in folder tips",
                Description = "Shows total size and file count when hovering over folders",
                GroupName = "Files and Folders",
                Icon = FluentIcons.DocumentEndnote,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("FolderContentsInfoTip", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "FolderContentsInfoTip", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["FolderContentsInfoTip"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["FolderContentsInfoTip"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-full-path",
            Display = new()
            {
                Name = "Display the full path in the title bar",
                Description = "Shows complete directory path in window title instead of folder name only",
                GroupName = "Files and Folders",
                Icon = FluentIcons.PanelTopExpand,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("FullPath", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\CabinetState" }, "FullPath", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["FullPath"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["FullPath"] = Of(0).OrAbsent() },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-show-hidden-files",
            Display = new()
            {
                Name = "Show hidden files, folders & drives",
                Description = "Displays items with the hidden attribute set",
                GroupName = "Files and Folders",
                Icon = MaterialIcons.FileEyeOutline,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("Hidden", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "Hidden", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["Hidden"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["Hidden"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-hide-empty-drives",
            Display = new()
            {
                Name = "Hide empty drives",
                Description = "Hides drives with no media inserted like empty card readers or optical drives",
                GroupName = "Files and Folders",
                Icon = MaterialIcons.HarddiskRemove,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("HideDrivesWithNoMedia", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "HideDrivesWithNoMedia", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["HideDrivesWithNoMedia"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["HideDrivesWithNoMedia"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-show-file-ext",
            Display = new()
            {
                Name = "Show file extensions",
                Description = "Displays file type extensions (like .txt, .pdf) after file names",
                GroupName = "Files and Folders",
                Icon = FluentIcons.DocumentQuestionMark,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("HideFileExt", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "HideFileExt", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["HideFileExt"] = Of(0) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["HideFileExt"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-show-lnk-extension",
            Display = new()
            {
                Name = "Show .lnk file extension",
                Description = "Shows the .lnk extension on shortcut files when file extensions are enabled. Helps spot malicious shortcuts disguised as folders or documents. Once enabled, some Start Menu names may keep showing .lnk even after turning this off.",
                GroupName = "Files and Folders",
                Icon = FluentIcons.DocumentQuestionMark,
                AddedInVersion = "26.04.21",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("NeverShowExt", new[] { @"HKEY_CLASSES_ROOT\lnkfile" }, "NeverShowExt", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Links = new[] { new Link("explorer-customization-show-file-ext", LinkKind.Requires, "Enabled") },
                    Set = new Dictionary<string, StateValue> { ["NeverShowExt"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["NeverShowExt"] = Of("") },
                },
            },
        },
        new()
        {
            Id = "explorer-enable-photo-viewer",
            Display = new()
            {
                Name = "Enable Windows Photo Viewer",
                Description = "Restore the legacy Windows Photo Viewer and set it as the default program for common image file formats",
                GroupName = "File Associations",
                Icon = MaterialIcons.ImageOutline,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("", new[] { @"HKEY_CURRENT_USER\Software\Classes\.bmp" }, "", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { [""] = Of("PhotoViewer.FileAssoc.Tiff") },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

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
") },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { [""] = Absent },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

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
") },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-legacy-notepad",
            Display = new()
            {
                Name = "Use Legacy Notepad for text files",
                Description = "Makes legacy Notepad available as a file handler and disables the Store Notepad redirect. Requires Notepad (Legacy) capability to be installed",
                GroupName = "File Associations",
                Icon = FluentIcons.NotepadEdit,
                AddedInVersion = "26.04.03",
                IsSubjectivePreference = true,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("NoOpenWith", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Applications\notepad.exe" }, "NoOpenWith", RegistryValueKind.String),
                new RegTarget("UseFilter", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\notepad.exe" }, "UseFilter", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue>
                    {
                        ["NoOpenWith"] = Absent,
                        ["UseFilter"] = Of(0),
                    },
                    Effects = new Effect[] { new ScriptEffect(@"
$appPathsKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\App Paths\notepad.exe'
if (Test-Path $appPathsKey) {
    Remove-Item -Path $appPathsKey -Force
}", RunContext.User) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["NoOpenWith"] = Of(""),
                        ["UseFilter"] = Of(1),
                    },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-hide-merge-conflicts",
            Display = new()
            {
                Name = "Hide folder merge conflicts",
                Description = "Automatically merges folders with same name without confirmation dialog",
                GroupName = "Files and Folders",
                Icon = MaterialIcons.FolderAlert,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("HideMergeConflicts", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "HideMergeConflicts", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["HideMergeConflicts"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["HideMergeConflicts"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-hide-protected-files",
            Display = new()
            {
                Name = "Show protected operating system files",
                Description = "Displays system files marked with the SuperHidden attribute",
                GroupName = "Files and Folders",
                Icon = MaterialIcons.FileHidden,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("ShowSuperHidden", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "ShowSuperHidden", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { ["ShowSuperHidden"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ShowSuperHidden"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-separate-process",
            Display = new()
            {
                Name = "Launch folder windows in a separate process",
                Description = "Runs each Explorer window in its own process to prevent crashes affecting all windows",
                GroupName = "Files and Folders",
                Icon = MaterialIcons.WindowRestore,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("SeparateProcess", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "SeparateProcess", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { ["SeparateProcess"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["SeparateProcess"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-persist-browsers",
            Display = new()
            {
                Name = "Restore previous folder windows at logon",
                Description = "Reopens Explorer windows that were open when you last shut down or logged off",
                GroupName = "Files and Folders",
                Icon = FluentIcons.WindowAd,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("PersistBrowsers", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "PersistBrowsers", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { ["PersistBrowsers"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["PersistBrowsers"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-show-drive-letters",
            Display = new()
            {
                Name = "Show drive letters",
                Description = "Displays drive letters (C:, D:) before drive names in This PC",
                GroupName = "Files and Folders",
                Icon = MaterialIcons.AlphaCBox,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("ShowDriveLettersFirst", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "ShowDriveLettersFirst", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShowDriveLettersFirst"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ShowDriveLettersFirst"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-compressed-color",
            Display = new()
            {
                Name = "Show encrypted or compressed NTFS files in color",
                Description = "Displays encrypted files in green and compressed files in blue",
                GroupName = "Files and Folders",
                Icon = FluentIcons.DocumentLock,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("ShowEncryptCompressedColor", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "ShowEncryptCompressedColor", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["ShowEncryptCompressedColor"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ShowEncryptCompressedColor"] = Of(0).OrAbsent() },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-popup-descriptions",
            Display = new()
            {
                Name = "Show pop-up description for folder and desktop items",
                Description = "Displays tooltip with item details when hovering over files and folders",
                GroupName = "Files and Folders",
                Icon = MaterialIcons.TooltipText,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("ShowInfoTip", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "ShowInfoTip", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShowInfoTip"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ShowInfoTip"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-preview-handlers",
            Display = new()
            {
                Name = "Show preview handlers in preview pane",
                Description = "Enables file content preview when selecting files in Explorer",
                GroupName = "Files and Folders",
                Icon = MaterialIcons.TableEye,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("ShowPreviewHandlers", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "ShowPreviewHandlers", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShowPreviewHandlers"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ShowPreviewHandlers"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-status-bar",
            Display = new()
            {
                Name = "Show status bar",
                Description = "Displays bar at bottom showing item count and selected file sizes",
                GroupName = "Files and Folders",
                Icon = MaterialIcons.DockBottom,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("ShowStatusBar", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "ShowStatusBar", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShowStatusBar"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ShowStatusBar"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-disable-sync-provider-notifications",
            Display = new()
            {
                Name = "Show sync provider notifications",
                Description = "Displays cloud sync status notifications from OneDrive and other sync providers",
                GroupName = "Files and Folders",
                Icon = MaterialIcons.CloudSync,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("ShowSyncProviderNotifications", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "ShowSyncProviderNotifications", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShowSyncProviderNotifications"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["ShowSyncProviderNotifications"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-checkbox-select",
            Display = new()
            {
                Name = "Use check boxes to select items",
                Description = "Adds checkboxes next to items for easier multi-selection",
                GroupName = "Files and Folders",
                Icon = MaterialIcons.CheckboxMarked,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("AutoCheckSelect", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "AutoCheckSelect", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { ["AutoCheckSelect"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["AutoCheckSelect"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-sharing-wizard",
            Display = new()
            {
                Name = "Use sharing wizard",
                Description = "Shows simplified sharing dialog instead of advanced security permissions",
                GroupName = "Files and Folders",
                Icon = FluentIcons.ShareAndroid,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("SharingWizardOn", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "SharingWizardOn", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["SharingWizardOn"] = Absent },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["SharingWizardOn"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-typing-behavior",
            Display = new()
            {
                Name = "When typing into list view",
                Description = "Chooses whether typing selects matching items or searches automatically",
                GroupName = "Files and Folders",
                Icon = MaterialIcons.KeyboardOutline,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("TypeAhead", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "TypeAhead", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Select the typed item in the view",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["TypeAhead"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = "Automatically type into the Search Box",
                    Set = new Dictionary<string, StateValue> { ["TypeAhead"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-thispc-folder-desktop",
            Display = new()
            {
                Name = "Show Desktop in This PC",
                Description = "Displays the Desktop folder under This PC in File Explorer",
                GroupName = "This PC Folders",
                Icon = MaterialIcons.Monitor,
                AddedInVersion = "26.06.01",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("HiddenByDefault", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}" }, "HiddenByDefault", RegistryValueKind.DWord) { AppliesTo = new[] { BuildRange.Windows11 } },
                new RegTarget("KeyExists", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}" }, null, RegistryValueKind.None) { AppliesTo = new[] { BuildRange.Windows10 } },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    // Windows 10 shows the This PC folders by default (namespace key present); Windows 11 hides
                    // them (HiddenByDefault=1). The merged setting carries BOTH per-OS defaults as build-scoped
                    // WindowsDefault roles so bulk "Reset to Defaults" resolves the correct one on the live OS.
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows10 } } },
                    Set = new Dictionary<string, StateValue> { ["HiddenByDefault"] = Of(0), ["KeyExists"] = Exists },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows11 } } },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["HiddenByDefault"] = Of(1), ["KeyExists"] = Absent },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-thispc-folder-documents",
            Display = new()
            {
                Name = "Show Documents in This PC",
                Description = "Displays the Documents folder under This PC in File Explorer",
                GroupName = "This PC Folders",
                Icon = MaterialIcons.FileDocument,
                AddedInVersion = "26.06.01",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("HiddenByDefault", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{D3162B92-9365-467A-956B-92703ACA08AF}", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{D3162B92-9365-467A-956B-92703ACA08AF}" }, "HiddenByDefault", RegistryValueKind.DWord) { AppliesTo = new[] { BuildRange.Windows11 } },
                new RegTarget("KeyExists", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{D3162B92-9365-467A-956B-92703ACA08AF}", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{D3162B92-9365-467A-956B-92703ACA08AF}" }, null, RegistryValueKind.None) { AppliesTo = new[] { BuildRange.Windows10 } },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    // Windows 10 shows the This PC folders by default (namespace key present); Windows 11 hides
                    // them (HiddenByDefault=1). The merged setting carries BOTH per-OS defaults as build-scoped
                    // WindowsDefault roles so bulk "Reset to Defaults" resolves the correct one on the live OS.
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows10 } } },
                    Set = new Dictionary<string, StateValue> { ["HiddenByDefault"] = Of(0), ["KeyExists"] = Exists },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows11 } } },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["HiddenByDefault"] = Of(1), ["KeyExists"] = Absent },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-thispc-folder-downloads",
            Display = new()
            {
                Name = "Show Downloads in This PC",
                Description = "Displays the Downloads folder under This PC in File Explorer",
                GroupName = "This PC Folders",
                Icon = MaterialIcons.Download,
                AddedInVersion = "26.06.01",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("HiddenByDefault", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{088E3905-0323-4B02-9826-5D99428E115F}", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{088E3905-0323-4B02-9826-5D99428E115F}" }, "HiddenByDefault", RegistryValueKind.DWord) { AppliesTo = new[] { BuildRange.Windows11 } },
                new RegTarget("KeyExists", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{088E3905-0323-4B02-9826-5D99428E115F}", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{088E3905-0323-4B02-9826-5D99428E115F}" }, null, RegistryValueKind.None) { AppliesTo = new[] { BuildRange.Windows10 } },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    // Windows 10 shows the This PC folders by default (namespace key present); Windows 11 hides
                    // them (HiddenByDefault=1). The merged setting carries BOTH per-OS defaults as build-scoped
                    // WindowsDefault roles so bulk "Reset to Defaults" resolves the correct one on the live OS.
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows10 } } },
                    Set = new Dictionary<string, StateValue> { ["HiddenByDefault"] = Of(0), ["KeyExists"] = Exists },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows11 } } },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["HiddenByDefault"] = Of(1), ["KeyExists"] = Absent },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-thispc-folder-music",
            Display = new()
            {
                Name = "Show Music in This PC",
                Description = "Displays the Music folder under This PC in File Explorer",
                GroupName = "This PC Folders",
                Icon = MaterialIcons.Music,
                AddedInVersion = "26.06.01",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("HiddenByDefault", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{3DFDF296-DBEC-4FB4-81D1-6A3438BCF4DE}", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{3DFDF296-DBEC-4FB4-81D1-6A3438BCF4DE}" }, "HiddenByDefault", RegistryValueKind.DWord) { AppliesTo = new[] { BuildRange.Windows11 } },
                new RegTarget("KeyExists", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{3DFDF296-DBEC-4FB4-81D1-6A3438BCF4DE}", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{3DFDF296-DBEC-4FB4-81D1-6A3438BCF4DE}" }, null, RegistryValueKind.None) { AppliesTo = new[] { BuildRange.Windows10 } },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    // Windows 10 shows the This PC folders by default (namespace key present); Windows 11 hides
                    // them (HiddenByDefault=1). The merged setting carries BOTH per-OS defaults as build-scoped
                    // WindowsDefault roles so bulk "Reset to Defaults" resolves the correct one on the live OS.
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows10 } } },
                    Set = new Dictionary<string, StateValue> { ["HiddenByDefault"] = Of(0), ["KeyExists"] = Exists },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows11 } } },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["HiddenByDefault"] = Of(1), ["KeyExists"] = Absent },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-thispc-folder-pictures",
            Display = new()
            {
                Name = "Show Pictures in This PC",
                Description = "Displays the Pictures folder under This PC in File Explorer",
                GroupName = "This PC Folders",
                Icon = MaterialIcons.Image,
                AddedInVersion = "26.06.01",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("HiddenByDefault", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{24AD3AD4-A569-4530-98E1-AB02F9417AA8}", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{24AD3AD4-A569-4530-98E1-AB02F9417AA8}" }, "HiddenByDefault", RegistryValueKind.DWord) { AppliesTo = new[] { BuildRange.Windows11 } },
                new RegTarget("KeyExists", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{24AD3AD4-A569-4530-98E1-AB02F9417AA8}", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{24AD3AD4-A569-4530-98E1-AB02F9417AA8}" }, null, RegistryValueKind.None) { AppliesTo = new[] { BuildRange.Windows10 } },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    // Windows 10 shows the This PC folders by default (namespace key present); Windows 11 hides
                    // them (HiddenByDefault=1). The merged setting carries BOTH per-OS defaults as build-scoped
                    // WindowsDefault roles so bulk "Reset to Defaults" resolves the correct one on the live OS.
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows10 } } },
                    Set = new Dictionary<string, StateValue> { ["HiddenByDefault"] = Of(0), ["KeyExists"] = Exists },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows11 } } },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["HiddenByDefault"] = Of(1), ["KeyExists"] = Absent },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-thispc-folder-videos",
            Display = new()
            {
                Name = "Show Videos in This PC",
                Description = "Displays the Videos folder under This PC in File Explorer",
                GroupName = "This PC Folders",
                Icon = MaterialIcons.Video,
                AddedInVersion = "26.06.01",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("HiddenByDefault", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{F86FA3AB-70D2-4FC7-9C99-FCBF05467F3A}", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{F86FA3AB-70D2-4FC7-9C99-FCBF05467F3A}" }, "HiddenByDefault", RegistryValueKind.DWord) { AppliesTo = new[] { BuildRange.Windows11 } },
                new RegTarget("KeyExists", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{F86FA3AB-70D2-4FC7-9C99-FCBF05467F3A}", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{F86FA3AB-70D2-4FC7-9C99-FCBF05467F3A}" }, null, RegistryValueKind.None) { AppliesTo = new[] { BuildRange.Windows10 } },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    // Windows 10 shows the This PC folders by default (namespace key present); Windows 11 hides
                    // them (HiddenByDefault=1). The merged setting carries BOTH per-OS defaults as build-scoped
                    // WindowsDefault roles so bulk "Reset to Defaults" resolves the correct one on the live OS.
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows10 } } },
                    Set = new Dictionary<string, StateValue> { ["HiddenByDefault"] = Of(0), ["KeyExists"] = Exists },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows11 } } },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["HiddenByDefault"] = Of(1), ["KeyExists"] = Absent },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-3d-objects",
            Display = new()
            {
                Name = "Show 3D Objects",
                Description = "Display the 3D Objects folder alongside Documents, Pictures, and other default folders",
                GroupName = "This PC Folders",
                Icon = MaterialIcons.Printer3d,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows10 } },
            Targets = new Target[]
            {
                new RegTarget("KeyExists", new[] { @"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{0DB7E03F-FC29-4DC6-9020-FF41B59E513A}", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\MyComputer\NameSpace\{0DB7E03F-FC29-4DC6-9020-FF41B59E513A}" }, null, RegistryValueKind.None),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["KeyExists"] = Exists },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["KeyExists"] = Absent },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-home-folder",
            Display = new()
            {
                Name = "Show Home Folder",
                Description = "Display the Home folder in the navigation pane as a shortcut to your user profile folder",
                GroupName = "Navigation Pane",
                Icon = MaterialIcons.Home,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("System.IsPinnedToNameSpaceTree", new[] { @"HKEY_CURRENT_USER\Software\Classes\CLSID\{f874310e-b6b7-47dc-bc84-b9e6b38f5903}" }, "System.IsPinnedToNameSpaceTree", RegistryValueKind.DWord),
                new RegTarget("{f874310e-b6b7-47dc-bc84-b9e6b38f5903}", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\NonEnum" }, "{f874310e-b6b7-47dc-bc84-b9e6b38f5903}", RegistryValueKind.DWord),
                new RegTarget("HiddenByDefault", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{f874310e-b6b7-47dc-bc84-b9e6b38f5903}" }, "HiddenByDefault", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["System.IsPinnedToNameSpaceTree"] = Of(1).OrAbsent(),
                        ["{f874310e-b6b7-47dc-bc84-b9e6b38f5903}"] = Of(0).OrAbsent(),
                        ["HiddenByDefault"] = Of(0).OrAbsent(),
                    },
                    ResetSet = new Dictionary<string, StateValue>
                    {
                        ["System.IsPinnedToNameSpaceTree"] = Absent,
                        ["{f874310e-b6b7-47dc-bc84-b9e6b38f5903}"] = Absent,
                        ["HiddenByDefault"] = Absent,
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["System.IsPinnedToNameSpaceTree"] = Of(0),
                        ["{f874310e-b6b7-47dc-bc84-b9e6b38f5903}"] = Of(1).OrAbsent(),
                        ["HiddenByDefault"] = Of(1).OrAbsent(),
                    },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-gallery",
            Display = new()
            {
                Name = "Show Gallery",
                Description = "Display the Gallery folder in the navigation pane for quick access to all your photos and videos",
                GroupName = "Navigation Pane",
                Icon = MaterialIcons.ImageMultiple,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Targets = new Target[]
            {
                new RegTarget("System.IsPinnedToNameSpaceTree", new[] { @"HKEY_CURRENT_USER\Software\Classes\CLSID\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}" }, "System.IsPinnedToNameSpaceTree", RegistryValueKind.DWord),
                new RegTarget("{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\NonEnum" }, "{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}", RegistryValueKind.DWord),
                new RegTarget("HiddenByDefault", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}" }, "HiddenByDefault", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["System.IsPinnedToNameSpaceTree"] = Of(1).OrAbsent(),
                        ["{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}"] = Of(0).OrAbsent(),
                        ["HiddenByDefault"] = Of(0).OrAbsent(),
                    },
                    ResetSet = new Dictionary<string, StateValue>
                    {
                        ["System.IsPinnedToNameSpaceTree"] = Absent,
                        ["{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}"] = Absent,
                        ["HiddenByDefault"] = Absent,
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["System.IsPinnedToNameSpaceTree"] = Of(0),
                        ["{e88865ea-0e1c-4e20-9aa6-edcd0212c87c}"] = Of(1).OrAbsent(),
                        ["HiddenByDefault"] = Of(1).OrAbsent(),
                    },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-show-availability-status",
            Display = new()
            {
                Name = "Always show availability status",
                Description = "Shows cloud sync status icons for OneDrive files in navigation pane",
                GroupName = "Navigation Pane",
                Icon = MaterialIcons.ArchiveSync,
            },
            Targets = new Target[]
            {
                new RegTarget("NavPaneShowAllCloudStates", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "NavPaneShowAllCloudStates", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { ["NavPaneShowAllCloudStates"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["NavPaneShowAllCloudStates"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-nav-expand-current",
            Display = new()
            {
                Name = "Expand to open folder",
                Description = "Automatically expands navigation tree to highlight current folder location",
                GroupName = "Navigation Pane",
                Icon = MaterialIcons.FileTree,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("NavPaneExpandToCurrentFolder", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "NavPaneExpandToCurrentFolder", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { ["NavPaneExpandToCurrentFolder"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["NavPaneExpandToCurrentFolder"] = Of(0).OrAbsent() },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-nav-show-all-folders",
            Display = new()
            {
                Name = "Show all folders",
                Description = "Shows all folders in the navigation pane",
                GroupName = "Navigation Pane",
                Icon = MaterialIcons.FolderMultiple,
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("NavPaneShowAllFolders", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" }, "NavPaneShowAllFolders", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Links = new[]
                    {
                        new Link("explorer-customization-nav-saf-desktop", LinkKind.Enables, "Enabled") { ReverseCascade = false, Force = true },
                        new Link("explorer-customization-nav-saf-documents", LinkKind.Enables, "Enabled") { ReverseCascade = false, Force = true },
                        new Link("explorer-customization-nav-saf-downloads", LinkKind.Enables, "Enabled") { ReverseCascade = false, Force = true },
                        new Link("explorer-customization-nav-saf-music", LinkKind.Enables, "Enabled") { ReverseCascade = false, Force = true },
                        new Link("explorer-customization-nav-saf-pictures", LinkKind.Enables, "Enabled") { ReverseCascade = false, Force = true },
                        new Link("explorer-customization-nav-saf-videos", LinkKind.Enables, "Enabled") { ReverseCascade = false, Force = true },
                        new Link("explorer-customization-nav-show-libraries", LinkKind.Enables, "Enabled") { ReverseCascade = false, Force = true },
                    },
                    Set = new Dictionary<string, StateValue> { ["NavPaneShowAllFolders"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["NavPaneShowAllFolders"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-nav-saf-desktop",
            Display = new()
            {
                Name = "Show Desktop folder",
                Description = "Shows the Desktop folder in the navigation pane",
                GroupName = "Navigation Pane",
                Icon = MaterialIcons.Monitor,
                AddedInVersion = "26.04.07",
                IsSubjectivePreference = true,
            },
            UiParentId = "explorer-customization-nav-show-all-folders",
            Targets = new Target[]
            {
                new RegTarget("{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\NonEnum" }, "{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}", RegistryValueKind.DWord),
                new RegTarget("HiddenByDefault", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}" }, "HiddenByDefault", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}"] = Absent,
                        ["HiddenByDefault"] = Absent,
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}"] = Of(1).OrAbsent(),
                        ["HiddenByDefault"] = Of(1).OrAbsent(),
                    },
                    ResetSet = new Dictionary<string, StateValue>
                    {
                        ["{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}"] = Absent,
                        ["HiddenByDefault"] = Absent,
                    },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-nav-saf-documents",
            Display = new()
            {
                Name = "Show Documents folder",
                Description = "Shows the Documents folder in the navigation pane",
                GroupName = "Navigation Pane",
                Icon = MaterialIcons.FileDocument,
                AddedInVersion = "26.04.07",
                IsSubjectivePreference = true,
            },
            UiParentId = "explorer-customization-nav-show-all-folders",
            Targets = new Target[]
            {
                new RegTarget("{A8CDFF1C-4878-43be-B5FD-F8091C1C60D0}", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\NonEnum" }, "{A8CDFF1C-4878-43be-B5FD-F8091C1C60D0}", RegistryValueKind.DWord),
                new RegTarget("HiddenByDefault", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{A8CDFF1C-4878-43be-B5FD-F8091C1C60D0}" }, "HiddenByDefault", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["{A8CDFF1C-4878-43be-B5FD-F8091C1C60D0}"] = Absent,
                        ["HiddenByDefault"] = Absent,
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["{A8CDFF1C-4878-43be-B5FD-F8091C1C60D0}"] = Of(1).OrAbsent(),
                        ["HiddenByDefault"] = Of(1).OrAbsent(),
                    },
                    ResetSet = new Dictionary<string, StateValue>
                    {
                        ["{A8CDFF1C-4878-43be-B5FD-F8091C1C60D0}"] = Absent,
                        ["HiddenByDefault"] = Absent,
                    },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-nav-saf-downloads",
            Display = new()
            {
                Name = "Show Downloads folder",
                Description = "Shows the Downloads folder in the navigation pane",
                GroupName = "Navigation Pane",
                Icon = MaterialIcons.Download,
                AddedInVersion = "26.04.07",
                IsSubjectivePreference = true,
            },
            UiParentId = "explorer-customization-nav-show-all-folders",
            Targets = new Target[]
            {
                new RegTarget("{374DE290-123F-4565-9164-39C4925E467B}", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\NonEnum" }, "{374DE290-123F-4565-9164-39C4925E467B}", RegistryValueKind.DWord),
                new RegTarget("HiddenByDefault", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{374DE290-123F-4565-9164-39C4925E467B}" }, "HiddenByDefault", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["{374DE290-123F-4565-9164-39C4925E467B}"] = Absent,
                        ["HiddenByDefault"] = Absent,
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["{374DE290-123F-4565-9164-39C4925E467B}"] = Of(1).OrAbsent(),
                        ["HiddenByDefault"] = Of(1).OrAbsent(),
                    },
                    ResetSet = new Dictionary<string, StateValue>
                    {
                        ["{374DE290-123F-4565-9164-39C4925E467B}"] = Absent,
                        ["HiddenByDefault"] = Absent,
                    },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-nav-saf-music",
            Display = new()
            {
                Name = "Show Music folder",
                Description = "Shows the Music folder in the navigation pane",
                GroupName = "Navigation Pane",
                Icon = MaterialIcons.Music,
                AddedInVersion = "26.04.07",
                IsSubjectivePreference = true,
            },
            UiParentId = "explorer-customization-nav-show-all-folders",
            Targets = new Target[]
            {
                new RegTarget("{1CF1260C-4DD0-4ebb-811F-33C572699FDE}", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\NonEnum" }, "{1CF1260C-4DD0-4ebb-811F-33C572699FDE}", RegistryValueKind.DWord),
                new RegTarget("HiddenByDefault", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{1CF1260C-4DD0-4ebb-811F-33C572699FDE}" }, "HiddenByDefault", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["{1CF1260C-4DD0-4ebb-811F-33C572699FDE}"] = Absent,
                        ["HiddenByDefault"] = Absent,
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["{1CF1260C-4DD0-4ebb-811F-33C572699FDE}"] = Of(1).OrAbsent(),
                        ["HiddenByDefault"] = Of(1).OrAbsent(),
                    },
                    ResetSet = new Dictionary<string, StateValue>
                    {
                        ["{1CF1260C-4DD0-4ebb-811F-33C572699FDE}"] = Absent,
                        ["HiddenByDefault"] = Absent,
                    },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-nav-saf-pictures",
            Display = new()
            {
                Name = "Show Pictures folder",
                Description = "Shows the Pictures folder in the navigation pane",
                GroupName = "Navigation Pane",
                Icon = MaterialIcons.Image,
                AddedInVersion = "26.04.07",
                IsSubjectivePreference = true,
            },
            UiParentId = "explorer-customization-nav-show-all-folders",
            Targets = new Target[]
            {
                new RegTarget("{3ADD1653-EB32-4cb0-BBD7-DFA0ABB5ACCA}", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\NonEnum" }, "{3ADD1653-EB32-4cb0-BBD7-DFA0ABB5ACCA}", RegistryValueKind.DWord),
                new RegTarget("HiddenByDefault", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{3ADD1653-EB32-4cb0-BBD7-DFA0ABB5ACCA}" }, "HiddenByDefault", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["{3ADD1653-EB32-4cb0-BBD7-DFA0ABB5ACCA}"] = Absent,
                        ["HiddenByDefault"] = Absent,
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["{3ADD1653-EB32-4cb0-BBD7-DFA0ABB5ACCA}"] = Of(1).OrAbsent(),
                        ["HiddenByDefault"] = Of(1).OrAbsent(),
                    },
                    ResetSet = new Dictionary<string, StateValue>
                    {
                        ["{3ADD1653-EB32-4cb0-BBD7-DFA0ABB5ACCA}"] = Absent,
                        ["HiddenByDefault"] = Absent,
                    },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-nav-saf-videos",
            Display = new()
            {
                Name = "Show Videos folder",
                Description = "Shows the Videos folder in the navigation pane",
                GroupName = "Navigation Pane",
                Icon = MaterialIcons.Video,
                AddedInVersion = "26.04.07",
                IsSubjectivePreference = true,
            },
            UiParentId = "explorer-customization-nav-show-all-folders",
            Targets = new Target[]
            {
                new RegTarget("{A0953C92-50DC-43bf-BE83-3742FED03C9C}", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\NonEnum" }, "{A0953C92-50DC-43bf-BE83-3742FED03C9C}", RegistryValueKind.DWord),
                new RegTarget("HiddenByDefault", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{A0953C92-50DC-43bf-BE83-3742FED03C9C}" }, "HiddenByDefault", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["{A0953C92-50DC-43bf-BE83-3742FED03C9C}"] = Absent,
                        ["HiddenByDefault"] = Absent,
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["{A0953C92-50DC-43bf-BE83-3742FED03C9C}"] = Of(1).OrAbsent(),
                        ["HiddenByDefault"] = Of(1).OrAbsent(),
                    },
                    ResetSet = new Dictionary<string, StateValue>
                    {
                        ["{A0953C92-50DC-43bf-BE83-3742FED03C9C}"] = Absent,
                        ["HiddenByDefault"] = Absent,
                    },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-nav-show-libraries",
            Display = new()
            {
                Name = "Show Libraries",
                Description = "Pins the Libraries folder as a top-level item in the navigation pane. Has no effect when Show All Folders is enabled, as Libraries becomes part of the folder tree instead",
                GroupName = "Navigation Pane",
                Icon = MaterialIcons.FolderTable,
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("System.IsPinnedToNameSpaceTree", new[] { @"HKEY_CURRENT_USER\Software\Classes\CLSID\{031E4825-7B94-4dc3-B131-E946B44C8DD5}" }, "System.IsPinnedToNameSpaceTree", RegistryValueKind.DWord),
                new RegTarget("{031E4825-7B94-4dc3-B131-E946B44C8DD5}", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\NonEnum" }, "{031E4825-7B94-4dc3-B131-E946B44C8DD5}", RegistryValueKind.DWord),
                new RegTarget("HiddenByDefault", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\{031E4825-7B94-4dc3-B131-E946B44C8DD5}" }, "HiddenByDefault", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue>
                    {
                        ["System.IsPinnedToNameSpaceTree"] = Of(1),
                        ["{031E4825-7B94-4dc3-B131-E946B44C8DD5}"] = Of(0),
                        ["HiddenByDefault"] = Of(0),
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["System.IsPinnedToNameSpaceTree"] = Of(0),
                        ["{031E4825-7B94-4dc3-B131-E946B44C8DD5}"] = Of(1).OrAbsent(),
                        ["HiddenByDefault"] = Of(1).OrAbsent(),
                    },
                    ResetSet = new Dictionary<string, StateValue>
                    {
                        ["{031E4825-7B94-4dc3-B131-E946B44C8DD5}"] = Absent,
                        ["HiddenByDefault"] = Absent,
                    },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-duplicate-removable-drives",
            Display = new()
            {
                Name = "Show Duplicate Removable Drives",
                Description = "Show removable drives as separate entries in the navigation pane in addition to under This PC",
                GroupName = "Navigation Pane",
                Icon = MaterialIcons.Usb,
                AddedInVersion = "26.04.09",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("KeyExists", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\DelegateFolders\{F5FB2C77-0E2F-4A16-A381-3E560C68BC83}", @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Explorer\Desktop\NameSpace\DelegateFolders\{F5FB2C77-0E2F-4A16-A381-3E560C68BC83}" }, null, RegistryValueKind.None),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["KeyExists"] = Exists },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["KeyExists"] = Absent },
                },
            },
        },
        new()
        {
            Id = "explorer-long-file-paths",
            Display = new()
            {
                Name = "Enable Long File Paths",
                Description = "Enables support for file paths with up to 32,767 characters instead of the traditional 260-character limit",
                GroupName = "Files and Folders",
                Icon = MaterialIcons.ScriptTextOutline,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("LongPathsEnabled", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FileSystem" }, "LongPathsEnabled", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["LongPathsEnabled"] = Of(1) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["LongPathsEnabled"] = Of(0) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-netplwiz-auto-login",
            Display = new()
            {
                Name = "Show Auto-Login Option in User Accounts",
                Description = "Shows the classic 'Users must enter a user name and password to use this computer' checkbox in the User Accounts (netplwiz) window, allowing you to configure automatic logon through the standard Windows UI",
                GroupName = "Network",
                Icon = FluentIcons.PersonKey,
                AddedInVersion = "26.04.03",
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("DevicePasswordLessBuildVersion", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\PasswordLess\Device" }, "DevicePasswordLessBuildVersion", RegistryValueKind.DWord),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { ["DevicePasswordLessBuildVersion"] = Of(0) },
                },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["DevicePasswordLessBuildVersion"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-short-date",
            Display = new()
            {
                Name = "Short Date Format",
                Description = "Choose the format used to display short dates across Windows",
                GroupName = "Regional Settings",
                Icon = MaterialIcons.CalendarMonth,
                AddedInVersion = "26.04.10",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("intl") },
            Targets = new Target[]
            {
                new RegTarget("sShortDate", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "sShortDate", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "M/d/yyyy",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["sShortDate"] = Of("M/d/yyyy").OrAbsent() },
                },
                new SettingState
                {
                    Label = "dd/MM/yyyy",
                    Set = new Dictionary<string, StateValue> { ["sShortDate"] = Of("dd/MM/yyyy") },
                },
                new SettingState
                {
                    Label = "yyyy-MM-dd",
                    Set = new Dictionary<string, StateValue> { ["sShortDate"] = Of("yyyy-MM-dd") },
                },
                new SettingState
                {
                    Label = "yyyy/MM/dd",
                    Set = new Dictionary<string, StateValue> { ["sShortDate"] = Of("yyyy/MM/dd") },
                },
                new SettingState
                {
                    Label = "dd MMM yyyy",
                    Set = new Dictionary<string, StateValue> { ["sShortDate"] = Of("dd MMM yyyy") },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-first-day-of-week",
            Display = new()
            {
                Name = "First Day of Week",
                Description = "Choose which day is displayed as the first day of the week in calendars",
                GroupName = "Regional Settings",
                Icon = MaterialIcons.CalendarWeekBegin,
                AddedInVersion = "26.04.10",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("intl") },
            Targets = new Target[]
            {
                new RegTarget("iFirstDayOfWeek", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "iFirstDayOfWeek", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Sunday",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["iFirstDayOfWeek"] = Of("6").OrAbsent() },
                },
                new SettingState
                {
                    Label = "Monday",
                    Set = new Dictionary<string, StateValue> { ["iFirstDayOfWeek"] = Of("0") },
                },
                new SettingState
                {
                    Label = "Tuesday",
                    Set = new Dictionary<string, StateValue> { ["iFirstDayOfWeek"] = Of("1") },
                },
                new SettingState
                {
                    Label = "Wednesday",
                    Set = new Dictionary<string, StateValue> { ["iFirstDayOfWeek"] = Of("2") },
                },
                new SettingState
                {
                    Label = "Thursday",
                    Set = new Dictionary<string, StateValue> { ["iFirstDayOfWeek"] = Of("3") },
                },
                new SettingState
                {
                    Label = "Friday",
                    Set = new Dictionary<string, StateValue> { ["iFirstDayOfWeek"] = Of("4") },
                },
                new SettingState
                {
                    Label = "Saturday",
                    Set = new Dictionary<string, StateValue> { ["iFirstDayOfWeek"] = Of("5") },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-number-decimal",
            Display = new()
            {
                Name = "Number Decimal Symbol",
                Description = "Choose the symbol used to separate whole numbers from decimals in number formatting",
                GroupName = "Regional Settings",
                Icon = MaterialIcons.Numeric,
                AddedInVersion = "26.04.10",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("intl") },
            Targets = new Target[]
            {
                new RegTarget("sDecimal", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "sDecimal", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = ". (Period)",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["sDecimal"] = Of(".").OrAbsent() },
                },
                new SettingState
                {
                    Label = ", (Comma)",
                    Set = new Dictionary<string, StateValue> { ["sDecimal"] = Of(",") },
                },
                new SettingState
                {
                    Label = "  (Space)",
                    Set = new Dictionary<string, StateValue> { ["sDecimal"] = Of(" ") },
                },
                new SettingState
                {
                    Label = "' (Apostrophe)",
                    Set = new Dictionary<string, StateValue> { ["sDecimal"] = Of("'") },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-list-separator",
            Display = new()
            {
                Name = "List Separator",
                Description = "Choose the character used to separate items in lists, such as in CSV exports and formulas",
                GroupName = "Regional Settings",
                Icon = MaterialIcons.FormatListBulleted,
                AddedInVersion = "26.04.10",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("intl") },
            Targets = new Target[]
            {
                new RegTarget("sList", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "sList", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = ", (Comma)",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["sList"] = Of(",").OrAbsent() },
                },
                new SettingState
                {
                    Label = "; (Semicolon)",
                    Set = new Dictionary<string, StateValue> { ["sList"] = Of(";") },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-measurement-system",
            Display = new()
            {
                Name = "Measurement System",
                Description = "Choose whether Windows uses the metric or U.S. imperial measurement system",
                GroupName = "Regional Settings",
                Icon = MaterialIcons.RulerSquare,
                AddedInVersion = "26.04.10",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("intl") },
            Targets = new Target[]
            {
                new RegTarget("iMeasure", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "iMeasure", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Metric",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["iMeasure"] = Of("0").OrAbsent() },
                },
                new SettingState
                {
                    Label = "U.S. (Imperial)",
                    Set = new Dictionary<string, StateValue> { ["iMeasure"] = Of("1") },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-currency-decimal",
            Display = new()
            {
                Name = "Currency Decimal Symbol",
                Description = "Choose the symbol used to separate whole numbers from decimals in currency formatting",
                GroupName = "Regional Settings",
                Icon = MaterialIcons.CurrencySign,
                AddedInVersion = "26.04.10",
                IsSubjectivePreference = true,
            },
            Apply = new() { Restart = new RestartProcess("intl") },
            Targets = new Target[]
            {
                new RegTarget("sMonDecimalSep", new[] { @"HKEY_CURRENT_USER\Control Panel\International" }, "sMonDecimalSep", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = ". (Period)",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["sMonDecimalSep"] = Of(".").OrAbsent() },
                },
                new SettingState
                {
                    Label = ", (Comma)",
                    Set = new Dictionary<string, StateValue> { ["sMonDecimalSep"] = Of(",") },
                },
                new SettingState
                {
                    Label = "  (Space)",
                    Set = new Dictionary<string, StateValue> { ["sMonDecimalSep"] = Of(" ") },
                },
                new SettingState
                {
                    Label = "' (Apostrophe)",
                    Set = new Dictionary<string, StateValue> { ["sMonDecimalSep"] = Of("'") },
                },
            },
        },
        new()
        {
            Id = "explorer-autoplay",
            Display = new()
            {
                Name = "Autoplay",
                Description = "Allow Windows to automatically open a dialog or run programs when you insert a USB drive, DVD, or SD card",
                GroupName = "Devices and Peripherals",
                Icon = MaterialIcons.PlayBox,
                AddedInVersion = "26.04.24",
            },
            Targets = new Target[]
            {
                new RegTarget("DisableAutoplay", new[] { @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\AutoplayHandlers" }, "DisableAutoplay", RegistryValueKind.DWord),
                new RegTarget("NoDriveTypeAutoRun", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\Explorer" }, "NoDriveTypeAutoRun", RegistryValueKind.DWord) { IsGroupPolicy = true },
            },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["DisableAutoplay"] = Of(0).OrAbsent(),
                        ["NoDriveTypeAutoRun"] = Absent,
                    },
                },
                new SettingState
                {
                    Label = "Disabled",
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["DisableAutoplay"] = Of(1),
                        ["NoDriveTypeAutoRun"] = Of(255),
                    },
                },
            },
        },
    };
}
