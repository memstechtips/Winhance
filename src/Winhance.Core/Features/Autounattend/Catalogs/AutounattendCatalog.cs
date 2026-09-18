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
    public const string FeatureName = "Autounattend";

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
                GroupName = LocKey.SettingGroup.Processorarchitecture,
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
                GroupName = LocKey.SettingGroup.Processorarchitecture,
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
                GroupName = LocKey.SettingGroup.Processorarchitecture,
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
                GroupName = LocKey.SettingGroup.Editionandproductkey,
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
                new() { Label = LocKey.Setting.AutounattendEdition.Option0, Set = new Dictionary<string, StateValue> { ["key"] = Of("00000-00000-00000-00000-00000"), ["show"] = Of("Always") }, Roles = [StateRole.WindowsDefault] },
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
                GroupName = LocKey.SettingGroup.Editionandproductkey,
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
            Id = "autounattend-hardware-checks",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendHardwareChecks.Name,
                Description = LocKey.Setting.AutounattendHardwareChecks.Description,
                GroupName = LocKey.SettingGroup.Windows11requirements,
                Icon = MaterialIcons.ShieldCheck,
                AddedInVersion = "26.09.07",
            },
            States =
            [
                new() { Label = LocKey.Common.Enabled },
                new()
                {
                    Label = LocKey.Common.Disabled,
                    Roles = [StateRole.WindowsDefault],
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
                new()
                {
                    Label = LocKey.Setting.AutounattendAccount.Option0,
                    Set = new Dictionary<string, StateValue> { ["hide-online"] = Of("true") },
                    Roles = [StateRole.WindowsDefault],
                    Effects =
                    [
                        new AutounattendCommand("specialize", "Microsoft-Windows-Deployment",
                            "reg.exe add \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\OOBE\" /v BypassNRO /t REG_DWORD /d 1 /f",
                            "Registry Entry to Create a Local Account during OOBE"),
                        new AutounattendCommand("specialize", "Microsoft-Windows-Deployment",
                            "powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command \"Get-NetAdapter | Disable-NetAdapter -Confirm:$false\"",
                            "Disable All Network Adapters Temporarily so Windows Doesn't Update During OOBE and to Allow Local Account Creation"),
                        new AutounattendFirstLogonCommand(
                            "powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command \"Get-NetAdapter | Enable-NetAdapter -Confirm:$false\"",
                            "Enables Network Adapters After OOBE Completes"),
                    ],
                },
                new() { Label = LocKey.Setting.AutounattendAccount.Option1, Set = new Dictionary<string, StateValue> { ["hide-online"] = Absent } },
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
                ],
                // Setup clears the AutoLogon credentials from its cached copy of the file, but not a LocalAccount password.
                new AutounattendFirstLogonCommand(
                    "cmd.exe /c del /q C:\\Windows\\Panther\\unattend.xml",
                    "Deletes the Cached Answer File Carrying the Account Passwords"),
                new Dictionary<string, string> { ["name"] = "name", ["display-name"] = "display-name" }),
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
            Id = "autounattend-licence-page",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendLicencePage.Name,
                Description = LocKey.Setting.AutounattendLicencePage.Description,
                GroupName = LocKey.SettingGroup.Setupscreens,
                Icon = MaterialIcons.FileDocumentCheck,
                AddedInVersion = "26.09.07",
            },
            Targets = [new AutounattendElement("hide", "oobeSystem", "Microsoft-Windows-Shell-Setup", "OOBE/HideEULAPage")],
            States =
            [
                new() { Label = LocKey.Common.Enabled, Set = new Dictionary<string, StateValue> { ["hide"] = Absent } },
                new() { Label = LocKey.Common.Disabled, Set = new Dictionary<string, StateValue> { ["hide"] = Of("true") }, Roles = [StateRole.WindowsDefault] },
            ],
        },
        new()
        {
            Id = "autounattend-oem-registration-page",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendOemRegistrationPage.Name,
                Description = LocKey.Setting.AutounattendOemRegistrationPage.Description,
                GroupName = LocKey.SettingGroup.Setupscreens,
                Icon = MaterialIcons.OfficeBuilding,
                AddedInVersion = "26.09.07",
            },
            Targets = [new AutounattendElement("hide", "oobeSystem", "Microsoft-Windows-Shell-Setup", "OOBE/HideOEMRegistrationScreen")],
            States =
            [
                new() { Label = LocKey.Common.Enabled, Set = new Dictionary<string, StateValue> { ["hide"] = Absent } },
                new() { Label = LocKey.Common.Disabled, Set = new Dictionary<string, StateValue> { ["hide"] = Of("true") }, Roles = [StateRole.WindowsDefault] },
            ],
        },
        new()
        {
            Id = "autounattend-network-page",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendNetworkPage.Name,
                Description = LocKey.Setting.AutounattendNetworkPage.Description,
                GroupName = LocKey.SettingGroup.Setupscreens,
                Icon = MaterialIcons.NetworkOutline,
                AddedInVersion = "26.09.07",
            },
            Targets = [new AutounattendElement("hide", "oobeSystem", "Microsoft-Windows-Shell-Setup", "OOBE/HideWirelessSetupInOOBE")],
            States =
            [
                new() { Label = LocKey.Common.Enabled, Set = new Dictionary<string, StateValue> { ["hide"] = Absent } },
                new() { Label = LocKey.Common.Disabled, Set = new Dictionary<string, StateValue> { ["hide"] = Of("true") }, Roles = [StateRole.WindowsDefault] },
            ],
        },
        new()
        {
            Id = "autounattend-privacy",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendPrivacy.Name,
                Description = LocKey.Setting.AutounattendPrivacy.Description,
                GroupName = LocKey.SettingGroup.Setupscreens,
                Icon = MaterialIcons.ShieldLock,
                AddedInVersion = "26.09.07",
            },
            Targets = [new AutounattendElement("protect", "oobeSystem", "Microsoft-Windows-Shell-Setup", "OOBE/ProtectYourPC")],
            // ProtectYourPC: 3 turns every express setting off, 1 turns them on; with no element Setup asks.
            States =
            [
                new() { Label = LocKey.Setting.AutounattendPrivacy.Option0, Set = new Dictionary<string, StateValue> { ["protect"] = Of("3") }, Roles = [StateRole.WindowsDefault] },
                new() { Label = LocKey.Setting.AutounattendPrivacy.Option1, Set = new Dictionary<string, StateValue> { ["protect"] = Of("1") } },
                new() { Label = LocKey.Setting.AutounattendPrivacy.Option2, Set = new Dictionary<string, StateValue> { ["protect"] = Absent } },
            ],
        },
        new()
        {
            Id = "autounattend-computer-name",
            Display = new()
            {
                Name = LocKey.Setting.AutounattendComputerName.Name,
                Description = LocKey.Setting.AutounattendComputerName.Description,
                GroupName = LocKey.SettingGroup.Setupscreens,
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
                    Roles = [StateRole.WindowsDefault],
                    // /LimitAccess keeps DISM off Windows Update; the media's sources\sxs is searched by drive letter
                    // because the letter Setup gives the media is not known in advance.
                    Effects =
                    [
                        new AutounattendCommand("specialize", "Microsoft-Windows-Deployment",
                            "powershell.exe -NoProfile -WindowStyle Hidden -Command \"foreach($d in 'C','D','E','F','G','H','I','J','K'){$src=Join-Path ($d+':') 'sources\\sxs';if(Test-Path $src\\*.cab){dism /Online /Enable-Feature /FeatureName:NetFx3 /All /LimitAccess /Source:$src;break}}\"",
                            "Enables .NET Framework 3.5 from Windows Installation Media"),
                    ],
                },
                new() { Label = LocKey.Common.Disabled },
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
            States =
            [
                new()
                {
                    Label = LocKey.Common.Enabled,
                    Roles = [StateRole.WindowsDefault],
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
[System.IO.File]::WriteAllBytes($shortcutPath, $bytes)", RunContext.System),
                    ],
                },
                new() { Label = LocKey.Common.Disabled },
            ],
        },
    ];
}
