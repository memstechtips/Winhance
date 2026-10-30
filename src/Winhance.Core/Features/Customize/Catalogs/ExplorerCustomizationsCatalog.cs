using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using static Winhance.Core.Features.Common.Catalog.StateValue;
using Winhance.Core.Features.Common.Localization;

namespace Winhance.Core.Features.Customize.Catalogs;

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
                Name = LocKey.Setting.ExplorerCustomizationShortcutSuffix.Name,
                Description = LocKey.Setting.ExplorerCustomizationShortcutSuffix.Description,
                GroupName = LocKey.SettingGroup.Desktop,
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
                    Label = LocKey.Setting.ExplorerCustomizationShortcutSuffix.Option0,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["link"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationShortcutSuffix.Option1,
                    Set = new Dictionary<string, StateValue> { ["link"] = Of(new byte[] { 0x00, 0x00, 0x00, 0x00 }) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-shortcut-arrow",
            Display = new()
            {
                Name = LocKey.Setting.ExplorerCustomizationShortcutArrow.Name,
                Description = LocKey.Setting.ExplorerCustomizationShortcutArrow.Description,
                GroupName = LocKey.SettingGroup.Desktop,
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
                    Label = LocKey.Setting.ExplorerCustomizationShortcutArrow.Option0,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["29"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationShortcutArrow.Option1,
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
                Name = LocKey.Setting.ExplorerCustomizationDesktopIconThisPc.Name,
                Description = LocKey.Setting.ExplorerCustomizationDesktopIconThisPc.Description,
                GroupName = LocKey.SettingGroup.Desktop,
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
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["{20D04FE0-3AEA-1069-A2D8-08002B30309D}"] = Of(0) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["{20D04FE0-3AEA-1069-A2D8-08002B30309D}"] = Of(1) },
                    ResetSet = new Dictionary<string, StateValue> { ["{20D04FE0-3AEA-1069-A2D8-08002B30309D}"] = Absent },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-desktop-icon-recycle-bin",
            Display = new()
            {
                Name = LocKey.Setting.ExplorerCustomizationDesktopIconRecycleBin.Name,
                Description = LocKey.Setting.ExplorerCustomizationDesktopIconRecycleBin.Description,
                GroupName = LocKey.SettingGroup.Desktop,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["{645FF040-5081-101B-9F08-00AA002F954E}"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationDesktopIconUsersFiles.Name,
                Description = LocKey.Setting.ExplorerCustomizationDesktopIconUsersFiles.Description,
                GroupName = LocKey.SettingGroup.Desktop,
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
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["{59031A47-3F72-44A7-89C5-5595FE6B30EE}"] = Of(0) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["{59031A47-3F72-44A7-89C5-5595FE6B30EE}"] = Of(1) },
                    ResetSet = new Dictionary<string, StateValue> { ["{59031A47-3F72-44A7-89C5-5595FE6B30EE}"] = Absent },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-desktop-icon-control-panel",
            Display = new()
            {
                Name = LocKey.Setting.ExplorerCustomizationDesktopIconControlPanel.Name,
                Description = LocKey.Setting.ExplorerCustomizationDesktopIconControlPanel.Description,
                GroupName = LocKey.SettingGroup.Desktop,
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
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["{5399E694-6CE5-4D6C-8FCE-1D8870FDCBA0}"] = Of(0) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["{5399E694-6CE5-4D6C-8FCE-1D8870FDCBA0}"] = Of(1) },
                    ResetSet = new Dictionary<string, StateValue> { ["{5399E694-6CE5-4D6C-8FCE-1D8870FDCBA0}"] = Absent },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-desktop-icon-network",
            Display = new()
            {
                Name = LocKey.Setting.ExplorerCustomizationDesktopIconNetwork.Name,
                Description = LocKey.Setting.ExplorerCustomizationDesktopIconNetwork.Description,
                GroupName = LocKey.SettingGroup.Desktop,
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
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}"] = Of(0) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}"] = Of(1) },
                    ResetSet = new Dictionary<string, StateValue> { ["{F02C1A0D-BE21-4350-88B0-7367FC96EF3C}"] = Absent },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-context-menu",
            Display = new()
            {
                Name = LocKey.Setting.ExplorerCustomizationContextMenu.Name,
                Description = LocKey.Setting.ExplorerCustomizationContextMenu.Description,
                GroupName = LocKey.SettingGroup.ContextMenu,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { [""] = Of("") },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerTakeOwnership.Name,
                Description = LocKey.Setting.ExplorerTakeOwnership.Description,
                GroupName = LocKey.SettingGroup.ContextMenu,
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
                    Label = LocKey.Common.Enabled,
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
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerContextMenuToggleExtensions.Name,
                Description = LocKey.Setting.ExplorerContextMenuToggleExtensions.Description,
                GroupName = LocKey.SettingGroup.ContextMenu,
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
                    Label = LocKey.Common.Enabled,
                    Links = new[] { new Link("explorer-customization-context-menu", LinkKind.Requires, LocKey.Common.Enabled) },
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
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerContextMenuWindowsTerminal.Name,
                Description = LocKey.Setting.ExplorerContextMenuWindowsTerminal.Description,
                GroupName = LocKey.SettingGroup.ContextMenu,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["{9F156763-7844-4DC4-B2B1-901F640F5155}"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerContextMenuSfc.Name,
                Description = LocKey.Setting.ExplorerContextMenuSfc.Description,
                GroupName = LocKey.SettingGroup.ContextMenu,
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
                    Label = LocKey.Common.Enabled,
                    Links = new[] { new Link("explorer-customization-context-menu", LinkKind.Requires, LocKey.Common.Enabled) },
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
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerContextMenuDism.Name,
                Description = LocKey.Setting.ExplorerContextMenuDism.Description,
                GroupName = LocKey.SettingGroup.ContextMenu,
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
                    Label = LocKey.Common.Enabled,
                    Links = new[] { new Link("explorer-customization-context-menu", LinkKind.Requires, LocKey.Common.Enabled) },
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
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerContextMenuChkdsk.Name,
                Description = LocKey.Setting.ExplorerContextMenuChkdsk.Description,
                GroupName = LocKey.SettingGroup.ContextMenu,
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
                    Label = LocKey.Common.Enabled,
                    Links = new[] { new Link("explorer-customization-context-menu", LinkKind.Requires, LocKey.Common.Enabled) },
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
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerContextMenuPs1EditRun.Name,
                Description = LocKey.Setting.ExplorerContextMenuPs1EditRun.Description,
                GroupName = LocKey.SettingGroup.ContextMenu,
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
                    Label = LocKey.Common.Enabled,
                    Links = new[] { new Link("explorer-customization-context-menu", LinkKind.Requires, LocKey.Common.Enabled) },
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
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerContextMenuCompressTo.Name,
                Description = LocKey.Setting.ExplorerContextMenuCompressTo.Description,
                GroupName = LocKey.SettingGroup.ContextMenu,
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
                    Label = LocKey.Common.Enabled,
                    Links = new[] { new Link("explorer-customization-context-menu", LinkKind.Requires, LocKey.Common.Enabled) },
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
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.DevicesDynamicLightingAmbient.Name,
                Description = LocKey.Setting.DevicesDynamicLightingAmbient.Description,
                GroupName = LocKey.SettingGroup.DevicesAndPeripherals,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["AmbientLightingEnabled"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.DevicesDynamicLightingForegroundApp.Name,
                Description = LocKey.Setting.DevicesDynamicLightingForegroundApp.Description,
                GroupName = LocKey.SettingGroup.DevicesAndPeripherals,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ControlledByForegroundApp"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.DevicesDefaultPrinterManagement.Name,
                Description = LocKey.Setting.DevicesDefaultPrinterManagement.Description,
                GroupName = LocKey.SettingGroup.DevicesAndPeripherals,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["LegacyDefaultPrinterMode"] = OneOf(0, -1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationLaunchTo.Name,
                Description = LocKey.Setting.ExplorerCustomizationLaunchTo.Description,
                GroupName = LocKey.SettingGroup.General,
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
                    Label = LocKey.Setting.ExplorerCustomizationLaunchTo.Option0,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["LaunchTo"] = Of(2).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationLaunchTo.Option1,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["LaunchTo"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationLaunchTo.Option2,
                    Set = new Dictionary<string, StateValue> { ["LaunchTo"] = Of(3) },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationLaunchTo.Option3,
                    Set = new Dictionary<string, StateValue> { ["LaunchTo"] = Of(4) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-browse-folders",
            Display = new()
            {
                Name = LocKey.Setting.ExplorerCustomizationBrowseFolders.Name,
                Description = LocKey.Setting.ExplorerCustomizationBrowseFolders.Description,
                GroupName = LocKey.SettingGroup.General,
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
                    Label = LocKey.Setting.ExplorerCustomizationBrowseFolders.Option0,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Settings"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationBrowseFolders.Option1,
                    Set = new Dictionary<string, StateValue> { ["Settings"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-click-items",
            Display = new()
            {
                Name = LocKey.Setting.ExplorerCustomizationClickItems.Name,
                Description = LocKey.Setting.ExplorerCustomizationClickItems.Description,
                GroupName = LocKey.SettingGroup.General,
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
                    Label = LocKey.Setting.ExplorerCustomizationClickItems.Option0,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["ShellState"] = Of(1),
                        ["IconUnderline"] = Of(3).OrAbsent(),
                    },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationClickItems.Option1,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["ShellState"] = Of(0),
                        ["IconUnderline"] = Of(3).OrAbsent(),
                    },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationClickItems.Option2,
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
                Name = LocKey.Setting.ExplorerCustomizationShowRecentFiles.Name,
                Description = LocKey.Setting.ExplorerCustomizationShowRecentFiles.Description,
                GroupName = LocKey.SettingGroup.General,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["ShowRecent"] = Of(1).OrAbsent(),
                        ["ShowRecommendations"] = Of(1).OrAbsent(),
                    },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationShowFrequentFolders.Name,
                Description = LocKey.Setting.ExplorerCustomizationShowFrequentFolders.Description,
                GroupName = LocKey.SettingGroup.General,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShowFrequent"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationShowOfficeFiles.Name,
                Description = LocKey.Setting.ExplorerCustomizationShowOfficeFiles.Description,
                GroupName = LocKey.SettingGroup.General,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShowCloudFilesInQuickAccess"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationShowThumbnails.Name,
                Description = LocKey.Setting.ExplorerCustomizationShowThumbnails.Description,
                GroupName = LocKey.SettingGroup.FilesAndFolders,
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
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["IconsOnly"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationShowMenus.Name,
                Description = LocKey.Setting.ExplorerCustomizationShowMenus.Description,
                GroupName = LocKey.SettingGroup.FilesAndFolders,
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
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["AlwaysShowMenus"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationItemSpace.Name,
                Description = LocKey.Setting.ExplorerCustomizationItemSpace.Description,
                GroupName = LocKey.SettingGroup.FilesAndFolders,
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
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["UseCompactMode"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationIconThumbnails.Name,
                Description = LocKey.Setting.ExplorerCustomizationIconThumbnails.Description,
                GroupName = LocKey.SettingGroup.FilesAndFolders,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShowTypeOverlay"] = Of(1).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationIconCacheSize.Name,
                Description = LocKey.Setting.ExplorerCustomizationIconCacheSize.Description,
                GroupName = LocKey.SettingGroup.FilesAndFolders,
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
                    Label = LocKey.Setting.ExplorerCustomizationIconCacheSize.Option0,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["MaxCachedIcons"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationIconCacheSize.Option1,
                    Set = new Dictionary<string, StateValue> { ["MaxCachedIcons"] = Of("4096") },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationIconCacheSize.Option2,
                    Set = new Dictionary<string, StateValue> { ["MaxCachedIcons"] = Of("8192") },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-thumbnail-cache-cleanup",
            Display = new()
            {
                Name = LocKey.Setting.ExplorerCustomizationThumbnailCacheCleanup.Name,
                Description = LocKey.Setting.ExplorerCustomizationThumbnailCacheCleanup.Description,
                GroupName = LocKey.SettingGroup.FilesAndFolders,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["Autorun"] = OneOf(3, 1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationFolderTips.Name,
                Description = LocKey.Setting.ExplorerCustomizationFolderTips.Description,
                GroupName = LocKey.SettingGroup.FilesAndFolders,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["FolderContentsInfoTip"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationFullPath.Name,
                Description = LocKey.Setting.ExplorerCustomizationFullPath.Description,
                GroupName = LocKey.SettingGroup.FilesAndFolders,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["FullPath"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationShowHiddenFiles.Name,
                Description = LocKey.Setting.ExplorerCustomizationShowHiddenFiles.Description,
                GroupName = LocKey.SettingGroup.FilesAndFolders,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["Hidden"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    IsFallback = true,
                    // Clean installs ship Hidden=2 (Explorer's Folder Options "don't show" write, all three
                    // clean-install fixtures); 0 is the legacy Winhance write. Both hide (Explorer shows
                    // only on Hidden=1). Write payload stays 0 (first value).
                    Set = new Dictionary<string, StateValue> { ["Hidden"] = OneOf(0, 2) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-hide-empty-drives",
            Display = new()
            {
                Name = LocKey.Setting.ExplorerCustomizationHideEmptyDrives.Name,
                Description = LocKey.Setting.ExplorerCustomizationHideEmptyDrives.Description,
                GroupName = LocKey.SettingGroup.FilesAndFolders,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["HideDrivesWithNoMedia"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationShowFileExt.Name,
                Description = LocKey.Setting.ExplorerCustomizationShowFileExt.Description,
                GroupName = LocKey.SettingGroup.FilesAndFolders,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["HideFileExt"] = Of(0) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationShowLnkExtension.Name,
                Description = LocKey.Setting.ExplorerCustomizationShowLnkExtension.Description,
                GroupName = LocKey.SettingGroup.FilesAndFolders,
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
                    Label = LocKey.Common.Enabled,
                    Links = new[] { new Link("explorer-customization-show-file-ext", LinkKind.Requires, LocKey.Common.Enabled) },
                    Set = new Dictionary<string, StateValue> { ["NeverShowExt"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerEnablePhotoViewer.Name,
                Description = LocKey.Setting.ExplorerEnablePhotoViewer.Description,
                GroupName = LocKey.SettingGroup.FileAssociations,
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
                    Label = LocKey.Common.Enabled,
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
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationLegacyNotepad.Name,
                Description = LocKey.Setting.ExplorerCustomizationLegacyNotepad.Description,
                GroupName = LocKey.SettingGroup.FileAssociations,
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
                    Label = LocKey.Common.Enabled,
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
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationHideMergeConflicts.Name,
                Description = LocKey.Setting.ExplorerCustomizationHideMergeConflicts.Description,
                GroupName = LocKey.SettingGroup.FilesAndFolders,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["HideMergeConflicts"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationHideProtectedFiles.Name,
                Description = LocKey.Setting.ExplorerCustomizationHideProtectedFiles.Description,
                GroupName = LocKey.SettingGroup.FilesAndFolders,
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
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["ShowSuperHidden"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationSeparateProcess.Name,
                Description = LocKey.Setting.ExplorerCustomizationSeparateProcess.Description,
                GroupName = LocKey.SettingGroup.FilesAndFolders,
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
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["SeparateProcess"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationPersistBrowsers.Name,
                Description = LocKey.Setting.ExplorerCustomizationPersistBrowsers.Description,
                GroupName = LocKey.SettingGroup.FilesAndFolders,
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
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["PersistBrowsers"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["PersistBrowsers"] = Of(0) },
                    ResetSet = new Dictionary<string, StateValue> { ["PersistBrowsers"] = Absent },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-show-drive-letters",
            Display = new()
            {
                Name = LocKey.Setting.ExplorerCustomizationShowDriveLetters.Name,
                Description = LocKey.Setting.ExplorerCustomizationShowDriveLetters.Description,
                GroupName = LocKey.SettingGroup.FilesAndFolders,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShowDriveLettersFirst"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationCompressedColor.Name,
                Description = LocKey.Setting.ExplorerCustomizationCompressedColor.Description,
                GroupName = LocKey.SettingGroup.FilesAndFolders,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["ShowEncryptCompressedColor"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationPopupDescriptions.Name,
                Description = LocKey.Setting.ExplorerCustomizationPopupDescriptions.Description,
                GroupName = LocKey.SettingGroup.FilesAndFolders,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShowInfoTip"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationPreviewHandlers.Name,
                Description = LocKey.Setting.ExplorerCustomizationPreviewHandlers.Description,
                GroupName = LocKey.SettingGroup.FilesAndFolders,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShowPreviewHandlers"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationStatusBar.Name,
                Description = LocKey.Setting.ExplorerCustomizationStatusBar.Description,
                GroupName = LocKey.SettingGroup.FilesAndFolders,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShowStatusBar"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationDisableSyncProviderNotifications.Name,
                Description = LocKey.Setting.ExplorerCustomizationDisableSyncProviderNotifications.Description,
                GroupName = LocKey.SettingGroup.FilesAndFolders,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["ShowSyncProviderNotifications"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationCheckboxSelect.Name,
                Description = LocKey.Setting.ExplorerCustomizationCheckboxSelect.Description,
                GroupName = LocKey.SettingGroup.FilesAndFolders,
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
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["AutoCheckSelect"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationSharingWizard.Name,
                Description = LocKey.Setting.ExplorerCustomizationSharingWizard.Description,
                GroupName = LocKey.SettingGroup.FilesAndFolders,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["SharingWizardOn"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationTypingBehavior.Name,
                Description = LocKey.Setting.ExplorerCustomizationTypingBehavior.Description,
                GroupName = LocKey.SettingGroup.FilesAndFolders,
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
                    Label = LocKey.Setting.ExplorerCustomizationTypingBehavior.Option0,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["TypeAhead"] = Of(0).OrAbsent() },
                },
                new SettingState
                {
                    Label = LocKey.Setting.ExplorerCustomizationTypingBehavior.Option1,
                    Set = new Dictionary<string, StateValue> { ["TypeAhead"] = Of(1) },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-thispc-folder-desktop",
            Display = new()
            {
                Name = LocKey.Setting.ExplorerCustomizationThispcFolderDesktop.Name,
                Description = LocKey.Setting.ExplorerCustomizationThispcFolderDesktop.Description,
                GroupName = LocKey.SettingGroup.ThisPCFolders,
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
                    Label = LocKey.Common.Enabled,
                    // Windows 10 shows the This PC folders by default (namespace key present); Windows 11 hides
                    // them (HiddenByDefault=1). The merged setting carries BOTH per-OS defaults as build-scoped
                    // WindowsDefault roles so bulk "Reset to Defaults" resolves the correct one on the live OS.
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows10 } } },
                    Set = new Dictionary<string, StateValue> { ["HiddenByDefault"] = Of(0), ["KeyExists"] = Exists },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationThispcFolderDocuments.Name,
                Description = LocKey.Setting.ExplorerCustomizationThispcFolderDocuments.Description,
                GroupName = LocKey.SettingGroup.ThisPCFolders,
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
                    Label = LocKey.Common.Enabled,
                    // Windows 10 shows the This PC folders by default (namespace key present); Windows 11 hides
                    // them (HiddenByDefault=1). The merged setting carries BOTH per-OS defaults as build-scoped
                    // WindowsDefault roles so bulk "Reset to Defaults" resolves the correct one on the live OS.
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows10 } } },
                    Set = new Dictionary<string, StateValue> { ["HiddenByDefault"] = Of(0), ["KeyExists"] = Exists },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationThispcFolderDownloads.Name,
                Description = LocKey.Setting.ExplorerCustomizationThispcFolderDownloads.Description,
                GroupName = LocKey.SettingGroup.ThisPCFolders,
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
                    Label = LocKey.Common.Enabled,
                    // Windows 10 shows the This PC folders by default (namespace key present); Windows 11 hides
                    // them (HiddenByDefault=1). The merged setting carries BOTH per-OS defaults as build-scoped
                    // WindowsDefault roles so bulk "Reset to Defaults" resolves the correct one on the live OS.
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows10 } } },
                    Set = new Dictionary<string, StateValue> { ["HiddenByDefault"] = Of(0), ["KeyExists"] = Exists },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationThispcFolderMusic.Name,
                Description = LocKey.Setting.ExplorerCustomizationThispcFolderMusic.Description,
                GroupName = LocKey.SettingGroup.ThisPCFolders,
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
                    Label = LocKey.Common.Enabled,
                    // Windows 10 shows the This PC folders by default (namespace key present); Windows 11 hides
                    // them (HiddenByDefault=1). The merged setting carries BOTH per-OS defaults as build-scoped
                    // WindowsDefault roles so bulk "Reset to Defaults" resolves the correct one on the live OS.
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows10 } } },
                    Set = new Dictionary<string, StateValue> { ["HiddenByDefault"] = Of(0), ["KeyExists"] = Exists },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationThispcFolderPictures.Name,
                Description = LocKey.Setting.ExplorerCustomizationThispcFolderPictures.Description,
                GroupName = LocKey.SettingGroup.ThisPCFolders,
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
                    Label = LocKey.Common.Enabled,
                    // Windows 10 shows the This PC folders by default (namespace key present); Windows 11 hides
                    // them (HiddenByDefault=1). The merged setting carries BOTH per-OS defaults as build-scoped
                    // WindowsDefault roles so bulk "Reset to Defaults" resolves the correct one on the live OS.
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows10 } } },
                    Set = new Dictionary<string, StateValue> { ["HiddenByDefault"] = Of(0), ["KeyExists"] = Exists },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationThispcFolderVideos.Name,
                Description = LocKey.Setting.ExplorerCustomizationThispcFolderVideos.Description,
                GroupName = LocKey.SettingGroup.ThisPCFolders,
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
                    Label = LocKey.Common.Enabled,
                    // Windows 10 shows the This PC folders by default (namespace key present); Windows 11 hides
                    // them (HiddenByDefault=1). The merged setting carries BOTH per-OS defaults as build-scoped
                    // WindowsDefault roles so bulk "Reset to Defaults" resolves the correct one on the live OS.
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows10 } } },
                    Set = new Dictionary<string, StateValue> { ["HiddenByDefault"] = Of(0), ["KeyExists"] = Exists },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomization3dObjects.Name,
                Description = LocKey.Setting.ExplorerCustomization3dObjects.Description,
                GroupName = LocKey.SettingGroup.ThisPCFolders,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["KeyExists"] = Exists },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationHomeFolder.Name,
                Description = LocKey.Setting.ExplorerCustomizationHomeFolder.Description,
                GroupName = LocKey.SettingGroup.NavigationPane,
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
                    Label = LocKey.Common.Enabled,
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
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationGallery.Name,
                Description = LocKey.Setting.ExplorerCustomizationGallery.Description,
                GroupName = LocKey.SettingGroup.NavigationPane,
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
                    Label = LocKey.Common.Enabled,
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
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationShowAvailabilityStatus.Name,
                Description = LocKey.Setting.ExplorerCustomizationShowAvailabilityStatus.Description,
                GroupName = LocKey.SettingGroup.NavigationPane,
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
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["NavPaneShowAllCloudStates"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["NavPaneShowAllCloudStates"] = Of(0) },
                    ResetSet = new Dictionary<string, StateValue> { ["NavPaneShowAllCloudStates"] = Absent },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-nav-expand-current",
            Display = new()
            {
                Name = LocKey.Setting.ExplorerCustomizationNavExpandCurrent.Name,
                Description = LocKey.Setting.ExplorerCustomizationNavExpandCurrent.Description,
                GroupName = LocKey.SettingGroup.NavigationPane,
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
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["NavPaneExpandToCurrentFolder"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationNavShowAllFolders.Name,
                Description = LocKey.Setting.ExplorerCustomizationNavShowAllFolders.Description,
                GroupName = LocKey.SettingGroup.NavigationPane,
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
                    Label = LocKey.Common.Enabled,
                    Links = new[]
                    {
                        new Link("explorer-customization-nav-saf-desktop", LinkKind.Enables, LocKey.Common.Enabled) { ReverseCascade = false, Force = true },
                        new Link("explorer-customization-nav-saf-documents", LinkKind.Enables, LocKey.Common.Enabled) { ReverseCascade = false, Force = true },
                        new Link("explorer-customization-nav-saf-downloads", LinkKind.Enables, LocKey.Common.Enabled) { ReverseCascade = false, Force = true },
                        new Link("explorer-customization-nav-saf-music", LinkKind.Enables, LocKey.Common.Enabled) { ReverseCascade = false, Force = true },
                        new Link("explorer-customization-nav-saf-pictures", LinkKind.Enables, LocKey.Common.Enabled) { ReverseCascade = false, Force = true },
                        new Link("explorer-customization-nav-saf-videos", LinkKind.Enables, LocKey.Common.Enabled) { ReverseCascade = false, Force = true },
                        new Link("explorer-customization-nav-show-libraries", LinkKind.Enables, LocKey.Common.Enabled) { ReverseCascade = false, Force = true },
                    },
                    Set = new Dictionary<string, StateValue> { ["NavPaneShowAllFolders"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["NavPaneShowAllFolders"] = Of(0) },
                    ResetSet = new Dictionary<string, StateValue> { ["NavPaneShowAllFolders"] = Absent },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-nav-saf-desktop",
            Display = new()
            {
                Name = LocKey.Setting.ExplorerCustomizationNavSafDesktop.Name,
                Description = LocKey.Setting.ExplorerCustomizationNavSafDesktop.Description,
                GroupName = LocKey.SettingGroup.NavigationPane,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}"] = Absent,
                        ["HiddenByDefault"] = Absent,
                    },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}"] = Of(1).OrAbsent(),
                        ["HiddenByDefault"] = Of(1).OrAbsent(),
                    },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-nav-saf-documents",
            Display = new()
            {
                Name = LocKey.Setting.ExplorerCustomizationNavSafDocuments.Name,
                Description = LocKey.Setting.ExplorerCustomizationNavSafDocuments.Description,
                GroupName = LocKey.SettingGroup.NavigationPane,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["{A8CDFF1C-4878-43be-B5FD-F8091C1C60D0}"] = Absent,
                        ["HiddenByDefault"] = Absent,
                    },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["{A8CDFF1C-4878-43be-B5FD-F8091C1C60D0}"] = Of(1).OrAbsent(),
                        ["HiddenByDefault"] = Of(1).OrAbsent(),
                    },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-nav-saf-downloads",
            Display = new()
            {
                Name = LocKey.Setting.ExplorerCustomizationNavSafDownloads.Name,
                Description = LocKey.Setting.ExplorerCustomizationNavSafDownloads.Description,
                GroupName = LocKey.SettingGroup.NavigationPane,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["{374DE290-123F-4565-9164-39C4925E467B}"] = Absent,
                        ["HiddenByDefault"] = Absent,
                    },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["{374DE290-123F-4565-9164-39C4925E467B}"] = Of(1).OrAbsent(),
                        ["HiddenByDefault"] = Of(1).OrAbsent(),
                    },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-nav-saf-music",
            Display = new()
            {
                Name = LocKey.Setting.ExplorerCustomizationNavSafMusic.Name,
                Description = LocKey.Setting.ExplorerCustomizationNavSafMusic.Description,
                GroupName = LocKey.SettingGroup.NavigationPane,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["{1CF1260C-4DD0-4ebb-811F-33C572699FDE}"] = Absent,
                        ["HiddenByDefault"] = Absent,
                    },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["{1CF1260C-4DD0-4ebb-811F-33C572699FDE}"] = Of(1).OrAbsent(),
                        ["HiddenByDefault"] = Of(1).OrAbsent(),
                    },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-nav-saf-pictures",
            Display = new()
            {
                Name = LocKey.Setting.ExplorerCustomizationNavSafPictures.Name,
                Description = LocKey.Setting.ExplorerCustomizationNavSafPictures.Description,
                GroupName = LocKey.SettingGroup.NavigationPane,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["{3ADD1653-EB32-4cb0-BBD7-DFA0ABB5ACCA}"] = Absent,
                        ["HiddenByDefault"] = Absent,
                    },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["{3ADD1653-EB32-4cb0-BBD7-DFA0ABB5ACCA}"] = Of(1).OrAbsent(),
                        ["HiddenByDefault"] = Of(1).OrAbsent(),
                    },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-nav-saf-videos",
            Display = new()
            {
                Name = LocKey.Setting.ExplorerCustomizationNavSafVideos.Name,
                Description = LocKey.Setting.ExplorerCustomizationNavSafVideos.Description,
                GroupName = LocKey.SettingGroup.NavigationPane,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["{A0953C92-50DC-43bf-BE83-3742FED03C9C}"] = Absent,
                        ["HiddenByDefault"] = Absent,
                    },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["{A0953C92-50DC-43bf-BE83-3742FED03C9C}"] = Of(1).OrAbsent(),
                        ["HiddenByDefault"] = Of(1).OrAbsent(),
                    },
                },
            },
        },
        new()
        {
            Id = "explorer-customization-nav-show-libraries",
            Display = new()
            {
                Name = LocKey.Setting.ExplorerCustomizationNavShowLibraries.Name,
                Description = LocKey.Setting.ExplorerCustomizationNavShowLibraries.Description,
                GroupName = LocKey.SettingGroup.NavigationPane,
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
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue>
                    {
                        ["System.IsPinnedToNameSpaceTree"] = Of(1),
                        ["{031E4825-7B94-4dc3-B131-E946B44C8DD5}"] = Of(0),
                        ["HiddenByDefault"] = Of(0),
                    },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                        ["System.IsPinnedToNameSpaceTree"] = Absent,
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
                Name = LocKey.Setting.ExplorerCustomizationDuplicateRemovableDrives.Name,
                Description = LocKey.Setting.ExplorerCustomizationDuplicateRemovableDrives.Description,
                GroupName = LocKey.SettingGroup.NavigationPane,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["KeyExists"] = Exists },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerLongFilePaths.Name,
                Description = LocKey.Setting.ExplorerLongFilePaths.Description,
                GroupName = LocKey.SettingGroup.FilesAndFolders,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended },
                    Set = new Dictionary<string, StateValue> { ["LongPathsEnabled"] = Of(1) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
                Name = LocKey.Setting.ExplorerCustomizationNetplwizAutoLogin.Name,
                Description = LocKey.Setting.ExplorerCustomizationNetplwizAutoLogin.Description,
                GroupName = LocKey.SettingGroup.Network,
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
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["DevicePasswordLessBuildVersion"] = Of(0) },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["DevicePasswordLessBuildVersion"] = Of(2) },
                },
            },
        },
        new()
        {
            Id = "explorer-autoplay",
            Display = new()
            {
                Name = LocKey.Setting.ExplorerAutoplay.Name,
                Description = LocKey.Setting.ExplorerAutoplay.Description,
                GroupName = LocKey.SettingGroup.DevicesAndPeripherals,
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
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue>
                    {
                        ["DisableAutoplay"] = Of(0).OrAbsent(),
                        ["NoDriveTypeAutoRun"] = Absent,
                    },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
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
