# Toggle Naming Clarity Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Rename 12 settings with confusing action-word names so toggle state (On/Off) reflects actual system state, convert 2 settings from Toggle to ComboBox, invert registry values accordingly, update all localization files with proper translations, update config files, and add config migration for the Toggle-to-ComboBox conversions.

**Architecture:** Changes span 6 model files (C# setting definitions), 27 localization JSON files, 3 `.winhance` config files, and the `ConfigMigrationService`. For the 10 toggle-inversion settings, we swap `EnabledValue`/`DisabledValue` and rename. For the 2 ComboBox conversions, we change `InputType` to `Selection`, add `ComboBox` metadata, and register config migrations. Localization files must be edited using the Edit tool or subagents with proper translations per language -- **never bash scripts**.

**Tech Stack:** C# (.NET 10), WinUI 3, JSON localization, xUnit + Moq + FluentAssertions

**Critical Rule:** When editing localization JSON files, each file must receive its own proper translation in that language. Use subagents (one per language group) with the Edit tool. **NEVER use bash/sed/awk scripts on localization files.**

---

## Settings Reference Table

This table is the single source of truth for all changes. Each task below references settings by their `#` number.

### Group 1: Toggle Inversion + Rename (10 settings)

| # | ID | Old Name | New Name | New Description | Old Enabled/Disabled | New Enabled/Disabled | File |
|---|---|---|---|---|---|---|---|
| 1 | `start-disable-bing-search-results` | Disable Bing search results | Bing Search Results in Start Menu | On: Web results from Bing appear when searching in the Start Menu. Off: Only local files and apps appear in Start Menu search. | E:[1] D:[null] | E:[null] D:[1] | StartMenuCustomizations.cs |
| 4 | `devices-default-printer-management` | Disable Automatic Default Printer Management | Automatic Default Printer Management | On: Windows automatically sets your default printer based on location or last used. Off: You manually control which printer is default. | E:[1] D:[0] | E:[0] D:[1] | ExplorerCustomizations.cs |
| 7 | `security-workplace-join-messages` | Block Workplace Join Messages | Workplace Join Message Prompts | On: 'Allow my organization to manage my device' prompts are shown. Off: These prompts are blocked. | E:[1] D:[null] | E:[null] D:[1] | PrivacyOptimizations.cs |
| 8 | `security-bitlocker-auto-encryption` | Prevent BitLocker Auto Encryption | BitLocker Auto Encryption | On: Windows may automatically encrypt your device with BitLocker. Off: Automatic BitLocker encryption is prevented. Has no effect if BitLocker encryption is already active on your device. | E:[1] D:[0] | E:[0] D:[1] | PrivacyOptimizations.cs |
| 9 | `privacy-onedrive-auto-backup` | Disable OneDrive Automatic Backups | OneDrive Automatic Backups | On: OneDrive automatically backs up your Documents, Pictures, and Desktop folders. Off: Automatic OneDrive backup is disabled. Has no effect if OneDrive Backups are already active. | E:[1] D:[null] | E:[null] D:[1] | PrivacyOptimizations.cs |
| 10 | `power-throttling` | Disable Power Throttling | Power Throttling | On: Windows reduces CPU performance for background processes to save power. Off: Background processes run at full CPU performance. | E:[1] D:[0] | E:[0] D:[1] | PowerOptimizations.cs |
| 11 | `updates-restart-options` | Prevent Automatic Restarts | Automatic Restart After Updates | On: Windows may automatically restart your PC after installing updates. Off: Automatic restarts are prevented when you are logged in. | E:[1] D:[null] | E:[null] D:[1] | UpdateOptimizations.cs |
| 12 | `updates-driver-controls` | Do Not Include Drivers with Updates | Driver Updates via Windows Update | On: Windows automatically downloads and installs hardware driver updates. Off: Driver updates are excluded from Windows Update. | E:[1] D:[null] | E:[null] D:[1] | UpdateOptimizations.cs |
| 13 | `gaming-disable-mpo` | Disable Multi-Plane Overlay (MPO) | Multi-Plane Overlay (MPO) | On: GPU composites multiple display layers in hardware (default Windows behavior). Off: MPO is disabled, which can fix screen flickering, black screens, and stuttering on multi-monitor setups. | E:[5]/[1] D:[null]/[null] | E:[null]/[null] D:[5]/[1] | GamingAndPerformanceOptimizations.cs |
| 14 | `gaming-disable-mpo-min-fps` | Disable MPO Minimum Frame Rate Requirement | MPO Minimum Frame Rate Requirement | On: Desktop Window Manager dynamically switches apps between overlay modes based on frame rate. Off: Prevents DWM switching, can potentially fix stuttering in browsers and Discord without fully disabling MPO. | E:[0] D:[null] | E:[null] D:[0] | GamingAndPerformanceOptimizations.cs |

### Group 2: Toggle to ComboBox Conversion (2 settings)

| # | ID | Old Name | New Name | Option 0 (Default) | Option 1 | File |
|---|---|---|---|---|---|---|
| 2 | `explorer-customization-shortcut-suffix` | Remove '- Shortcut' suffix from new shortcuts | Shortcut Naming | Keep '- Shortcut' suffix | Remove '- Shortcut' suffix | ExplorerCustomizations.cs |
| 3 | `explorer-customization-shortcut-arrow` | Remove Shortcut Arrow Icon | Shortcut Arrow Icon | Show arrow icon | Remove arrow icon | ExplorerCustomizations.cs |

### Config file `IsSelected` inversion mapping (for Recommended Config where `IsSelected: true`)

When we invert the toggle semantics, settings that were `IsSelected: true` (meaning "apply the action") now need `IsSelected: false` (because the toggle now represents the opposite state). And vice versa.

| ID | Old IsSelected (Recommended) | New IsSelected (Recommended) | Reason |
|---|---|---|---|
| `devices-default-printer-management` | true | false | Was "Disable auto mgmt = ON", now "Auto mgmt = OFF" |
| `start-disable-bing-search-results` | true | false | Was "Disable Bing = ON", now "Bing results = OFF" |
| `security-workplace-join-messages` | true | false | Was "Block prompts = ON", now "Show prompts = OFF" |
| `security-bitlocker-auto-encryption` | true | false | Was "Prevent encryption = ON", now "Auto encryption = OFF" |
| `privacy-onedrive-auto-backup` | true | false | Was "Disable backups = ON", now "Auto backups = OFF" |
| `power-throttling` | true | false | Was "Disable throttling = ON", now "Throttling = OFF" |
| `updates-restart-options` | true | false | Was "Prevent restarts = ON", now "Auto restart = OFF" |
| `updates-driver-controls` | true | false | Was "Exclude drivers = ON", now "Driver updates = OFF" |

Settings in Default configs all have `IsSelected: false` -- these also invert to `IsSelected: true` for the same reason (was "action disabled", now "feature enabled").

| ID | Old IsSelected (Default Configs) | New IsSelected (Default Configs) |
|---|---|---|
| All 10 toggle settings above | false | true |

For the 2 ComboBox conversions (#2, #3), `IsSelected: false` becomes `SelectedIndex: 0` (default option) and `InputType: 1`.

---

## Task 1: Update C# Model Files -- Toggle Inversions (Group 1)

**Files:**
- Modify: `src/Winhance.Core/Features/Customize/Models/StartMenuCustomizations.cs` (setting #1)
- Modify: `src/Winhance.Core/Features/Customize/Models/ExplorerCustomizations.cs` (setting #4)
- Modify: `src/Winhance.Core/Features/Optimize/Models/PrivacyOptimizations.cs` (settings #7, #8, #9)
- Modify: `src/Winhance.Core/Features/Optimize/Models/PowerOptimizations.cs` (setting #10)
- Modify: `src/Winhance.Core/Features/Optimize/Models/UpdateOptimizations.cs` (settings #11, #12)
- Modify: `src/Winhance.Core/Features/Optimize/Models/GamingAndPerformanceOptimizations.cs` (settings #13, #14)

For each setting, apply the exact changes from the reference table:
1. Change the `Name` property
2. Change the `Description` property
3. Swap `EnabledValue` and `DisabledValue` arrays
4. Swap `RecommendedValue` to match the new semantics (the recommended state is now the "off" value since the toggle is inverted)
5. For settings with `DefaultValue`, update it if needed (the default should reflect the default Windows state under the new toggle direction)

**Detailed changes per setting:**

### Setting #1 (`start-disable-bing-search-results`) in StartMenuCustomizations.cs:
```csharp
// BEFORE:
Name = "Disable Bing search results",
Description = "Prevent web results from Bing from appearing when searching in the Start Menu, showing only local files and apps",
// Both registry entries:
RecommendedValue = 1,
EnabledValue = [1],
DisabledValue = [null],

// AFTER:
Name = "Bing Search Results in Start Menu",
Description = "On: Web results from Bing appear when searching in the Start Menu. Off: Only local files and apps appear in Start Menu search",
// Both registry entries:
RecommendedValue = null,
EnabledValue = [null],
DisabledValue = [1],
```

### Setting #4 (`devices-default-printer-management`) in ExplorerCustomizations.cs:
```csharp
// BEFORE:
Name = "Disable Automatic Default Printer Management",
Description = "Prevents Windows from automatically changing your default printer based on location or last used printer",
RecommendedValue = 1,
EnabledValue = [1],
DisabledValue = [0],
DefaultValue = 0,

// AFTER:
Name = "Automatic Default Printer Management",
Description = "On: Windows automatically sets your default printer based on location or last used. Off: You manually control which printer is default",
RecommendedValue = 0,
EnabledValue = [0],
DisabledValue = [1],
DefaultValue = 0,
```
Note: `DefaultValue = 0` stays the same. In the old semantics 0 meant "disabled" (auto management was active). In the new semantics 0 means "enabled" (auto management is active). Same registry value, same meaning, correct.

### Setting #7 (`security-workplace-join-messages`) in PrivacyOptimizations.cs:
```csharp
// BEFORE:
Name = "Block Workplace Join Messages",
Description = "Blocks the 'Allow my organization to manage my device' and 'No, sign in to this app only' pop-up messages",
// Both registry entries:
RecommendedValue = 1,
EnabledValue = [1],
DisabledValue = [null],

// AFTER:
Name = "Workplace Join Message Prompts",
Description = "On: 'Allow my organization to manage my device' prompts are shown. Off: These prompts are blocked",
// Both registry entries:
RecommendedValue = null,
EnabledValue = [null],
DisabledValue = [1],
```

### Setting #8 (`security-bitlocker-auto-encryption`) in PrivacyOptimizations.cs:
```csharp
// BEFORE:
Name = "Prevent BitLocker Auto Encryption",
Description = "Prevents Windows from automatically encrypting your device with BitLocker without user consent",
RecommendedValue = 1,
EnabledValue = [1],
DisabledValue = [0],
DefaultValue = 0,

// AFTER:
Name = "BitLocker Auto Encryption",
Description = "On: Windows may automatically encrypt your device with BitLocker. Off: Automatic BitLocker encryption is prevented. Has no effect if BitLocker encryption is already active on your device",
RecommendedValue = 0,
EnabledValue = [0],
DisabledValue = [1],
DefaultValue = 0,
```

### Setting #9 (`privacy-onedrive-auto-backup`) in PrivacyOptimizations.cs:
```csharp
// BEFORE:
Name = "Disable OneDrive Automatic Backups",
Description = "Prevents OneDrive from automatically backing up important folders (Documents, Pictures, Desktop, etc.)",
// Both registry entries:
RecommendedValue = 1,
EnabledValue = [1],
DisabledValue = [null],

// AFTER:
Name = "OneDrive Automatic Backups",
Description = "On: OneDrive automatically backs up your Documents, Pictures, and Desktop folders. Off: Automatic OneDrive backup is disabled. Has no effect if OneDrive Backups are already active",
// Both registry entries:
RecommendedValue = null,
EnabledValue = [null],
DisabledValue = [1],
```

### Setting #10 (`power-throttling`) in PowerOptimizations.cs:
```csharp
// BEFORE:
Name = "Disable Power Throttling",
Description = "Automatically reduces CPU performance for background processes to improve battery life and reduce heat generation",
RecommendedValue = 1,
EnabledValue = [1],
DisabledValue = [0],
DefaultValue = 0,

// AFTER:
Name = "Power Throttling",
Description = "On: Windows reduces CPU performance for background processes to save power. Off: Background processes run at full CPU performance",
RecommendedValue = 0,
EnabledValue = [0],
DisabledValue = [1],
DefaultValue = 0,
```

### Setting #11 (`updates-restart-options`) in UpdateOptimizations.cs:
```csharp
// BEFORE:
Name = "Prevent Automatic Restarts",
Description = "Prevents automatic restarts after installing updates when users are logged on",
// Both registry entries:
RecommendedValue = 1,
EnabledValue = [1],
DisabledValue = [null],

// AFTER:
Name = "Automatic Restart After Updates",
Description = "On: Windows may automatically restart your PC after installing updates. Off: Automatic restarts are prevented when you are logged in",
// Both registry entries:
RecommendedValue = null,
EnabledValue = [null],
DisabledValue = [1],
```

### Setting #12 (`updates-driver-controls`) in UpdateOptimizations.cs:
```csharp
// BEFORE:
Name = "Do Not Include Drivers with Updates",
Description = "Prevent Windows from automatically downloading and installing hardware driver updates",
// Both registry entries:
RecommendedValue = 1,
EnabledValue = [1],
DisabledValue = [null],

// AFTER:
Name = "Driver Updates via Windows Update",
Description = "On: Windows automatically downloads and installs hardware driver updates. Off: Driver updates are excluded from Windows Update",
// Both registry entries:
RecommendedValue = null,
EnabledValue = [null],
DisabledValue = [1],
```

### Setting #13 (`gaming-disable-mpo`) in GamingAndPerformanceOptimizations.cs:
```csharp
// BEFORE:
Name = "Disable Multi-Plane Overlay (MPO)",
Description = "Disables Multi-Plane Overlay, a Windows graphics feature that allows the GPU to composite multiple display layers in hardware. Disabling MPO can fix screen flickering, black screens, stuttering, and frame pacing issues, especially on multi-monitor setups. May slightly increase CPU and GPU overhead for desktop composition",
// OverlayTestMode registry:
RecommendedValue = null,
EnabledValue = [5],
DisabledValue = [null],
// DisableOverlays registry:
RecommendedValue = null,
EnabledValue = [1],
DisabledValue = [null],

// AFTER:
Name = "Multi-Plane Overlay (MPO)",
Description = "On: GPU composites multiple display layers in hardware (default Windows behavior). Off: MPO is disabled, which can fix screen flickering, black screens, and stuttering on multi-monitor setups",
// OverlayTestMode registry:
RecommendedValue = null,
EnabledValue = [null],
DisabledValue = [5],
// DisableOverlays registry:
RecommendedValue = null,
EnabledValue = [null],
DisabledValue = [1],
```

### Setting #14 (`gaming-disable-mpo-min-fps`) in GamingAndPerformanceOptimizations.cs:
```csharp
// BEFORE:
Name = "Disable MPO Minimum Frame Rate Requirement",
Description = "Prevents the Desktop Window Manager from dynamically switching apps between overlay modes based on frame rate. This fixes stuttering and freezing in apps like browsers and Discord without fully disabling Multi-Plane Overlay. A lighter alternative to fully disabling MPO",
RecommendedValue = null,
EnabledValue = [0],
DisabledValue = [null],

// AFTER:
Name = "MPO Minimum Frame Rate Requirement",
Description = "On: Desktop Window Manager dynamically switches apps between overlay modes based on frame rate. Off: Prevents DWM switching, can potentially fix stuttering in browsers and Discord without fully disabling MPO",
RecommendedValue = null,
EnabledValue = [null],
DisabledValue = [0],
```

---

## Task 2: Update C# Model Files -- ComboBox Conversions (Group 2)

**Files:**
- Modify: `src/Winhance.Core/Features/Customize/Models/ExplorerCustomizations.cs` (settings #2, #3)

### Setting #2 (`explorer-customization-shortcut-suffix`):
```csharp
// BEFORE:
new SettingDefinition
{
    Id = "explorer-customization-shortcut-suffix",
    Name = "Remove '- Shortcut' suffix from new shortcuts",
    Description = "Prevents Windows from appending '- Shortcut' text to newly created shortcut file names",
    GroupName = "Desktop",
    InputType = InputType.Toggle,
    Icon = "LinkVariant",
    RestartProcess = "Explorer",
    RegistrySettings = new List<RegistrySetting>
    {
        new RegistrySetting
        {
            KeyPath = @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer",
            ValueName = "link",
            RecommendedValue = null,
            EnabledValue = [new byte[] { 0x00, 0x00, 0x00, 0x00 }],
            DisabledValue = [null],
            DefaultValue = null,
            ValueType = RegistryValueKind.Binary,
        },
    },
},

// AFTER:
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
        Values = new object[]
        {
            null,
            new byte[] { 0x00, 0x00, 0x00, 0x00 },
        },
    },
},
```

### Setting #3 (`explorer-customization-shortcut-arrow`):
```csharp
// BEFORE:
new SettingDefinition
{
    Id = "explorer-customization-shortcut-arrow",
    Name = "Remove Shortcut Arrow Icon",
    Description = "Removes the small arrow overlay from desktop shortcut icons for a cleaner look",
    GroupName = "Desktop",
    InputType = InputType.Toggle,
    Icon = "ArrowTopLeftBoldOutline",
    AddedInVersion = "26.03.26",
    RestartProcess = "Explorer",
    PowerShellScripts = new List<PowerShellScriptSetting> { ... }, // Keep unchanged
    RegistrySettings = new List<RegistrySetting>
    {
        new RegistrySetting
        {
            KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons",
            ValueName = "29",
            RecommendedValue = null,
            EnabledValue = [@"C:\Windows\blank.ico"],
            DisabledValue = [null],
            DefaultValue = null,
            ValueType = RegistryValueKind.String,
        },
    },
},

// AFTER:
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
    PowerShellScripts = new List<PowerShellScriptSetting> { ... }, // Keep unchanged
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
        Values = new object[]
        {
            null,
            @"C:\Windows\blank.ico",
        },
    },
},
```

**Important:** The `PowerShellScripts` section must be preserved exactly as-is. The EnabledScript creates the blank.ico file and should still run when "Remove arrow icon" (index 1) is selected. Verify that the `SettingOperationExecutor` runs PowerShell scripts for Selection types by checking how `taskbar-transparent` handles its scripts (it was also converted from Toggle to Selection). If Selection types don't run PowerShell scripts, this needs additional investigation.

---

## Task 3: Add Config Migration for ComboBox Conversions

**Files:**
- Modify: `src/Winhance.Infrastructure/Features/Common/Services/ConfigMigrationService.cs`
- Modify: `tests/Winhance.Infrastructure.Tests/Services/ConfigMigrationServiceTests.cs`

### Step 1: Add migration registrations

In `ConfigMigrationService.cs`, add two new entries to the `_migrations` dictionary:

```csharp
_migrations = new Dictionary<string, Action<ConfigurationItem>>
{
    ["taskbar-transparent"] = MigrateTaskbarTransparent,
    ["explorer-customization-shortcut-suffix"] = MigrateToggleToSelection,
    ["explorer-customization-shortcut-arrow"] = MigrateToggleToSelection,
};
```

### Step 2: Add the generic migration method

```csharp
/// <summary>
/// Migrates a Toggle-based setting to Selection format.
/// Old format: InputType=Toggle, IsSelected=true (action applied) or false (default).
/// New format: InputType=Selection, SelectedIndex=0 (default/first option), 1 (action applied/second option).
/// </summary>
private void MigrateToggleToSelection(ConfigurationItem item)
{
    if (item.InputType != InputType.Toggle)
        return; // Already migrated or not a toggle

    if (item.IsSelected == true)
    {
        item.SelectedIndex = 1; // The "action" option (e.g., "Remove")
    }
    else
    {
        item.SelectedIndex = 0; // The "default" option (e.g., "Keep")
    }

    item.InputType = InputType.Selection;
    item.IsSelected = null;

    _logService.Log(
        LogLevel.Info,
        $"Migrated config item '{item.Id}' from Toggle to Selection (SelectedIndex={item.SelectedIndex})");
}
```

### Step 3: Write tests

Add to `ConfigMigrationServiceTests.cs`:

```csharp
[Theory]
[InlineData("explorer-customization-shortcut-suffix")]
[InlineData("explorer-customization-shortcut-arrow")]
public void MigrateConfig_ShortcutToggleSelected_MigratedToSelectionIndex1(string settingId)
{
    var item = new ConfigurationItem
    {
        Id = settingId,
        Name = "Old Name",
        InputType = InputType.Toggle,
        IsSelected = true,
    };

    var config = CreateConfigWithCustomizeItem(item);

    _sut.MigrateConfig(config);

    item.InputType.Should().Be(InputType.Selection);
    item.SelectedIndex.Should().Be(1);
    item.IsSelected.Should().BeNull();
}

[Theory]
[InlineData("explorer-customization-shortcut-suffix")]
[InlineData("explorer-customization-shortcut-arrow")]
public void MigrateConfig_ShortcutToggleNotSelected_MigratedToSelectionIndex0(string settingId)
{
    var item = new ConfigurationItem
    {
        Id = settingId,
        Name = "Old Name",
        InputType = InputType.Toggle,
        IsSelected = false,
    };

    var config = CreateConfigWithCustomizeItem(item);

    _sut.MigrateConfig(config);

    item.InputType.Should().Be(InputType.Selection);
    item.SelectedIndex.Should().Be(0);
    item.IsSelected.Should().BeNull();
}

[Theory]
[InlineData("explorer-customization-shortcut-suffix")]
[InlineData("explorer-customization-shortcut-arrow")]
public void MigrateConfig_ShortcutAlreadySelection_NotMigrated(string settingId)
{
    var item = new ConfigurationItem
    {
        Id = settingId,
        Name = "New Name",
        InputType = InputType.Selection,
        SelectedIndex = 1,
    };

    var config = CreateConfigWithCustomizeItem(item);

    _sut.MigrateConfig(config);

    item.InputType.Should().Be(InputType.Selection);
    item.SelectedIndex.Should().Be(1);
}
```

### Step 4: Build and run tests

```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" tests/Winhance.Infrastructure.Tests/Winhance.Infrastructure.Tests.csproj -t:Build -p:Platform=x64 -p:Configuration=Debug -v:quiet
```

```bash
dotnet test tests/Winhance.Infrastructure.Tests/Winhance.Infrastructure.Tests.csproj --filter "ConfigMigrationServiceTests" -p:Platform=x64 --no-build
```

### Step 5: Commit

```
feat: add config migration for shortcut settings Toggle-to-Selection conversion
```

---

## Task 4: Update English Localization (en.json)

**Files:**
- Modify: `src/Winhance.UI/Features/Common/Localization/en.json`

Update all 12 settings' `_Name` and `_Description` keys. Also add `_Option_0` and `_Option_1` keys for the 2 ComboBox settings.

### Toggle inversion name/description changes:
```json
"Setting_start-disable-bing-search-results_Name": "Bing Search Results in Start Menu",
"Setting_start-disable-bing-search-results_Description": "On: Web results from Bing appear when searching in the Start Menu. Off: Only local files and apps appear in Start Menu search",

"Setting_devices-default-printer-management_Name": "Automatic Default Printer Management",
"Setting_devices-default-printer-management_Description": "On: Windows automatically sets your default printer based on location or last used. Off: You manually control which printer is default",

"Setting_security-workplace-join-messages_Name": "Workplace Join Message Prompts",
"Setting_security-workplace-join-messages_Description": "On: \u0027Allow my organization to manage my device\u0027 prompts are shown. Off: These prompts are blocked",

"Setting_security-bitlocker-auto-encryption_Name": "BitLocker Auto Encryption",
"Setting_security-bitlocker-auto-encryption_Description": "On: Windows may automatically encrypt your device with BitLocker. Off: Automatic BitLocker encryption is prevented. Has no effect if BitLocker encryption is already active on your device",

"Setting_privacy-onedrive-auto-backup_Name": "OneDrive Automatic Backups",
"Setting_privacy-onedrive-auto-backup_Description": "On: OneDrive automatically backs up your Documents, Pictures, and Desktop folders. Off: Automatic OneDrive backup is disabled. Has no effect if OneDrive Backups are already active",

"Setting_power-throttling_Name": "Power Throttling",
"Setting_power-throttling_Description": "On: Windows reduces CPU performance for background processes to save power. Off: Background processes run at full CPU performance",

"Setting_updates-restart-options_Name": "Automatic Restart After Updates",
"Setting_updates-restart-options_Description": "On: Windows may automatically restart your PC after installing updates. Off: Automatic restarts are prevented when you are logged in",

"Setting_updates-driver-controls_Name": "Driver Updates via Windows Update",
"Setting_updates-driver-controls_Description": "On: Windows automatically downloads and installs hardware driver updates. Off: Driver updates are excluded from Windows Update",

"Setting_gaming-disable-mpo_Name": "Multi-Plane Overlay (MPO)",
"Setting_gaming-disable-mpo_Description": "On: GPU composites multiple display layers in hardware (default Windows behavior). Off: MPO is disabled, which can fix screen flickering, black screens, and stuttering on multi-monitor setups",

"Setting_gaming-disable-mpo-min-fps_Name": "MPO Minimum Frame Rate Requirement",
"Setting_gaming-disable-mpo-min-fps_Description": "On: Desktop Window Manager dynamically switches apps between overlay modes based on frame rate. Off: Prevents DWM switching, can potentially fix stuttering in browsers and Discord without fully disabling MPO",
```

### ComboBox name/description/option changes:
```json
"Setting_explorer-customization-shortcut-suffix_Name": "Shortcut Naming",
"Setting_explorer-customization-shortcut-suffix_Description": "Controls whether Windows appends \u0027- Shortcut\u0027 text to newly created shortcut file names",
"Setting_explorer-customization-shortcut-suffix_Option_0": "Keep \u0027- Shortcut\u0027 suffix",
"Setting_explorer-customization-shortcut-suffix_Option_1": "Remove \u0027- Shortcut\u0027 suffix",

"Setting_explorer-customization-shortcut-arrow_Name": "Shortcut Arrow Icon",
"Setting_explorer-customization-shortcut-arrow_Description": "Controls the small arrow overlay on desktop shortcut icons",
"Setting_explorer-customization-shortcut-arrow_Option_0": "Show arrow icon",
"Setting_explorer-customization-shortcut-arrow_Option_1": "Remove arrow icon",
```

---

## Task 5: Update Non-English Localization Files (26 files)

**Files:**
- Modify: All 26 non-English JSON files in `src/Winhance.UI/Features/Common/Localization/`

**Critical:** Use subagents with the Edit tool. Each subagent handles a batch of language files. Each file must receive **proper translations** in that language, not English copy-pasted.

### Subagent dispatch strategy:
Dispatch subagents in parallel, each handling ~4-5 language files. Each subagent receives:
1. The exact JSON keys to update (same keys as Task 4)
2. The English text as reference
3. Instruction to translate into each target language
4. Instruction to use the Edit tool only
5. For ComboBox settings: also add the `_Option_0` and `_Option_1` keys with translations

### Language groupings for subagents:
- **Subagent A:** af, nl, nl-BE, de, sv (Germanic languages)
- **Subagent B:** fr, es, pt, pt-BR, it (Romance languages)
- **Subagent C:** pl, cs, hu, lt, lv, uk, ru (Slavic/Baltic languages)
- **Subagent D:** zh-Hans, zh-Hant, ja, ko, vi (East Asian languages)
- **Subagent E:** ar, el, hi, tr (Other languages)

### What each subagent must do per file:
1. Read the file
2. Find and edit each of the 12 settings' `_Name` and `_Description` keys using the Edit tool
3. Add the 4 new `_Option_` keys for the 2 ComboBox settings (place them right after the `_Description` key for that setting)
4. Verify the JSON is valid after edits

---

## Task 6: Update .winhance Config Files

**Files:**
- Modify: `src/Winhance.UI/Features/Common/Resources/Configs/Winhance_Default_Config_Windows11_25H2.winhance`
- Modify: `src/Winhance.UI/Features/Common/Resources/Configs/Winhance_Default_Config_Windows10_22H2.winhance`
- Modify: `src/Winhance.UI/Features/Common/Resources/Configs/Winhance_Recommended_Config.winhance`

### For each of the 10 toggle-inversion settings in ALL THREE config files:
1. Update the `"Name"` field to the new name
2. **Invert the `"IsSelected"` value** (true becomes false, false becomes true)

### For the 2 ComboBox conversion settings in ALL THREE config files:
1. Update the `"Name"` field to the new name
2. Change `"InputType": 0` to `"InputType": 1`
3. Remove the `"IsSelected"` field
4. Add `"SelectedIndex": 0` (default option)

Example for shortcut suffix in Recommended Config:
```json
// BEFORE:
{
    "Id": "explorer-customization-shortcut-suffix",
    "Name": "Remove '- Shortcut' suffix from new shortcuts",
    "IsSelected": false,
    "InputType": 0
}

// AFTER:
{
    "Id": "explorer-customization-shortcut-suffix",
    "Name": "Shortcut Naming",
    "InputType": 1,
    "SelectedIndex": 0
}
```

---

## Task 7: Verify PowerShell Script Execution for Selection Type

**Files:**
- Read: `src/Winhance.Infrastructure/Features/Common/Services/SettingOperationExecutor.cs`

Before considering this plan complete, verify that `SettingOperationExecutor` properly executes PowerShell scripts for `InputType.Selection` settings. The `explorer-customization-shortcut-arrow` setting has a PowerShell script that creates `blank.ico`. If the executor only runs scripts for Toggle types, we need to handle this.

Check how `taskbar-transparent` (which was already converted from Toggle to Selection) handles any associated scripts, if any, as a reference.

---

## Task 8: Build and Smoke Test

### Step 1: Build the full solution
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" Winhance.sln -t:Build -p:Platform=x64 -p:Configuration=Debug -v:quiet
```

### Step 2: Run all existing tests
```bash
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" tests/Winhance.Infrastructure.Tests/Winhance.Infrastructure.Tests.csproj -t:Build -p:Platform=x64 -p:Configuration=Debug -v:quiet && dotnet test tests/Winhance.Infrastructure.Tests/Winhance.Infrastructure.Tests.csproj -p:Platform=x64 --no-build
```

### Step 3: Commit all changes
```
feat: clarify toggle naming for 12 settings to reflect actual system state

Rename settings with action-word names (Disable, Block, Prevent, Remove)
so that toggle On/Off reflects the actual state. Convert 2 shortcut
settings from Toggle to ComboBox with explicit options. Add config
migration for Toggle-to-Selection conversion of shortcut settings.
Update all 27 localization files with proper translations and all 3
.winhance config files with inverted IsSelected values.
```
