using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using static Winhance.Core.Features.Common.Catalog.StateValue;
using Winhance.Core.Features.Common.Localization;

namespace Winhance.Core.Features.Autounattend.Catalogs;

public static class AutounattendCatalog
{
    public const string FeatureId = FeatureIds.Autounattend;

    public static IReadOnlyList<Setting> All { get; } =
    [
        // A file that carries more than one architecture installs on any of them.
        new()
        {
            Id = "autounattend-architecture-x64",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendArchitectureX64.Name,
                Description = LocKey.Setting.AutounattendArchitectureX64.Description,
                GroupName = LocKey.SettingGroup.ProcessorArchitecture,
                Icon = MaterialIcons.Chip,
                AddedInVersion = "26.09.07",
            },
            Targets =
            [
                new AutounattendArchitecture("arch", "amd64"),
                new RegTarget("this-pc", [@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment"], "PROCESSOR_ARCHITECTURE", RegistryValueKind.String) { ReadOnly = true },
            ],
            States =
            [
                new() { Label = LocKey.Common.Checked, Set = new Dictionary<string, StateValue> { ["arch"] = Of("amd64"), ["this-pc"] = Of("AMD64") }, Roles = [StateRole.WindowsDefault] },
                new() { Label = LocKey.Common.Unchecked, Set = new Dictionary<string, StateValue> { ["arch"] = Absent, ["this-pc"] = Exists } },
            ],
        },
        new()
        {
            Id = "autounattend-architecture-arm64",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendArchitectureArm64.Name,
                Description = LocKey.Setting.AutounattendArchitectureArm64.Description,
                GroupName = LocKey.SettingGroup.ProcessorArchitecture,
                Icon = MaterialIcons.Chip,
                AddedInVersion = "26.09.07",
            },
            Targets =
            [
                new AutounattendArchitecture("arch", "arm64"),
                new RegTarget("this-pc", [@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment"], "PROCESSOR_ARCHITECTURE", RegistryValueKind.String) { ReadOnly = true },
            ],
            States =
            [
                new() { Label = LocKey.Common.Checked, Set = new Dictionary<string, StateValue> { ["arch"] = Of("arm64"), ["this-pc"] = Of("ARM64") }, Roles = [StateRole.WindowsDefault] },
                new() { Label = LocKey.Common.Unchecked, Set = new Dictionary<string, StateValue> { ["arch"] = Absent, ["this-pc"] = Exists } },
            ],
        },
        new()
        {
            Id = "autounattend-architecture-x86",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendArchitectureX86.Name,
                Description = LocKey.Setting.AutounattendArchitectureX86.Description,
                GroupName = LocKey.SettingGroup.ProcessorArchitecture,
                Icon = MaterialIcons.Chip,
                AddedInVersion = "26.09.07",
            },
            Targets =
            [
                new AutounattendArchitecture("arch", "x86"),
                new RegTarget("this-pc", [@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment"], "PROCESSOR_ARCHITECTURE", RegistryValueKind.String) { ReadOnly = true },
            ],
            States =
            [
                new() { Label = LocKey.Common.Checked, Set = new Dictionary<string, StateValue> { ["arch"] = Of("x86"), ["this-pc"] = Of("x86") }, Roles = [StateRole.WindowsDefault] },
                new() { Label = LocKey.Common.Unchecked, Set = new Dictionary<string, StateValue> { ["arch"] = Absent, ["this-pc"] = Exists } },
            ],
        },
        new()
        {
            Id = "autounattend-edition",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendEdition.Name,
                Description = LocKey.Setting.AutounattendEdition.Description,
                GroupName = LocKey.SettingGroup.EditionAndProductKey,
                Icon = MaterialIcons.DesktopClassic,
                AddedInVersion = "26.09.07",
            },
            Targets =
            [
                new AutounattendElement("key", "windowsPE", "Microsoft-Windows-Setup", "UserData/ProductKey/Key"),
                new AutounattendElement("show", "windowsPE", "Microsoft-Windows-Setup", "UserData/ProductKey/WillShowUI"),
            ],
            // Generic RTM install keys: https://gist.github.com/ThiagoBarradas/e21d09bdc896892299af0b21e43f1069#rtm-generic-keys
            States =
            [
                // Not a real key: Setup rejects it and, with WillShowUI=Always, lists every edition on the media instead.
                new() { Label = LocKey.Setting.AutounattendEdition.Option0, Set = new Dictionary<string, StateValue> { ["key"] = Of("00000-00000-00000-00000-00000"), ["show"] = Of("Always") }, Roles = [StateRole.Recommended, StateRole.WindowsDefault] },
                new() { Label = LocKey.Setting.AutounattendEdition.Option1, Set = new Dictionary<string, StateValue> { ["key"] = Of("YTMG3-N6DKC-DKB77-7M9GH-8HVX7"), ["show"] = Of("OnError") } },
                new() { Label = LocKey.Setting.AutounattendEdition.Option2, Set = new Dictionary<string, StateValue> { ["key"] = Of("4CPRK-NM3K3-X6XXQ-RXX86-WXCHW"), ["show"] = Of("OnError") } },
                new() { Label = LocKey.Setting.AutounattendEdition.Option3, Set = new Dictionary<string, StateValue> { ["key"] = Of("BT79Q-G7N6G-PGBYW-4YWX6-6F4BT"), ["show"] = Of("OnError") } },
                new() { Label = LocKey.Setting.AutounattendEdition.Option4, Set = new Dictionary<string, StateValue> { ["key"] = Of("VK7JG-NPHTM-C97JM-9MPGT-3V66T"), ["show"] = Of("OnError") } },
                new() { Label = LocKey.Setting.AutounattendEdition.Option5, Set = new Dictionary<string, StateValue> { ["key"] = Of("2B87N-8KFHP-DKV6R-Y2C8J-PKCKT"), ["show"] = Of("OnError") } },
                new() { Label = LocKey.Setting.AutounattendEdition.Option6, Set = new Dictionary<string, StateValue> { ["key"] = Of("DXG7C-N36C4-C4HTG-X4T3X-2YV77"), ["show"] = Of("OnError") } },
                new() { Label = LocKey.Setting.AutounattendEdition.Option7, Set = new Dictionary<string, StateValue> { ["key"] = Of("WYPNQ-8C467-V2W6J-TX4WX-WT2RQ"), ["show"] = Of("OnError") } },
                new() { Label = LocKey.Setting.AutounattendEdition.Option8, Set = new Dictionary<string, StateValue> { ["key"] = Of("8PTT6-RNW4C-6V7J2-C2D3X-MHBPB"), ["show"] = Of("OnError") } },
                new() { Label = LocKey.Setting.AutounattendEdition.Option9, Set = new Dictionary<string, StateValue> { ["key"] = Of("GJTYN-HDMQY-FRR76-HVGC7-QPF8P"), ["show"] = Of("OnError") } },
                new() { Label = LocKey.Setting.AutounattendEdition.Option10, Set = new Dictionary<string, StateValue> { ["key"] = Of("YNMGQ-8RYV3-4PGQ3-C8XTP-7CFBY"), ["show"] = Of("OnError") } },
                new() { Label = LocKey.Setting.AutounattendEdition.Option11, Set = new Dictionary<string, StateValue> { ["key"] = Of("84NGF-MHBT6-FXBX8-QWJK7-DRR8H"), ["show"] = Of("OnError") } },
                new() { Label = LocKey.Setting.AutounattendEdition.Option12, Set = new Dictionary<string, StateValue> { ["key"] = Of("XGVPP-NMH47-7TTHJ-W3FW7-8HV2C"), ["show"] = Of("OnError") } },
                new() { Label = LocKey.Setting.AutounattendEdition.Option13, Set = new Dictionary<string, StateValue> { ["key"] = Of("WGGHN-J84D6-QYCPR-T7PJ7-X766F"), ["show"] = Of("OnError") } },
                new() { Label = LocKey.Setting.AutounattendEdition.Option14, Set = new Dictionary<string, StateValue> { ["key"] = Absent, ["show"] = Of("OnError") } },
                new() { Label = LocKey.Setting.AutounattendEdition.Option15, Set = new Dictionary<string, StateValue> { ["key"] = Absent, ["show"] = Of("Never") } },
            ],
        },
        new()
        {
            Id = "autounattend-product-key",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendProductKey.Name,
                Description = LocKey.Setting.AutounattendProductKey.Description,
                GroupName = LocKey.SettingGroup.EditionAndProductKey,
                Icon = FluentIcons.LockClosedKey,
                AddedInVersion = "26.09.07",
            },
            TextBox = new(new TextRule(
                "^([A-Z0-9]{5}-){4}[A-Z0-9]{5}$",
                UpperCase: true,
                LocKey.Setting.AutounattendProductKey.Error)),
            Targets =
            [
                new AutounattendElement("key", "windowsPE", "Microsoft-Windows-Setup", "UserData/ProductKey/Key"),
                new AutounattendElement("activate", "specialize", "Microsoft-Windows-Shell-Setup", "ProductKey"),
            ],
            UiParentId = "autounattend-edition",
            EnabledWhen = new("autounattend-edition", [LocKey.Setting.AutounattendEdition.Option14]),
        },
        new()
        {
            Id = "autounattend-media-editions",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendMediaEditions.Name,
                Description = LocKey.Setting.AutounattendMediaEditions.Description,
                GroupName = LocKey.SettingGroup.EditionAndProductKey,
                Icon = MaterialIcons.PackageVariantRemove,
                AddedInVersion = "26.10.04",
            },
            Targets = [new AutounattendMedia("keep")],
            States =
            [
                new() { Label = LocKey.Setting.AutounattendMediaEditions.Option0, Roles = [StateRole.Recommended, StateRole.WindowsDefault] },
                new() { Label = LocKey.Setting.AutounattendMediaEditions.Option1 },
                new() { Label = LocKey.Setting.AutounattendMediaEditions.Option2 },
            ],
            UiParentId = "autounattend-edition",
        },
        new()
        {
            Id = "autounattend-media-editions-list",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendMediaEditionsList.Name,
                Description = LocKey.Setting.AutounattendMediaEditionsList.Description,
                GroupName = LocKey.SettingGroup.EditionAndProductKey,
                Icon = MaterialIcons.CheckboxMarked,
                AddedInVersion = "26.10.04",
            },
            CheckList = new(CheckListSource.MediaEditions),
            Targets = [new AutounattendMedia("editions")],
            UiParentId = "autounattend-edition",
            VisibleWhen = new("autounattend-media-editions", [LocKey.Setting.AutounattendMediaEditions.Option2]),
        },
        new()
        {
            Id = "autounattend-hardware-checks",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendHardwareChecks.Name,
                Description = LocKey.Setting.AutounattendHardwareChecks.Description,
                GroupName = LocKey.SettingGroup.Windows11Requirements,
                Icon = MaterialIcons.ShieldCheck,
                AddedInVersion = "26.09.07",
            },
            States =
            [
                new() { Label = LocKey.Common.Enabled, Roles = [StateRole.WindowsDefault] },
                new()
                {
                    Label = LocKey.Common.Disabled,
                    Roles = [StateRole.Recommended],
                    // Microsoft documents none of these LabConfig values.
                    Effects =
                    [
                        new AutounattendCommand("windowsPE", "Microsoft-Windows-Setup", "reg.exe add \"HKLM\\SYSTEM\\Setup\\LabConfig\" /v BypassTPMCheck /t REG_DWORD /d 1 /f", "Order 1-6: Skip Windows 11 Hardware Requirement Checks"),
                        new AutounattendCommand("windowsPE", "Microsoft-Windows-Setup", "reg.exe add \"HKLM\\SYSTEM\\Setup\\LabConfig\" /v BypassSecureBootCheck /t REG_DWORD /d 1 /f"),
                        new AutounattendCommand("windowsPE", "Microsoft-Windows-Setup", "reg.exe add \"HKLM\\SYSTEM\\Setup\\LabConfig\" /v BypassStorageCheck /t REG_DWORD /d 1 /f"),
                        new AutounattendCommand("windowsPE", "Microsoft-Windows-Setup", "reg.exe add \"HKLM\\SYSTEM\\Setup\\LabConfig\" /v BypassCPUCheck /t REG_DWORD /d 1 /f"),
                        new AutounattendCommand("windowsPE", "Microsoft-Windows-Setup", "reg.exe add \"HKLM\\SYSTEM\\Setup\\LabConfig\" /v BypassRAMCheck /t REG_DWORD /d 1 /f"),
                        new AutounattendCommand("windowsPE", "Microsoft-Windows-Setup", "reg.exe add \"HKLM\\SYSTEM\\Setup\\LabConfig\" /v BypassDiskCheck /t REG_DWORD /d 1 /f"),
                    ],
                },
            ],
        },
        // The partition screen is the one windowsPE screen an answer file cannot skip by hiding it: Setup needs a
        // partition to install to. DiskConfigurationWriter builds the layout, because CreatePartition is a repeated
        // element no single target path can address - the same reason LocalAccount has AccountWriter.
        new()
        {
            Id = "autounattend-disk",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendDisk.Name,
                Description = LocKey.Setting.AutounattendDisk.Description,
                GroupName = LocKey.SettingGroup.DiskAndPartitions,
                Icon = MaterialIcons.Harddisk,
                AddedInVersion = "26.09.22",
            },
            // WillShowUI OnError rather than Never: a layout that will not apply puts the partition screen back
            // in front of the user instead of ending the installation.
            Targets = [new AutounattendElement("show-ui", "windowsPE", "Microsoft-Windows-Setup", "DiskConfiguration/WillShowUI")],
            States =
            [
                new() { Label = LocKey.Setting.AutounattendDisk.Option0, Set = new Dictionary<string, StateValue> { ["show-ui"] = Absent }, Roles = [StateRole.Recommended, StateRole.WindowsDefault] },
                new() { Label = LocKey.Setting.AutounattendDisk.Option1, Set = new Dictionary<string, StateValue> { ["show-ui"] = Of("OnError") } },
                new() { Label = LocKey.Setting.AutounattendDisk.Option2, Set = new Dictionary<string, StateValue> { ["show-ui"] = Of("OnError") } },
            ],
        },
        new()
        {
            Id = "autounattend-disk-number",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendDiskNumber.Name,
                Description = LocKey.Setting.AutounattendDiskNumber.Description,
                GroupName = LocKey.SettingGroup.DiskAndPartitions,
                Icon = MaterialIcons.Harddisk,
                AddedInVersion = "26.09.22",
            },
            TextBox = new(new TextRule(
                "^[0-9]{1,2}$",
                UpperCase: false,
                LocKey.Setting.AutounattendDiskNumber.Error),
                Default: "0"),
            Targets = [new AutounattendElement("disk-id", "windowsPE", "Microsoft-Windows-Setup", "DiskConfiguration/Disk/DiskID")],
            UiParentId = "autounattend-disk",
            EnabledWhen = new("autounattend-disk", [LocKey.Setting.AutounattendDisk.Option1, LocKey.Setting.AutounattendDisk.Option2]),
        },
        new()
        {
            Id = "autounattend-disk-system-size",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendDiskSystemSize.Name,
                Description = LocKey.Setting.AutounattendDiskSystemSize.Description,
                GroupName = LocKey.SettingGroup.DiskAndPartitions,
                Icon = MaterialIcons.Harddisk,
                AddedInVersion = "26.09.22",
            },
            TextBox = new(new TextRule(
                "^[1-9][0-9]{2,3}$",
                UpperCase: false,
                LocKey.Setting.AutounattendDiskSystemSize.Error),
                Default: "300"),
            Targets = [new AutounattendElement("system-size", "windowsPE", "Microsoft-Windows-Setup", "DiskConfiguration/Disk/CreatePartitions")],
            UiParentId = "autounattend-disk",
            EnabledWhen = new("autounattend-disk", [LocKey.Setting.AutounattendDisk.Option1, LocKey.Setting.AutounattendDisk.Option2]),
        },
        new()
        {
            Id = "autounattend-disk-recovery-size",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendDiskRecoverySize.Name,
                Description = LocKey.Setting.AutounattendDiskRecoverySize.Description,
                GroupName = LocKey.SettingGroup.DiskAndPartitions,
                Icon = MaterialIcons.Harddisk,
                AddedInVersion = "26.09.22",
            },
            // An empty box is a real answer here: no recovery partition, and Setup keeps the tools on C:.
            TextBox = new(new TextRule(
                "^(?:[1-9][0-9]{2,4})?$",
                UpperCase: false,
                LocKey.Setting.AutounattendDiskRecoverySize.Error),
                Default: "1000"),
            Targets = [new AutounattendElement("recovery-size", "windowsPE", "Microsoft-Windows-Setup", "DiskConfiguration/Disk/CreatePartitions")],
            UiParentId = "autounattend-disk",
            EnabledWhen = new("autounattend-disk", [LocKey.Setting.AutounattendDisk.Option1, LocKey.Setting.AutounattendDisk.Option2]),
        },
        new()
        {
            Id = "autounattend-account",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendAccount.Name,
                Description = LocKey.Setting.AutounattendAccount.Description,
                GroupName = LocKey.SettingGroup.Accounts,
                Icon = MaterialIcons.AccountCog,
                AddedInVersion = "26.09.07",
            },
            Targets = [new AutounattendElement("hide-online", "oobeSystem", "Microsoft-Windows-Shell-Setup", "OOBE/HideOnlineAccountScreens")],
            States =
            [
                // OOBE skips its account phase when UserAccounts creates an account, so only this state keeps OOBE offline.
                // Controls is the whole decision about the adapter cut: OobeNetworkWriter reads it for the file and the
                // page reads it for the card, so a mode that decides for the user cannot show one answer and write another.
                // Option 2 declares none, which is what leaves the choice with the user.
                new()
                {
                    Label = LocKey.Setting.AutounattendAccount.Option0,
                    Set = new Dictionary<string, StateValue> { ["hide-online"] = Of("true") },
                    Roles = [StateRole.Recommended],
                    Controls = new Dictionary<string, LocKey> { ["autounattend-oobe-network"] = LocKey.Setting.AutounattendOobeNetwork.Option1 },
                    Effects =
                    [
                        new AutounattendCommand("specialize", "Microsoft-Windows-Deployment",
                            "reg.exe add \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\OOBE\" /v BypassNRO /t REG_DWORD /d 1 /f",
                            "Registry Entry to Create a Local Account during OOBE"),
                    ],
                },
                new()
                {
                    Label = LocKey.Setting.AutounattendAccount.Option1,
                    Set = new Dictionary<string, StateValue> { ["hide-online"] = Absent },
                    Roles = [StateRole.WindowsDefault],
                    Controls = new Dictionary<string, LocKey> { ["autounattend-oobe-network"] = LocKey.Setting.AutounattendOobeNetwork.Option0 },
                },
                new() { Label = LocKey.Setting.AutounattendAccount.Option2, Set = new Dictionary<string, StateValue> { ["hide-online"] = Of("true") } },
            ],
        },
        new()
        {
            Id = "autounattend-accounts",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendAccounts.Name,
                Description = LocKey.Setting.AutounattendAccounts.Description,
                GroupName = LocKey.SettingGroup.Accounts,
                Icon = MaterialIcons.AccountSupervisor,
                AddedInVersion = "26.09.07",
            },
            List = new(
                [
                    // Windows account-name rules: 1 to 20 characters, no reserved punctuation, not only dots and spaces.
                    new Field("name", FieldKind.Text, LocKey.AutounattendAccounts.Name, Rule: new TextRule(
                        "^(?![. ]+$)[^\"/\\\\\\[\\]:;|=,+*?<>]{1,20}$",
                        UpperCase: false,
                        LocKey.Setting.AutounattendAccounts.Error)),
                    new Field("display-name", FieldKind.Text, LocKey.AutounattendAccounts.DisplayName),
                    new Field("group", FieldKind.Selection, LocKey.AutounattendAccounts.Group,
                        Options: [LocKey.AutounattendAccounts.GroupAdministrators, LocKey.AutounattendAccounts.GroupUsers],
                        Default: "0"),
                    new Field("password", FieldKind.Password, LocKey.AutounattendAccounts.Password),
                    new Field("obscure", FieldKind.CheckBox, LocKey.AutounattendAccounts.Obscure,
                        Tooltip: LocKey.AutounattendAccounts.ObscureTooltip,
                        Default: "true"),
                    // Windows signs one account in, once, and it has to be an administrator.
                    new Field("auto-logon", FieldKind.CheckBox, LocKey.AutounattendAccounts.AutoLogon,
                        Tooltip: LocKey.AutounattendAccounts.AutoLogonTooltip,
                        OneRowOnly: true,
                        OnlyWhen: new FieldCondition("group", 0)),
                    new Field("change-password", FieldKind.CheckBox, LocKey.AutounattendAccounts.ChangePassword,
                        Tooltip: LocKey.AutounattendAccounts.ChangePasswordTooltip,
                        Default: "false"),
                ],
                // Setup clears the AutoLogon credentials from its cached copy of the file, but not a LocalAccount password.
                new AutounattendFirstLogonCommand(
                    "cmd.exe /c del /q C:\\Windows\\Panther\\unattend.xml",
                    "Deletes the Cached Answer File Carrying the Account Passwords"),
                new Dictionary<string, string> { ["name"] = "name", ["display-name"] = "display-name" },
                LocKey.AutounattendAccounts.Add,
                LocKey.AutounattendAccounts.Remove),
            Targets =
            [
                new AutounattendElement("accounts", "oobeSystem", "Microsoft-Windows-Shell-Setup", "UserAccounts/LocalAccounts"),
                new RegTarget("name", [@"HKEY_CURRENT_USER\Volatile Environment"], "USERNAME", RegistryValueKind.String) { ReadOnly = true },
                new RegTarget("display-name", [@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\LogonUI"], "LastLoggedOnDisplayName", RegistryValueKind.String) { ReadOnly = true },
            ],
            UiParentId = "autounattend-account",
            EnabledWhen = new("autounattend-account", [LocKey.Setting.AutounattendAccount.Option2]),
        },
        new()
        {
            Id = "autounattend-oobe-network",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendOobeNetwork.Name,
                Description = LocKey.Setting.AutounattendOobeNetwork.Description,
                GroupName = LocKey.SettingGroup.Accounts,
                Icon = MaterialIcons.LanDisconnect,
                AddedInVersion = "26.09.22",
            },
            States =
            [
                new() { Label = LocKey.Setting.AutounattendOobeNetwork.Option0, Roles = [StateRole.WindowsDefault] },
                // What a new file starts on, and what a local account has no choice about: Setup downloads updates over any
                // connection it has, which is most of the wait between the last screen and the desktop.
                new()
                {
                    Label = LocKey.Setting.AutounattendOobeNetwork.Option1,
                    Roles = [StateRole.Recommended],
                    Effects =
                    [
                        new AutounattendCommand("specialize", "Microsoft-Windows-Deployment",
                            "powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command \"Get-NetAdapter | Disable-NetAdapter -Confirm:$false\"",
                            "Disable All Network Adapters Temporarily so Windows Doesn't Update During OOBE and to Allow Local Account Creation"),
                        new AutounattendFirstLogonCommand(
                            "powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command \"Get-NetAdapter | Enable-NetAdapter -Confirm:$false\"",
                            "Enables Network Adapters After OOBE Completes"),
                    ],
                },
            ],
            UiParentId = "autounattend-account",
            EnabledWhen = new("autounattend-account", [LocKey.Setting.AutounattendAccount.Option2]),
        },
        new()
        {
            Id = "autounattend-user-folders",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendUserFolders.Name,
                Description = LocKey.Setting.AutounattendUserFolders.Description,
                GroupName = LocKey.SettingGroup.Accounts,
                Icon = MaterialIcons.FolderAccount,
                AddedInVersion = "26.10.02",
            },
            // Any text passes: a drive the new PC lacks is reported in its log, never refused here, so the rule's
            // message is never shown and reuses the placeholder key. An empty box writes nothing at all.
            TextBox = new(
                new TextRule("^.*$", UpperCase: false, LocKey.Setting.AutounattendUserFolders.Placeholder),
                Placeholder: LocKey.Setting.AutounattendUserFolders.Placeholder),
            // Runs as each account at its first sign-in, before the restart that follows it, so Explorer starts on
            // the new folders. User Shell Folders is what Explorer reads; Windows rebuilds Shell Folders from it.
            CustomStateScripts =
            [
                new ScriptEffect(@"$base = '{{value}}'
$root = [System.IO.Path]::Combine($base.TrimEnd('\') + '\', $env:USERNAME)
$drive = [System.IO.Path]::GetPathRoot($root)
if ($drive -and (Test-Path -LiteralPath $drive)) {
    $key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders'
    $folders = [ordered]@{ 'Desktop' = 'Desktop'; 'Personal' = 'Documents'; '{374DE290-123F-4565-9164-39C4925E467B}' = 'Downloads'; 'My Music' = 'Music'; 'My Pictures' = 'Pictures'; 'My Video' = 'Videos' }
    foreach ($name in $folders.Keys) {
        $target = Join-Path $root $folders[$name]
        $current = [Environment]::ExpandEnvironmentVariables([string](Get-ItemProperty -Path $key -Name $name -ErrorAction SilentlyContinue).$name)
        New-Item -Path $target -ItemType Directory -Force | Out-Null
        if ($current -and $current -ne $target -and (Test-Path -LiteralPath $current)) {
            Get-ChildItem -LiteralPath $current -Force | Move-Item -Destination $target -Force -ErrorAction SilentlyContinue
        }
        Set-ItemProperty -Path $key -Name $name -Value $target -Type ExpandString
    }
} else {
    Write-Log ""User folders left in place: $base is not on a drive this PC has"" ""WARNING""
}", RunContext.User),
            ],
        },
        new()
        {
            Id = "autounattend-setup-language",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendSetupLanguage.Name,
                Description = LocKey.Setting.AutounattendSetupLanguage.Description,
                GroupName = LocKey.SettingGroup.SetupScreens,
                Icon = MaterialIcons.Translate,
                AddedInVersion = "26.09.22",
            },
            // No Path, so the list is every language rather than this PC's: the file is for media this PC has
            // never seen, and SetupUILanguage has to name a language that media actually carries.
            Options = new(OptionSource.DisplayLanguages) { NoChoiceLabel = LocKey.Setting.AutounattendSetupLanguage.OptionAsk },
            Targets =
            [
                new AutounattendElement("setup-ui", "windowsPE", "Microsoft-Windows-International-Core-WinPE", "SetupUILanguage/UILanguage"),
                new AutounattendElement("ui", "windowsPE", "Microsoft-Windows-International-Core-WinPE", "UILanguage"),
            ],
        },
        new()
        {
            Id = "autounattend-licence-page",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendLicencePage.Name,
                Description = LocKey.Setting.AutounattendLicencePage.Description,
                GroupName = LocKey.SettingGroup.SetupScreens,
                Icon = MaterialIcons.FileDocumentCheck,
                AddedInVersion = "26.09.07",
            },
            Targets = [new AutounattendElement("hide", "oobeSystem", "Microsoft-Windows-Shell-Setup", "OOBE/HideEULAPage")],
            States =
            [
                new() { Label = LocKey.Common.Enabled, Set = new Dictionary<string, StateValue> { ["hide"] = Absent }, Roles = [StateRole.WindowsDefault] },
                new() { Label = LocKey.Common.Disabled, Set = new Dictionary<string, StateValue> { ["hide"] = Of("true") }, Roles = [StateRole.Recommended] },
            ],
        },
        new()
        {
            Id = "autounattend-oem-registration-page",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendOemRegistrationPage.Name,
                Description = LocKey.Setting.AutounattendOemRegistrationPage.Description,
                GroupName = LocKey.SettingGroup.SetupScreens,
                Icon = MaterialIcons.OfficeBuilding,
                AddedInVersion = "26.09.07",
            },
            Targets = [new AutounattendElement("hide", "oobeSystem", "Microsoft-Windows-Shell-Setup", "OOBE/HideOEMRegistrationScreen")],
            States =
            [
                new() { Label = LocKey.Common.Enabled, Set = new Dictionary<string, StateValue> { ["hide"] = Absent }, Roles = [StateRole.WindowsDefault] },
                new() { Label = LocKey.Common.Disabled, Set = new Dictionary<string, StateValue> { ["hide"] = Of("true") }, Roles = [StateRole.Recommended] },
            ],
        },
        new()
        {
            Id = "autounattend-network-page",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendNetworkPage.Name,
                Description = LocKey.Setting.AutounattendNetworkPage.Description,
                GroupName = LocKey.SettingGroup.SetupScreens,
                Icon = MaterialIcons.NetworkOutline,
                AddedInVersion = "26.09.07",
            },
            Targets = [new AutounattendElement("hide", "oobeSystem", "Microsoft-Windows-Shell-Setup", "OOBE/HideWirelessSetupInOOBE")],
            States =
            [
                new() { Label = LocKey.Common.Enabled, Set = new Dictionary<string, StateValue> { ["hide"] = Absent }, Roles = [StateRole.WindowsDefault] },
                new() { Label = LocKey.Common.Disabled, Set = new Dictionary<string, StateValue> { ["hide"] = Of("true") }, Roles = [StateRole.Recommended] },
            ],
        },
        new()
        {
            Id = "autounattend-privacy",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendPrivacy.Name,
                Description = LocKey.Setting.AutounattendPrivacy.Description,
                GroupName = LocKey.SettingGroup.SetupScreens,
                Icon = MaterialIcons.ShieldLock,
                AddedInVersion = "26.09.07",
            },
            Targets = [new AutounattendElement("protect", "oobeSystem", "Microsoft-Windows-Shell-Setup", "OOBE/ProtectYourPC")],
            // ProtectYourPC: 3 turns every express setting off, 1 turns them on; with no element Setup asks.
            States =
            [
                new() { Label = LocKey.Setting.AutounattendPrivacy.Option0, Set = new Dictionary<string, StateValue> { ["protect"] = Of("3") }, Roles = [StateRole.Recommended] },
                new() { Label = LocKey.Setting.AutounattendPrivacy.Option1, Set = new Dictionary<string, StateValue> { ["protect"] = Of("1") } },
                new() { Label = LocKey.Setting.AutounattendPrivacy.Option2, Set = new Dictionary<string, StateValue> { ["protect"] = Absent }, Roles = [StateRole.WindowsDefault] },
            ],
        },
        new()
        {
            Id = "autounattend-computer-name",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendComputerName.Name,
                Description = LocKey.Setting.AutounattendComputerName.Description,
                GroupName = LocKey.SettingGroup.SetupScreens,
                Icon = MaterialIcons.Laptop,
                AddedInVersion = "26.09.07",
            },
            TextBox = new(new TextRule(
                "^(?!\\d+$)[A-Za-z0-9](?:[A-Za-z0-9-]{0,13}[A-Za-z0-9])?$",
                UpperCase: false,
                LocKey.Setting.AutounattendComputerName.Error),
                SeedKey: "this-pc"),
            Targets =
            [
                new AutounattendElement("name", "specialize", "Microsoft-Windows-Shell-Setup", "ComputerName"),
                // The DNS host name keeps the case the user typed; the NetBIOS name beside it is upper-cased.
                new RegTarget("this-pc", [@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters"], "Hostname", RegistryValueKind.String) { ReadOnly = true },
            ],
        },
        new()
        {
            Id = "autounattend-netfx3",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendNetfx3.Name,
                Description = LocKey.Setting.AutounattendNetfx3.Description,
                GroupName = LocKey.SettingGroup.General,
                Icon = MaterialIcons.PackageVariantPlus,
                AddedInVersion = "26.09.07",
            },
            States =
            [
                new()
                {
                    Label = LocKey.Common.Enabled,
                    Roles = [StateRole.Recommended],
                    // /LimitAccess keeps DISM off Windows Update; every filesystem drive is searched for
                    // sources\sxs because the letter Setup gives the media is not known in advance. Path caps
                    // at 259 characters, so anything added to this command has to be measured first.
                    Effects =
                    [
                        new AutounattendCommand("specialize", "Microsoft-Windows-Deployment",
                            "powershell.exe -NoProfile -WindowStyle Hidden -Command \"foreach($d in (gdr -PSProvider FileSystem)){$src=$d.Root+'sources\\sxs';if(Test-Path $src\\*.cab){dism /Online /Enable-Feature /FeatureName:NetFx3 /All /LimitAccess /Source:$src;break}}\"",
                            "Enables .NET Framework 3.5 from Windows Installation Media"),
                    ],
                },
                new() { Label = LocKey.Common.Disabled, Roles = [StateRole.WindowsDefault] },
            ],
        },
        new()
        {
            Id = "autounattend-desktop-shortcut",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendDesktopShortcut.Name,
                Description = LocKey.Setting.AutounattendDesktopShortcut.Description,
                GroupName = LocKey.SettingGroup.General,
                Icon = new Icon(IconPack.AppAsset, "winhance-rocket-card.png"),
                AddedInVersion = "26.09.10",
            },
            // {{external-app-ids}} becomes the External Apps chosen for the new PC, comma-separated, or nothing.
            States =
            [
                new() { Label = LocKey.Setting.AutounattendDesktopShortcut.Option0, Roles = [StateRole.WindowsDefault] },
                new()
                {
                    Label = LocKey.Setting.AutounattendDesktopShortcut.Option1,
                    Roles = [StateRole.Recommended],
                    // Byte 21 of a .lnk holds the link flags; 0x22 sets RunAsUser, so the shortcut asks for elevation.
                    Effects =
                    [
                        new ScriptEffect(@"$shortcutPath = ""C:\Users\Default\Desktop\Install Winhance.lnk""
$WshShell = New-Object -ComObject WScript.Shell
$shortcut = $WshShell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = ""C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe""
$shortcut.Arguments = ""-ExecutionPolicy Bypass -NoProfile -Command `""irm 'https://get.winhance.net' | iex`""""
$shortcut.IconLocation = ""C:\Windows\System32\appwiz.cpl,0""
$shortcut.WorkingDirectory = ""C:\Windows\System32""
$shortcut.Description = ""Download and install Winhance from GitHub""
$shortcut.Save()
$bytes = [System.IO.File]::ReadAllBytes($shortcutPath)
$bytes[21] = 34
[System.IO.File]::WriteAllBytes($shortcutPath, $bytes)", RunContext.System) { When = AppsToInstall.None },
                        new ScriptEffect(@"Set-Content -Path 'C:\ProgramData\Winhance\Unattend\Scripts\Install-Winhance.ps1' -Encoding UTF8 -Value @'
# Installs Winhance, then starts it with the apps chosen for this PC. Exit 0 once Winhance is installed and started, 1 when that failed.
$ErrorActionPreference = 'Stop'
$apps = '{{external-app-ids}}'
$installer = 'C:\ProgramData\Winhance\Unattend\Winhance.Installer.exe'
$winhance = ""$env:ProgramFiles\Winhance\Winhance.exe""
$ownInstallers = 'C:\ProgramData\Winhance\Unattend\Apps\Custom'
try {
    if (-not (Test-Path $winhance)) {
        if (-not (Test-Path $installer)) {
            $ProgressPreference = 'SilentlyContinue'
            Invoke-WebRequest 'https://github.com/memstechtips/Winhance/releases/latest/download/Winhance.Installer.exe' -OutFile ""$installer.part"" -UseBasicParsing
            Move-Item ""$installer.part"" $installer -Force
        }
        Start-Process $installer '/SILENT /SUPPRESSMSGBOXES /MERGETASKS=""regularinstall\desktopicon,regularinstall\startmenuicon""' -Wait
        if (-not (Test-Path $winhance)) { exit 1 }
        Remove-Item $installer -ErrorAction SilentlyContinue
    }
    $arguments = @()
    if ($apps) { $arguments += ""--install-apps $apps"" }
    if (Test-Path $ownInstallers) { $arguments += ""--install-folder `""$ownInstallers`"""" }
    if ($arguments) { Start-Process $winhance ($arguments -join ' ') }
    Remove-Item 'C:\Users\Default\Desktop\Install Winhance.lnk', ""$env:USERPROFILE\Desktop\Install Winhance.lnk"", $PSCommandPath -ErrorAction SilentlyContinue
    exit 0
} catch {
    exit 1
}
'@
$shortcutPath = ""C:\Users\Default\Desktop\Install Winhance.lnk""
$WshShell = New-Object -ComObject WScript.Shell
$shortcut = $WshShell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = ""C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe""
$shortcut.Arguments = ""-ExecutionPolicy Bypass -NoProfile -File `""C:\ProgramData\Winhance\Unattend\Scripts\Install-Winhance.ps1`""""
$shortcut.IconLocation = ""C:\Windows\System32\appwiz.cpl,0""
$shortcut.WorkingDirectory = ""C:\Windows\System32""
$shortcut.Description = ""Download and install Winhance from GitHub""
$shortcut.Save()
$bytes = [System.IO.File]::ReadAllBytes($shortcutPath)
$bytes[21] = 34
[System.IO.File]::WriteAllBytes($shortcutPath, $bytes)", RunContext.System) { When = AppsToInstall.Some },
                    ],
                },
                new()
                {
                    Label = LocKey.Setting.AutounattendDesktopShortcut.Option2,
                    // The sign-in after Setup's restart runs the script, which deletes itself once Winhance is installed
                    // and started, so a failed install retries at the next sign-in.
                    Effects =
                    [
                        new ScriptEffect(@"Set-Content -Path 'C:\ProgramData\Winhance\Unattend\Scripts\Install-Winhance.ps1' -Encoding UTF8 -Value @'
# Installs Winhance, then starts it with the apps chosen for this PC. Exit 0 once Winhance is installed and started, 1 when that failed.
$ErrorActionPreference = 'Stop'
$apps = '{{external-app-ids}}'
$installer = 'C:\ProgramData\Winhance\Unattend\Winhance.Installer.exe'
$winhance = ""$env:ProgramFiles\Winhance\Winhance.exe""
$ownInstallers = 'C:\ProgramData\Winhance\Unattend\Apps\Custom'
try {
    if (-not (Test-Path $winhance)) {
        if (-not (Test-Path $installer)) {
            $ProgressPreference = 'SilentlyContinue'
            Invoke-WebRequest 'https://github.com/memstechtips/Winhance/releases/latest/download/Winhance.Installer.exe' -OutFile ""$installer.part"" -UseBasicParsing
            Move-Item ""$installer.part"" $installer -Force
        }
        Start-Process $installer '/SILENT /SUPPRESSMSGBOXES /MERGETASKS=""regularinstall\desktopicon,regularinstall\startmenuicon""' -Wait
        if (-not (Test-Path $winhance)) { exit 1 }
        Remove-Item $installer -ErrorAction SilentlyContinue
    }
    $arguments = @()
    if ($apps) { $arguments += ""--install-apps $apps"" }
    if (Test-Path $ownInstallers) { $arguments += ""--install-folder `""$ownInstallers`"""" }
    if ($arguments) { Start-Process $winhance ($arguments -join ' ') }
    Remove-Item 'C:\Users\Default\Desktop\Install Winhance.lnk', ""$env:USERPROFILE\Desktop\Install Winhance.lnk"", $PSCommandPath -ErrorAction SilentlyContinue
    exit 0
} catch {
    exit 1
}
'@", RunContext.System),
                        new ScriptEffect(@"if (Test-Path 'C:\ProgramData\Winhance\Unattend\Scripts\Install-Winhance.ps1') { Start-ProcessAsUser -CommandLine ""powershell.exe -ExecutionPolicy Bypass -NoProfile -WindowStyle Hidden -File `""C:\ProgramData\Winhance\Unattend\Scripts\Install-Winhance.ps1`"""" | Out-Null }", RunContext.SignInAfterSetup),
                    ],
                },
            ],
        },
        new()
        {
            Id = "autounattend-delete-installers",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendDeleteInstallers.Name,
                Description = LocKey.Setting.AutounattendDeleteInstallers.Description,
                GroupName = LocKey.SettingGroup.General,
                Icon = MaterialIcons.TrashCanOutline,
                AddedInVersion = "26.09.28",
            },
            // Winhance reads KeepStagedInstallers before it deletes an installer it has run.
            States =
            [
                new() { Label = LocKey.Common.Enabled, Roles = [StateRole.Recommended, StateRole.WindowsDefault] },
                new()
                {
                    Label = LocKey.Common.Disabled,
                    Effects =
                    [
                        new ScriptEffect(@"New-Item -Path 'HKLM:\SOFTWARE\Winhance' -Force | Out-Null
Set-ItemProperty -Path 'HKLM:\SOFTWARE\Winhance' -Name 'KeepStagedInstallers' -Value 1 -Type DWord", RunContext.System) { When = AppsToInstall.Some },
                    ],
                },
            ],
            UiParentId = "autounattend-desktop-shortcut",
            EnabledWhen = new("autounattend-desktop-shortcut",
                [LocKey.Setting.AutounattendDesktopShortcut.Option1, LocKey.Setting.AutounattendDesktopShortcut.Option2]),
        },
        // The External Software row on the page draws these two; WIMUtil acts on them when it generates the file.
        new()
        {
            Id = "autounattend-stage-apps",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendStageApps.Name,
                Description = LocKey.Setting.AutounattendStageApps.Description,
                GroupName = LocKey.SettingGroup.General,
                Icon = MaterialIcons.PackageDown,
                AddedInVersion = "26.10.06",
            },
            Targets = [new AutounattendMedia("stage")],
            States =
            [
                new() { Label = LocKey.Common.Checked, Roles = [StateRole.Recommended, StateRole.WindowsDefault] },
                new() { Label = LocKey.Common.Unchecked },
            ],
        },
        new()
        {
            Id = "autounattend-own-installers",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendOwnInstallers.Name,
                Description = LocKey.Setting.AutounattendOwnInstallers.Description,
                GroupName = LocKey.SettingGroup.General,
                Icon = MaterialIcons.PackageVariantPlus,
                AddedInVersion = "26.10.06",
            },
            List = new(
                [
                    new Field("file", FieldKind.File, LocKey.AutounattendOwnInstallers.File,
                        Filter: new FileFilter(LocKey.AutounattendOwnInstallers.Filter, "*.exe;*.msi")),
                    new Field("switches", FieldKind.Text, LocKey.AutounattendOwnInstallers.Switches),
                ],
                Add: LocKey.AutounattendOwnInstallers.Add,
                Remove: LocKey.AutounattendOwnInstallers.Remove,
                CanBeEmpty: true),
            Targets = [new AutounattendMedia("installers")],
        },
        new()
        {
            Id = "autounattend-user-script-system",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendUserScriptSystem.Name,
                Description = LocKey.Setting.AutounattendUserScriptSystem.Description,
                GroupName = LocKey.SettingGroup.YourOwnScripts,
                Icon = MaterialIcons.ScriptText,
                AddedInVersion = "26.09.21",
            },
            // The type picks the file's extension, and with it the encoding the extractor writes and the program that runs it.
            List = new(
                [
                    new Field("type", FieldKind.Selection, LocKey.AutounattendUserScripts.Type,
                        Options:
                        [
                            LocKey.AutounattendUserScripts.TypePowerShell,
                            LocKey.AutounattendUserScripts.TypeCmd,
                            LocKey.AutounattendUserScripts.TypeBatch,
                            LocKey.AutounattendUserScripts.TypeRegistry,
                        ],
                        Default: "0"),
                    new Field("script", FieldKind.Text, LocKey.AutounattendUserScripts.Script, MultiLine: true),
                ],
                Add: LocKey.AutounattendUserScripts.Add,
                Remove: LocKey.AutounattendUserScripts.Remove),
            Targets = [new AutounattendScript("script", RunContext.System)],
        },
        new()
        {
            Id = "autounattend-user-script-user",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendUserScriptUser.Name,
                Description = LocKey.Setting.AutounattendUserScriptUser.Description,
                GroupName = LocKey.SettingGroup.YourOwnScripts,
                Icon = MaterialIcons.ScriptTextOutline,
                AddedInVersion = "26.09.21",
            },
            List = new(
                [
                    new Field("type", FieldKind.Selection, LocKey.AutounattendUserScripts.Type,
                        Options:
                        [
                            LocKey.AutounattendUserScripts.TypePowerShell,
                            LocKey.AutounattendUserScripts.TypeCmd,
                            LocKey.AutounattendUserScripts.TypeBatch,
                            LocKey.AutounattendUserScripts.TypeRegistry,
                        ],
                        Default: "0"),
                    new Field("script", FieldKind.Text, LocKey.AutounattendUserScripts.Script, MultiLine: true),
                ],
                Add: LocKey.AutounattendUserScripts.Add,
                Remove: LocKey.AutounattendUserScripts.Remove),
            Targets = [new AutounattendScript("script", RunContext.User)],
        },
    ];
}
