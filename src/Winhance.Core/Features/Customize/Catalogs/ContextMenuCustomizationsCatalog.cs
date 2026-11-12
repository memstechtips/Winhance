using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using static Winhance.Core.Features.Common.Catalog.StateValue;
using Winhance.Core.Features.Common.Localization;

namespace Winhance.Core.Features.Customize.Catalogs;

public static class ContextMenuCustomizationsCatalog
{
    public const string FeatureId = FeatureIds.ContextMenu;

    public static IReadOnlyList<Setting> All { get; } = new Setting[]
    {
        new()
        {
            Id = "context-menu-classic",
            Display = new()
            {
                Name = LocKey.Setting.ContextMenuClassic.Name,
                Description = LocKey.Setting.ContextMenuClassic.Description,
                GroupName = LocKey.SettingGroup.General,
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
            Id = "context-menu-ask-copilot",
            Display = new()
            {
                Name = LocKey.Setting.ContextMenuAskCopilot.Name,
                Description = LocKey.Setting.ContextMenuAskCopilot.Description,
                GroupName = LocKey.SettingGroup.General,
                Icon = FluentIcons.BotSparkle,
                IsSubjectivePreference = true,
                AddedInVersion = "26.09.30",
            },
            Apply = new() { Restart = new RestartProcess("Explorer") },
            Targets = new Target[]
            {
                new RegTarget("{CB3B0003-8088-4EDE-8769-8B354AB2FF8C}", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Shell Extensions\Blocked" }, "{CB3B0003-8088-4EDE-8769-8B354AB2FF8C}", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["{CB3B0003-8088-4EDE-8769-8B354AB2FF8C}"] = Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["{CB3B0003-8088-4EDE-8769-8B354AB2FF8C}"] = Of("") },
                },
            },
        },
        new()
        {
            Id = "context-menu-windows-terminal",
            Display = new()
            {
                Name = LocKey.Setting.ContextMenuWindowsTerminal.Name,
                Description = LocKey.Setting.ContextMenuWindowsTerminal.Description,
                GroupName = LocKey.SettingGroup.General,
                Icon = MaterialIcons.Console,
                IsSubjectivePreference = true,
            },
            Availability = new() { Builds = new[] { BuildRange.Windows11 } },
            Apply = new() { Restart = new RestartProcess("Explorer") },
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
            Id = "context-menu-compress-to",
            Display = new()
            {
                Name = LocKey.Setting.ContextMenuCompressTo.Name,
                Description = LocKey.Setting.ContextMenuCompressTo.Description,
                GroupName = LocKey.SettingGroup.General,
                Icon = FluentIcons.FolderZip,
                AddedInVersion = "25.04.09",
                IsSubjectivePreference = true,
            },
            Availability = new() { Builds = new[] { BuildRange.Between(26100, int.MaxValue) } },
            Targets = new Target[]
            {
                new RegTarget("ExplorerCommandHandler", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\*\shell\CompressToFullMenu_ForOldContextMenu" }, "ExplorerCommandHandler", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Links = new[] { new Link("context-menu-classic", LinkKind.Requires, LocKey.Common.Enabled) },
                    Set = new Dictionary<string, StateValue> { ["ExplorerCommandHandler"] = Of("{7AE6900F-6EB0-44A2-9CA1-DB2F7EF352AF}") },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

; Credit: ThioJoe - https://github.com/ThioJoe/
; Source: https://gist.github.com/ThioJoe/f4b0799e2f0d95466f4c2bd4e46d1e67

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\*\shell\CompressToFullMenu_ForOldContextMenu]
""CommandStateSync""=""""
""ExplorerCommandHandler""=""{7AE6900F-6EB0-44A2-9CA1-DB2F7EF352AF}""
""MUIVerb""=""@Windows.UI.FileExplorer.dll,-51797""
""Note""=""Copied from original Command Store command: Windows.CompressTo""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Folder\shell\CompressToFullMenu_ForOldContextMenu]
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

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\*\shell\CompressToFullMenu_ForOldContextMenu]

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Folder\shell\CompressToFullMenu_ForOldContextMenu]
") },
                },
            },
        },
        new()
        {
            Id = "context-menu-take-ownership",
            Display = new()
            {
                Name = LocKey.Setting.ContextMenuTakeOwnership.Name,
                Description = LocKey.Setting.ContextMenuTakeOwnership.Description,
                GroupName = LocKey.SettingGroup.FilesAndFolders,
                Icon = MaterialIcons.Security,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\*\shell\TakeOwnership" }, "", RegistryValueKind.String),
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

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\*\shell\TakeOwnership]
[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\*\shell\runas]

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\*\shell\TakeOwnership]
@=""Take Ownership""
""Extended""=-
""HasLUAShield""=""""
""NoWorkingDirectory""=""""
""NeverDefault""=""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\*\shell\TakeOwnership\command]
@=""powershell -windowstyle hidden -command \""Start-Process cmd -ArgumentList '/c takeown /f \\\""%1\\\"" && icacls \\\""%1\\\"" /grant *S-1-3-4:F /t /c /l & pause' -Verb runAs\""""
""IsolatedCommand""=""powershell -windowstyle hidden -command \""Start-Process cmd -ArgumentList '/c takeown /f \\\""%1\\\"" && icacls \\\""%1\\\"" /grant *S-1-3-4:F /t /c /l & pause' -Verb runAs\""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\shell\TakeOwnership]
@=""Take Ownership""
""AppliesTo""=""NOT (System.ItemPathDisplay:=\""C:\\Users\"" OR System.ItemPathDisplay:=\""C:\\ProgramData\"" OR System.ItemPathDisplay:=\""C:\\Windows\"" OR System.ItemPathDisplay:=\""C:\\Windows\\System32\"" OR System.ItemPathDisplay:=\""C:\\Program Files\"" OR System.ItemPathDisplay:=\""C:\\Program Files (x86)\"")""
""Extended""=-
""HasLUAShield""=""""
""NoWorkingDirectory""=""""
""Position""=""middle""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\shell\TakeOwnership\command]
@=""powershell -windowstyle hidden -command \""$Y = ($null | choice).Substring(1,1); Start-Process cmd -ArgumentList ('/c takeown /f \\\""%1\\\"" /r /d ' + $Y + ' && icacls \\\""%1\\\"" /grant *S-1-3-4:F /t /c /l /q & pause') -Verb runAs\""""
""IsolatedCommand""=""powershell -windowstyle hidden -command \""$Y = ($null | choice).Substring(1,1); Start-Process cmd -ArgumentList ('/c takeown /f \\\""%1\\\"" /r /d ' + $Y + ' && icacls \\\""%1\\\"" /grant *S-1-3-4:F /t /c /l /q & pause') -Verb runAs\""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Drive\shell\runas]
@=""Take Ownership""
""Extended""=-
""HasLUAShield""=""""
""NoWorkingDirectory""=""""
""Position""=""middle""
""AppliesTo""=""NOT (System.ItemPathDisplay:=\""C:\\\"")""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Drive\shell\runas\command]
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

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\*\shell\TakeOwnership]
[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\*\shell\runas]
[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\shell\TakeOwnership]
[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Drive\shell\runas]
") },
                },
            },
        },
        new()
        {
            Id = "context-menu-firewall",
            Display = new()
            {
                Name = LocKey.Setting.ContextMenuFirewall.Name,
                Description = LocKey.Setting.ContextMenuFirewall.Description,
                GroupName = LocKey.SettingGroup.FilesAndFolders,
                Icon = MaterialIcons.ShieldLock,
                AddedInVersion = "26.10.16",
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("MUIVerb", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\exefile\shell\WinhanceFirewall" }, "MUIVerb", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Of("Windows Firewall") },
                    // Each command touches only the two rules it names, so a rule an installer added is never removed.
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\exefile\shell\WinhanceFirewall]

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\exefile\shell\WinhanceFirewall]
""MUIVerb""=""Windows Firewall""
""Icon""=""FirewallControlPanel.dll,0""
""SubCommands""=""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\exefile\shell\WinhanceFirewall\shell\01Allow]
""MUIVerb""=""Allow through Windows Firewall""
""HasLUAShield""=""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\exefile\shell\WinhanceFirewall\shell\01Allow\command]
@=""powershell -windowstyle hidden -command \""Start-Process cmd -WindowStyle Hidden -Verb runAs -ArgumentList '/c netsh advfirewall firewall delete rule name=\\\""Winhance allow: %1\\\"" & netsh advfirewall firewall delete rule name=\\\""Winhance block: %1\\\"" & netsh advfirewall firewall add rule name=\\\""Winhance allow: %1\\\"" dir=in action=allow program=\\\""%1\\\"" enable=yes & netsh advfirewall firewall add rule name=\\\""Winhance allow: %1\\\"" dir=out action=allow program=\\\""%1\\\"" enable=yes'\""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\exefile\shell\WinhanceFirewall\shell\02Block]
""MUIVerb""=""Block in Windows Firewall""
""HasLUAShield""=""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\exefile\shell\WinhanceFirewall\shell\02Block\command]
@=""powershell -windowstyle hidden -command \""Start-Process cmd -WindowStyle Hidden -Verb runAs -ArgumentList '/c netsh advfirewall firewall delete rule name=\\\""Winhance allow: %1\\\"" & netsh advfirewall firewall delete rule name=\\\""Winhance block: %1\\\"" & netsh advfirewall firewall add rule name=\\\""Winhance block: %1\\\"" dir=in action=block program=\\\""%1\\\"" enable=yes & netsh advfirewall firewall add rule name=\\\""Winhance block: %1\\\"" dir=out action=block program=\\\""%1\\\"" enable=yes'\""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\exefile\shell\WinhanceFirewall\shell\03Remove]
""MUIVerb""=""Remove Winhance rules""
""HasLUAShield""=""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\exefile\shell\WinhanceFirewall\shell\03Remove\command]
@=""powershell -windowstyle hidden -command \""Start-Process cmd -WindowStyle Hidden -Verb runAs -ArgumentList '/c netsh advfirewall firewall delete rule name=\\\""Winhance allow: %1\\\"" & netsh advfirewall firewall delete rule name=\\\""Winhance block: %1\\\""'\""""
") },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Absent },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\exefile\shell\WinhanceFirewall]
") },
                },
            },
        },
        new()
        {
            Id = "context-menu-toggle-extensions",
            Display = new()
            {
                Name = LocKey.Setting.ContextMenuToggleExtensions.Name,
                Description = LocKey.Setting.ContextMenuToggleExtensions.Description,
                GroupName = LocKey.SettingGroup.FilesAndFolders,
                Icon = FluentIcons.DocumentQuestionMark,
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("ExplorerCommandHandler", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\AllFilesystemObjects\shell\Windows.ShowFileExtensions" }, "ExplorerCommandHandler", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Links = new[] { new Link("context-menu-classic", LinkKind.Requires, LocKey.Common.Enabled) },
                    Set = new Dictionary<string, StateValue> { ["ExplorerCommandHandler"] = Of("{4ac6c205-2853-4bf5-b47c-919a42a48a16}") },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\AllFilesystemObjects\shell\Windows.ShowFileExtensions]
""CommandStateSync""=""""
""Description""=""@shell32.dll,-37571""
""ExplorerCommandHandler""=""{4ac6c205-2853-4bf5-b47c-919a42a48a16}""
""MUIVerb""=""@shell32.dll,-37570""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Windows.ShowFileExtensions]
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

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\AllFilesystemObjects\shell\Windows.ShowFileExtensions]
[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Windows.ShowFileExtensions]
") },
                },
            },
        },
        new()
        {
            Id = "context-menu-ps1-edit-run",
            Display = new()
            {
                Name = LocKey.Setting.ContextMenuPs1EditRun.Name,
                Description = LocKey.Setting.ContextMenuPs1EditRun.Description,
                GroupName = LocKey.SettingGroup.FilesAndFolders,
                Icon = MaterialIcons.Powershell,
                AddedInVersion = "25.04.09",
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("MUIVerb", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\SystemFileAssociations\.ps1\Shell\Edit-Run-with" }, "MUIVerb", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Links = new[] { new Link("context-menu-classic", LinkKind.Requires, LocKey.Common.Enabled) },
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Of("Edit or Run with") },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

; Created by: Shawn Brink
; Created on: December 4, 2023
; Tutorial: https://www.elevenforum.com/t/add-edit-or-run-with-to-ps1-file-context-menu-in-windows-11.20366/

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\SystemFileAssociations\.ps1\Shell\Edit-Run-with]
""MUIVerb""=""Edit or Run with""
""SubCommands""=""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\001flyout]
""MUIVerb""=""Run with PowerShell""
""Icon""=""powershell.exe""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\001flyout\Command]
@=""\""C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe\"" \""-Command\"" \""if((Get-ExecutionPolicy ) -ne 'AllSigned') { Set-ExecutionPolicy -Scope Process Bypass }; & '%1'\""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\002flyout]
""MUIVerb""=""Run with PowerShell as administrator""
""HasLUAShield""=""""
""Icon""=""powershell.exe""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\002flyout\Command]
@=""\""C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe\"" \""-Command\"" \""\""& {Start-Process PowerShell.exe -ArgumentList '-ExecutionPolicy RemoteSigned -File \\\""%1\\\""' -Verb RunAs}\""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\003flyout]
""MUIVerb""=""Run with PowerShell 7""
""Icon""=""pwsh.exe""
""CommandFlags""=dword:00000020

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\003flyout\Command]
@=""\""C:\\Program Files\\PowerShell\\7\\pwsh.exe\"" \""-Command\"" \""if((Get-ExecutionPolicy ) -ne 'AllSigned') { Set-ExecutionPolicy -Scope Process Bypass }; & '%1'\""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\004flyout]
""MUIVerb""=""Run with PowerShell 7 as administrator""
""HasLUAShield""=""""
""Icon""=""pwsh.exe""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\004flyout\Command]
@=""\""C:\\Program Files\\PowerShell\\7\\pwsh.exe\"" \""-Command\"" \""\""& {Start-Process pwsh.exe -ArgumentList '-ExecutionPolicy RemoteSigned -File \\\""%1\\\""' -Verb RunAs}\""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\005flyout]
""MUIVerb""=""Edit with PowerShell ISE""
""Icon""=""powershell_ise.exe""
""CommandFlags""=dword:00000020

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\005flyout\Command]
@=""\""C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell_ise.exe\"" \""%1\""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\006flyout]
""MUIVerb""=""Edit with PowerShell ISE as administrator""
""HasLUAShield""=""""
""Icon""=""powershell_ise.exe""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\006flyout\Command]
@=""PowerShell -windowstyle hidden -Command \""Start-Process cmd -ArgumentList '/s,/c,start PowerShell_ISE.exe \""\""%1\""\""'  -Verb RunAs\""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\007flyout]
""MUIVerb""=""Edit with PowerShell ISE (x86)""
""Icon""=""powershell_ise.exe""
""CommandFlags""=dword:00000020

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\007flyout\Command]
@=""\""C:\\WINDOWS\\syswow64\\WindowsPowerShell\\v1.0\\powershell_ise.exe\"" \""%1\""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\008flyout]
""MUIVerb""=""Edit with PowerShell ISE (x86) as administrator""
""HasLUAShield""=""""
""Icon""=""powershell_ise.exe""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\008flyout\Command]
@=""PowerShell -windowstyle hidden -Command \""Start-Process cmd -ArgumentList '/s,/c,start C:\\WINDOWS\\syswow64\\WindowsPowerShell\\v1.0\\powershell_ise.exe \""\""%1\""\""'  -Verb RunAs\""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\009flyout]
""MUIVerb""=""Edit with Notepad""
""Icon""=""notepad.exe""
""CommandFlags""=dword:00000020

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\009flyout\Command]
@=""\""C:\\Windows\\System32\\notepad.exe\"" \""%1\""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\010flyout]
""MUIVerb""=""Edit with Notepad as administrator""
""HasLUAShield""=""""
""Icon""=""notepad.exe""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\SystemFileAssociations\.ps1\Shell\Edit-Run-with\shell\010flyout\Command]
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

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\SystemFileAssociations\.ps1\Shell\Edit-Run-with]
") },
                },
            },
        },
        new()
        {
            Id = "context-menu-repair-menu",
            Display = new()
            {
                Name = LocKey.Setting.ContextMenuRepairMenu.Name,
                Description = LocKey.Setting.ContextMenuRepairMenu.Description,
                GroupName = LocKey.SettingGroup.RepairAndMaintenance,
                Icon = MaterialIcons.ProgressWrench,
                AddedInVersion = "26.11.10",
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("MUIVerb", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance" }, "MUIVerb", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Links = new[] { new Link("context-menu-classic", LinkKind.Requires, LocKey.Common.Enabled) },
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Of("Repair and Maintenance") },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance]
""MUIVerb""=""Repair and Maintenance""
""Icon""=""WmiPrvSE.exe""
""Position""=""Bottom""
""SubCommands""=""""
") },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Absent },
                    Controls = new Dictionary<string, LocKey> { ["context-menu-repair-windows"] = LocKey.Common.Disabled, ["context-menu-sfc"] = LocKey.Common.Disabled, ["context-menu-dism"] = LocKey.Common.Disabled, ["context-menu-chkdsk"] = LocKey.Common.Disabled, ["context-menu-restore-point"] = LocKey.Common.Disabled, ["context-menu-disk-cleanup"] = LocKey.Common.Disabled, ["context-menu-optimize-drives"] = LocKey.Common.Disabled, ["context-menu-memory-diagnostic"] = LocKey.Common.Disabled, ["context-menu-icon-cache"] = LocKey.Common.Disabled },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance]
") },
                },
            },
        },
        new()
        {
            Id = "context-menu-repair-windows",
            Display = new()
            {
                Name = LocKey.Setting.ContextMenuRepairWindows.Name,
                Description = LocKey.Setting.ContextMenuRepairWindows.Description,
                GroupName = LocKey.SettingGroup.RepairAndMaintenance,
                Icon = MaterialIcons.Wrench,
                AddedInVersion = "26.11.10",
                IsSubjectivePreference = true,
            },
            UiParentId = "context-menu-repair-menu",
            Targets = new Target[]
            {
                new RegTarget("MUIVerb", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\01RepairWindows" }, "MUIVerb", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Links = new[] { new Link("context-menu-classic", LinkKind.Requires, LocKey.Common.Enabled), new Link("context-menu-repair-menu", LinkKind.Requires, LocKey.Common.Enabled) },
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Of("Repair Windows (DISM + SFC)") },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairWindows]

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\01RepairWindows]
""MUIVerb""=""Repair Windows (DISM + SFC)""
""Icon""=""WmiPrvSE.exe""
""HasLUAShield""=""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\01RepairWindows\command]
@=""PowerShell -ExecutionPolicy Bypass -windowstyle hidden -command \""Start-Process cmd -ArgumentList '/s,/k, DISM /Online /Cleanup-Image /RestoreHealth & sfc /scannow' -Verb runAs\""""
") },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Absent },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\01RepairWindows]

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairWindows]
") },
                },
            },
        },
        new()
        {
            Id = "context-menu-sfc",
            Display = new()
            {
                Name = LocKey.Setting.ContextMenuSfc.Name,
                Description = LocKey.Setting.ContextMenuSfc.Description,
                GroupName = LocKey.SettingGroup.RepairAndMaintenance,
                Icon = MaterialIcons.MagnifyScan,
                AddedInVersion = "25.04.09",
                IsSubjectivePreference = true,
            },
            UiParentId = "context-menu-repair-menu",
            Targets = new Target[]
            {
                new RegTarget("MUIVerb", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\02SFC" }, "MUIVerb", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Links = new[] { new Link("context-menu-classic", LinkKind.Requires, LocKey.Common.Enabled), new Link("context-menu-repair-menu", LinkKind.Requires, LocKey.Common.Enabled) },
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Of("SFC /SCANNOW") },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\SFC]

; Created by: Shawn Brink
; Created on: March 12, 2020
; Tutorial: https://www.tenforums.com/tutorials/152128-how-add-sfc-scannow-context-menu-windows-10-a.html

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\02SFC]
""Icon""=""WmiPrvSE.exe""
""MUIVerb""=""SFC /SCANNOW""
""Extended""=-
""SubCommands""=""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\02SFC\shell\001menu]
""HasLUAShield""=""""
""MUIVerb""=""Run SFC /SCANNOW""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\02SFC\shell\001menu\command]
@=""PowerShell -ExecutionPolicy Bypass -windowstyle hidden -command \""Start-Process cmd -ArgumentList '/s,/k, sfc /scannow' -Verb runAs\""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\02SFC\shell\002menu]
""MUIVerb""=""SFC scan details log""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\02SFC\shell\002menu\command]
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

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\02SFC]

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\SFC]
") },
                },
            },
        },
        new()
        {
            Id = "context-menu-dism",
            Display = new()
            {
                Name = LocKey.Setting.ContextMenuDism.Name,
                Description = LocKey.Setting.ContextMenuDism.Description,
                GroupName = LocKey.SettingGroup.RepairAndMaintenance,
                Icon = MaterialIcons.MedicalBag,
                AddedInVersion = "26.11.10",
                IsSubjectivePreference = true,
            },
            UiParentId = "context-menu-repair-menu",
            Targets = new Target[]
            {
                new RegTarget("MUIVerb", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\03RepairWindowsImage" }, "MUIVerb", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Links = new[] { new Link("context-menu-classic", LinkKind.Requires, LocKey.Common.Enabled), new Link("context-menu-repair-menu", LinkKind.Requires, LocKey.Common.Enabled) },
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = OneOf("DISM", "Repair Windows Image") },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairWindowsImage]

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\03RepairWindowsImage]

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\03RepairWindowsImage]
""MUIVerb""=""DISM""
""Icon""=""WmiPrvSE.exe""
""SubCommands""=""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\03RepairWindowsImage\shell\001menu]
""HasLUAShield""=""""
""MUIVerb""=""Check health (quick)""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\03RepairWindowsImage\shell\001menu\command]
@=""PowerShell -ExecutionPolicy Bypass -windowstyle hidden -command \""Start-Process cmd -ArgumentList '/s,/k, DISM /Online /Cleanup-Image /CheckHealth' -Verb runAs\""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\03RepairWindowsImage\shell\002menu]
""HasLUAShield""=""""
""MUIVerb""=""Scan health""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\03RepairWindowsImage\shell\002menu\command]
@=""PowerShell -ExecutionPolicy Bypass -windowstyle hidden -command \""Start-Process cmd -ArgumentList '/s,/k, DISM /Online /Cleanup-Image /ScanHealth' -Verb runAs\""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\03RepairWindowsImage\shell\003menu]
""HasLUAShield""=""""
""MUIVerb""=""Restore health (repair)""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\03RepairWindowsImage\shell\003menu\command]
@=""PowerShell -ExecutionPolicy Bypass -windowstyle hidden -command \""Start-Process cmd -ArgumentList '/s,/k, DISM /Online /Cleanup-Image /RestoreHealth' -Verb runAs\""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\03RepairWindowsImage\shell\004menu]
""HasLUAShield""=""""
""MUIVerb""=""Clean up components""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\03RepairWindowsImage\shell\004menu\command]
@=""PowerShell -ExecutionPolicy Bypass -windowstyle hidden -command \""Start-Process cmd -ArgumentList '/s,/k, DISM /Online /Cleanup-Image /StartComponentCleanup' -Verb runAs\""""
") },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Absent },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\03RepairWindowsImage]

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairWindowsImage]
") },
                },
            },
        },
        new()
        {
            Id = "context-menu-chkdsk",
            Display = new()
            {
                Name = LocKey.Setting.ContextMenuChkdsk.Name,
                Description = LocKey.Setting.ContextMenuChkdsk.Description,
                GroupName = LocKey.SettingGroup.RepairAndMaintenance,
                Icon = MaterialIcons.Harddisk,
                AddedInVersion = "25.04.09",
                IsSubjectivePreference = true,
            },
            UiParentId = "context-menu-repair-menu",
            Targets = new Target[]
            {
                new RegTarget("MUIVerb", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\04CHKDSK" }, "MUIVerb", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Links = new[] { new Link("context-menu-classic", LinkKind.Requires, LocKey.Common.Enabled), new Link("context-menu-repair-menu", LinkKind.Requires, LocKey.Common.Enabled) },
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Of("CHKDSK") },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\CHKDSK]

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\04CHKDSK]
""Icon""=""imageres.dll,-36""
""MUIVerb""=""CHKDSK""
""SubCommands""=""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\04CHKDSK\shell\001menu]
""HasLUAShield""=""""
""MUIVerb""=""Run CHKDSK (scan only)""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\04CHKDSK\shell\001menu\command]
@=""PowerShell -ExecutionPolicy Bypass -windowstyle hidden -command \""Start-Process cmd -ArgumentList '/v:on,/s,/k, set /p d=Enter drive letter (e.g. C): & chkdsk !d!:' -Verb runAs\""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\04CHKDSK\shell\002menu]
""HasLUAShield""=""""
""MUIVerb""=""Run CHKDSK /F (fix errors)""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\04CHKDSK\shell\002menu\command]
@=""PowerShell -ExecutionPolicy Bypass -windowstyle hidden -command \""Start-Process cmd -ArgumentList '/v:on,/s,/k, set /p d=Enter drive letter (e.g. C): & chkdsk !d!: /f' -Verb runAs\""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\04CHKDSK\shell\003menu]
""HasLUAShield""=""""
""MUIVerb""=""Run CHKDSK /R (locate bad sectors)""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\04CHKDSK\shell\003menu\command]
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

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\04CHKDSK]

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\CHKDSK]
") },
                },
            },
        },
        new()
        {
            Id = "context-menu-restore-point",
            Display = new()
            {
                Name = LocKey.Setting.ContextMenuRestorePoint.Name,
                Description = LocKey.Setting.ContextMenuRestorePoint.Description,
                GroupName = LocKey.SettingGroup.RepairAndMaintenance,
                Icon = MaterialIcons.BackupRestore,
                AddedInVersion = "26.11.10",
                IsSubjectivePreference = true,
            },
            UiParentId = "context-menu-repair-menu",
            Targets = new Target[]
            {
                new RegTarget("MUIVerb", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\05CreateRestorePoint" }, "MUIVerb", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Links = new[] { new Link("context-menu-classic", LinkKind.Requires, LocKey.Common.Enabled), new Link("context-menu-repair-menu", LinkKind.Requires, LocKey.Common.Enabled) },
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Of("Create Restore Point") },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\CreateRestorePoint]

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\05CreateRestorePoint]
""MUIVerb""=""Create Restore Point""
""Icon""=""rstrui.exe""
""HasLUAShield""=""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\05CreateRestorePoint\command]
@=""PowerShell -ExecutionPolicy Bypass -windowstyle hidden -command \""Start-Process PowerShell -ArgumentList '-NoExit -ExecutionPolicy Bypass -Command $k=''HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\SystemRestore''; $old=(Get-ItemProperty $k).SystemRestorePointCreationFrequency; Set-ItemProperty $k SystemRestorePointCreationFrequency 0 -Type DWord; try { Checkpoint-Computer -Description ''Winhance context menu'' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop; Write-Host ''Restore point created.'' } catch { Write-Host $_.Exception.Message } finally { if ($null -eq $old) { Remove-ItemProperty $k SystemRestorePointCreationFrequency } else { Set-ItemProperty $k SystemRestorePointCreationFrequency $old } }' -Verb runAs\""""
") },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Absent },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\05CreateRestorePoint]

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\CreateRestorePoint]
") },
                },
            },
        },
        new()
        {
            Id = "context-menu-disk-cleanup",
            Display = new()
            {
                Name = LocKey.Setting.ContextMenuDiskCleanup.Name,
                Description = LocKey.Setting.ContextMenuDiskCleanup.Description,
                GroupName = LocKey.SettingGroup.RepairAndMaintenance,
                Icon = MaterialIcons.Broom,
                AddedInVersion = "26.11.10",
                IsSubjectivePreference = true,
            },
            UiParentId = "context-menu-repair-menu",
            Targets = new Target[]
            {
                new RegTarget("MUIVerb", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\06DiskCleanup" }, "MUIVerb", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Links = new[] { new Link("context-menu-classic", LinkKind.Requires, LocKey.Common.Enabled), new Link("context-menu-repair-menu", LinkKind.Requires, LocKey.Common.Enabled) },
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Of("Disk Cleanup") },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\DiskCleanup]

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\06DiskCleanup]
""MUIVerb""=""Disk Cleanup""
""Icon""=""cleanmgr.exe""
""SubCommands""=""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\06DiskCleanup\shell\001menu]
""MUIVerb""=""Open Disk Cleanup""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\06DiskCleanup\shell\001menu\command]
@=""cleanmgr.exe""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\06DiskCleanup\shell\002menu]
""HasLUAShield""=""""
""MUIVerb""=""Delete temporary files""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\06DiskCleanup\shell\002menu\command]
@=""PowerShell -ExecutionPolicy Bypass -windowstyle hidden -command \""Start-Process PowerShell -ArgumentList '-NoExit -ExecutionPolicy Bypass -Command Remove-Item $env:TEMP\\* -Recurse -Force -ErrorAction SilentlyContinue; Remove-Item $env:SystemRoot\\Temp\\* -Recurse -Force -ErrorAction SilentlyContinue; Write-Host ''Temporary files cleaned.''' -Verb runAs\""""
") },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Absent },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\06DiskCleanup]

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\DiskCleanup]
") },
                },
            },
        },
        new()
        {
            Id = "context-menu-optimize-drives",
            Display = new()
            {
                Name = LocKey.Setting.ContextMenuOptimizeDrives.Name,
                Description = LocKey.Setting.ContextMenuOptimizeDrives.Description,
                GroupName = LocKey.SettingGroup.RepairAndMaintenance,
                Icon = MaterialIcons.Speedometer,
                AddedInVersion = "26.11.10",
                IsSubjectivePreference = true,
            },
            UiParentId = "context-menu-repair-menu",
            Targets = new Target[]
            {
                new RegTarget("MUIVerb", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\07OptimizeDrives" }, "MUIVerb", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Links = new[] { new Link("context-menu-classic", LinkKind.Requires, LocKey.Common.Enabled), new Link("context-menu-repair-menu", LinkKind.Requires, LocKey.Common.Enabled) },
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Of("Optimize Drives") },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\OptimizeDrives]

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\07OptimizeDrives]
""MUIVerb""=""Optimize Drives""
""Icon""=""dfrgui.exe""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\07OptimizeDrives\command]
@=""dfrgui.exe""
") },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Absent },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\07OptimizeDrives]

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\OptimizeDrives]
") },
                },
            },
        },
        new()
        {
            Id = "context-menu-memory-diagnostic",
            Display = new()
            {
                Name = LocKey.Setting.ContextMenuMemoryDiagnostic.Name,
                Description = LocKey.Setting.ContextMenuMemoryDiagnostic.Description,
                GroupName = LocKey.SettingGroup.RepairAndMaintenance,
                Icon = MaterialIcons.Memory,
                AddedInVersion = "26.11.10",
                IsSubjectivePreference = true,
            },
            UiParentId = "context-menu-repair-menu",
            Targets = new Target[]
            {
                new RegTarget("MUIVerb", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\08MemoryDiagnostic" }, "MUIVerb", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Links = new[] { new Link("context-menu-classic", LinkKind.Requires, LocKey.Common.Enabled), new Link("context-menu-repair-menu", LinkKind.Requires, LocKey.Common.Enabled) },
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Of("Windows Memory Diagnostic") },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\MemoryDiagnostic]

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\08MemoryDiagnostic]
""MUIVerb""=""Windows Memory Diagnostic""
""Icon""=""mdsched.exe""
""HasLUAShield""=""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\08MemoryDiagnostic\command]
@=""mdsched.exe""
") },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Absent },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\08MemoryDiagnostic]

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\MemoryDiagnostic]
") },
                },
            },
        },
        new()
        {
            Id = "context-menu-icon-cache",
            Display = new()
            {
                Name = LocKey.Setting.ContextMenuIconCache.Name,
                Description = LocKey.Setting.ContextMenuIconCache.Description,
                GroupName = LocKey.SettingGroup.RepairAndMaintenance,
                Icon = MaterialIcons.Cached,
                AddedInVersion = "26.11.10",
                IsSubjectivePreference = true,
            },
            UiParentId = "context-menu-repair-menu",
            Targets = new Target[]
            {
                new RegTarget("MUIVerb", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\09RebuildIconCache" }, "MUIVerb", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Links = new[] { new Link("context-menu-classic", LinkKind.Requires, LocKey.Common.Enabled), new Link("context-menu-repair-menu", LinkKind.Requires, LocKey.Common.Enabled) },
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Of("Rebuild Icon Cache") },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RebuildIconCache]

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\09RebuildIconCache]
""MUIVerb""=""Rebuild Icon Cache""
""Icon""=""imageres.dll,-1401""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\09RebuildIconCache\command]
@=""PowerShell -ExecutionPolicy Bypass -windowstyle hidden -command \""$p=Get-Process explorer -ErrorAction SilentlyContinue; $p | Stop-Process -Force; $p | Wait-Process -Timeout 5 -ErrorAction SilentlyContinue; Remove-Item $env:LOCALAPPDATA\\IconCache.db -Force -ErrorAction SilentlyContinue; Remove-Item $env:LOCALAPPDATA\\Microsoft\\Windows\\Explorer\\*cache_*.db -Force -ErrorAction SilentlyContinue; Start-Sleep 2; if (-not (Get-Process explorer -ErrorAction SilentlyContinue)) { Start-Process explorer }\""""
") },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Absent },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RepairAndMaintenance\shell\09RebuildIconCache]

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RebuildIconCache]
") },
                },
            },
        },
        new()
        {
            Id = "context-menu-troubleshooting-menu",
            Display = new()
            {
                Name = LocKey.Setting.ContextMenuTroubleshootingMenu.Name,
                Description = LocKey.Setting.ContextMenuTroubleshootingMenu.Description,
                GroupName = LocKey.SettingGroup.Troubleshooting,
                Icon = MaterialIcons.Stethoscope,
                AddedInVersion = "26.11.10",
                IsSubjectivePreference = true,
            },
            Targets = new Target[]
            {
                new RegTarget("MUIVerb", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting" }, "MUIVerb", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Links = new[] { new Link("context-menu-classic", LinkKind.Requires, LocKey.Common.Enabled) },
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Of("Troubleshooting") },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting]
""MUIVerb""=""Troubleshooting""
""Icon""=""msconfig.exe""
""Position""=""Bottom""
""SubCommands""=""""
") },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Absent },
                    Controls = new Dictionary<string, LocKey> { ["context-menu-restart-explorer"] = LocKey.Common.Disabled, ["context-menu-restart-into"] = LocKey.Common.Disabled, ["context-menu-network"] = LocKey.Common.Disabled, ["context-menu-end-frozen-apps"] = LocKey.Common.Disabled, ["context-menu-store-reset"] = LocKey.Common.Disabled, ["context-menu-reliability-monitor"] = LocKey.Common.Disabled, ["context-menu-battery-report"] = LocKey.Common.Disabled },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting]
") },
                },
            },
        },
        new()
        {
            Id = "context-menu-restart-explorer",
            Display = new()
            {
                Name = LocKey.Setting.ContextMenuRestartExplorer.Name,
                Description = LocKey.Setting.ContextMenuRestartExplorer.Description,
                GroupName = LocKey.SettingGroup.Troubleshooting,
                Icon = MaterialIcons.FolderRefresh,
                AddedInVersion = "26.11.10",
                IsSubjectivePreference = true,
            },
            UiParentId = "context-menu-troubleshooting-menu",
            Targets = new Target[]
            {
                new RegTarget("MUIVerb", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\01RestartExplorer" }, "MUIVerb", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Links = new[] { new Link("context-menu-classic", LinkKind.Requires, LocKey.Common.Enabled), new Link("context-menu-troubleshooting-menu", LinkKind.Requires, LocKey.Common.Enabled) },
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Of("Restart Explorer") },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RestartExplorer]

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\01RestartExplorer]
""MUIVerb""=""Restart Explorer""
""Icon""=""explorer.exe""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\01RestartExplorer\command]
@=""PowerShell -ExecutionPolicy Bypass -windowstyle hidden -command \""$p=Get-Process explorer -ErrorAction SilentlyContinue; $p | Stop-Process -Force; $p | Wait-Process -Timeout 5 -ErrorAction SilentlyContinue; Start-Sleep 2; if (-not (Get-Process explorer -ErrorAction SilentlyContinue)) { Start-Process explorer }\""""
") },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Absent },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\01RestartExplorer]

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RestartExplorer]
") },
                },
            },
        },
        new()
        {
            Id = "context-menu-restart-into",
            Display = new()
            {
                Name = LocKey.Setting.ContextMenuRestartInto.Name,
                Description = LocKey.Setting.ContextMenuRestartInto.Description,
                GroupName = LocKey.SettingGroup.Troubleshooting,
                Icon = MaterialIcons.Restart,
                AddedInVersion = "26.11.10",
                IsSubjectivePreference = true,
            },
            UiParentId = "context-menu-troubleshooting-menu",
            Targets = new Target[]
            {
                new RegTarget("MUIVerb", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\02RestartInto" }, "MUIVerb", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Links = new[] { new Link("context-menu-classic", LinkKind.Requires, LocKey.Common.Enabled), new Link("context-menu-troubleshooting-menu", LinkKind.Requires, LocKey.Common.Enabled) },
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Of("Restart Into") },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RestartInto]

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\02RestartInto]
""MUIVerb""=""Restart Into""
""Icon""=""shell32.dll,-27""
""SubCommands""=""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\02RestartInto\shell\001menu]
""HasLUAShield""=""""
""MUIVerb""=""Advanced startup""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\02RestartInto\shell\001menu\command]
@=""PowerShell -ExecutionPolicy Bypass -windowstyle hidden -command \""Start-Process cmd -ArgumentList '/s,/c, choice /c YN /m \\\""Restart into Advanced startup now\\\"" & if not errorlevel 2 (shutdown /r /o /t 0 || pause)' -Verb runAs\""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\02RestartInto\shell\002menu]
""HasLUAShield""=""""
""MUIVerb""=""UEFI firmware settings""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\02RestartInto\shell\002menu\command]
@=""PowerShell -ExecutionPolicy Bypass -windowstyle hidden -command \""Start-Process cmd -ArgumentList '/s,/c, choice /c YN /m \\\""Restart into the UEFI firmware settings now\\\"" & if not errorlevel 2 (shutdown /r /fw /t 0 || pause)' -Verb runAs\""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\02RestartInto\shell\003menu]
""HasLUAShield""=""""
""MUIVerb""=""Safe Mode""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\02RestartInto\shell\003menu\command]
@=""PowerShell -ExecutionPolicy Bypass -windowstyle hidden -command \""Start-Process cmd -ArgumentList '/s,/c, choice /c YN /m \\\""Restart into Safe Mode now\\\"" & if not errorlevel 2 (bcdedit /set {current} safeboot minimal && shutdown /r /t 0 || pause)' -Verb runAs\""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\02RestartInto\shell\004menu]
""HasLUAShield""=""""
""MUIVerb""=""Normal mode (leave Safe Mode)""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\02RestartInto\shell\004menu\command]
@=""PowerShell -ExecutionPolicy Bypass -windowstyle hidden -command \""Start-Process cmd -ArgumentList '/s,/c, choice /c YN /m \\\""Turn off Safe Mode and restart now\\\"" & if not errorlevel 2 (bcdedit /deletevalue {current} safeboot && shutdown /r /t 0 || pause)' -Verb runAs\""""
") },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Absent },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\02RestartInto]

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\RestartInto]
") },
                },
            },
        },
        new()
        {
            Id = "context-menu-network",
            Display = new()
            {
                Name = LocKey.Setting.ContextMenuNetwork.Name,
                Description = LocKey.Setting.ContextMenuNetwork.Description,
                GroupName = LocKey.SettingGroup.Troubleshooting,
                Icon = MaterialIcons.LanConnect,
                AddedInVersion = "26.11.10",
                IsSubjectivePreference = true,
            },
            UiParentId = "context-menu-troubleshooting-menu",
            Targets = new Target[]
            {
                new RegTarget("MUIVerb", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\03NetworkTools" }, "MUIVerb", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Links = new[] { new Link("context-menu-classic", LinkKind.Requires, LocKey.Common.Enabled), new Link("context-menu-troubleshooting-menu", LinkKind.Requires, LocKey.Common.Enabled) },
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Of("Network") },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\NetworkTools]

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\03NetworkTools]
""MUIVerb""=""Network""
""Icon""=""shell32.dll,-18""
""SubCommands""=""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\03NetworkTools\shell\001menu]
""HasLUAShield""=""""
""MUIVerb""=""Flush DNS cache""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\03NetworkTools\shell\001menu\command]
@=""PowerShell -ExecutionPolicy Bypass -windowstyle hidden -command \""Start-Process cmd -ArgumentList '/s,/k, ipconfig /flushdns' -Verb runAs\""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\03NetworkTools\shell\002menu]
""HasLUAShield""=""""
""MUIVerb""=""Renew IP address""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\03NetworkTools\shell\002menu\command]
@=""PowerShell -ExecutionPolicy Bypass -windowstyle hidden -command \""Start-Process cmd -ArgumentList '/s,/k, ipconfig /release & ipconfig /renew' -Verb runAs\""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\03NetworkTools\shell\003menu]
""HasLUAShield""=""""
""MUIVerb""=""Reset Winsock""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\03NetworkTools\shell\003menu\command]
@=""PowerShell -ExecutionPolicy Bypass -windowstyle hidden -command \""Start-Process cmd -ArgumentList '/s,/k, netsh winsock reset' -Verb runAs\""""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\03NetworkTools\shell\004menu]
""HasLUAShield""=""""
""MUIVerb""=""Reset TCP/IP""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\03NetworkTools\shell\004menu\command]
@=""PowerShell -ExecutionPolicy Bypass -windowstyle hidden -command \""Start-Process cmd -ArgumentList '/s,/k, netsh int ip reset' -Verb runAs\""""
") },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Absent },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\03NetworkTools]

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\NetworkTools]
") },
                },
            },
        },
        new()
        {
            Id = "context-menu-end-frozen-apps",
            Display = new()
            {
                Name = LocKey.Setting.ContextMenuEndFrozenApps.Name,
                Description = LocKey.Setting.ContextMenuEndFrozenApps.Description,
                GroupName = LocKey.SettingGroup.Troubleshooting,
                Icon = MaterialIcons.CloseOctagon,
                AddedInVersion = "26.11.10",
                IsSubjectivePreference = true,
            },
            UiParentId = "context-menu-troubleshooting-menu",
            Targets = new Target[]
            {
                new RegTarget("MUIVerb", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\04EndFrozenApps" }, "MUIVerb", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Links = new[] { new Link("context-menu-classic", LinkKind.Requires, LocKey.Common.Enabled), new Link("context-menu-troubleshooting-menu", LinkKind.Requires, LocKey.Common.Enabled) },
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Of("End Apps Not Responding") },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\EndFrozenApps]

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\04EndFrozenApps]
""MUIVerb""=""End Apps Not Responding""
""Icon""=""taskmgr.exe""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\04EndFrozenApps\command]
@=""cmd /k taskkill /f /fi \""status eq not responding\""""
") },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Absent },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\04EndFrozenApps]

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\EndFrozenApps]
") },
                },
            },
        },
        new()
        {
            Id = "context-menu-store-reset",
            Display = new()
            {
                Name = LocKey.Setting.ContextMenuStoreReset.Name,
                Description = LocKey.Setting.ContextMenuStoreReset.Description,
                GroupName = LocKey.SettingGroup.Troubleshooting,
                Icon = MaterialIcons.StorefrontOutline,
                AddedInVersion = "26.11.10",
                IsSubjectivePreference = true,
            },
            UiParentId = "context-menu-troubleshooting-menu",
            Targets = new Target[]
            {
                new RegTarget("MUIVerb", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\05ResetStoreCache" }, "MUIVerb", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Links = new[] { new Link("context-menu-classic", LinkKind.Requires, LocKey.Common.Enabled), new Link("context-menu-troubleshooting-menu", LinkKind.Requires, LocKey.Common.Enabled) },
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Of("Reset Microsoft Store Cache") },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\ResetStoreCache]

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\05ResetStoreCache]
""MUIVerb""=""Reset Microsoft Store Cache""
""Icon""=""wsreset.exe""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\05ResetStoreCache\command]
@=""wsreset.exe""
") },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Absent },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\05ResetStoreCache]

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\ResetStoreCache]
") },
                },
            },
        },
        new()
        {
            Id = "context-menu-reliability-monitor",
            Display = new()
            {
                Name = LocKey.Setting.ContextMenuReliabilityMonitor.Name,
                Description = LocKey.Setting.ContextMenuReliabilityMonitor.Description,
                GroupName = LocKey.SettingGroup.Troubleshooting,
                Icon = MaterialIcons.ChartLine,
                AddedInVersion = "26.11.10",
                IsSubjectivePreference = true,
            },
            UiParentId = "context-menu-troubleshooting-menu",
            Targets = new Target[]
            {
                new RegTarget("MUIVerb", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\06ReliabilityMonitor" }, "MUIVerb", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Links = new[] { new Link("context-menu-classic", LinkKind.Requires, LocKey.Common.Enabled), new Link("context-menu-troubleshooting-menu", LinkKind.Requires, LocKey.Common.Enabled) },
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Of("Reliability Monitor") },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\ReliabilityMonitor]

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\06ReliabilityMonitor]
""MUIVerb""=""Reliability Monitor""
""Icon""=""perfmon.exe""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\06ReliabilityMonitor\command]
@=""perfmon.exe /rel""
") },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Absent },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\06ReliabilityMonitor]

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\ReliabilityMonitor]
") },
                },
            },
        },
        new()
        {
            Id = "context-menu-battery-report",
            Display = new()
            {
                Name = LocKey.Setting.ContextMenuBatteryReport.Name,
                Description = LocKey.Setting.ContextMenuBatteryReport.Description,
                GroupName = LocKey.SettingGroup.Troubleshooting,
                Icon = MaterialIcons.BatteryCharging,
                AddedInVersion = "26.11.10",
                IsSubjectivePreference = true,
            },
            Availability = new() { Hardware = new[] { HardwareRequirement.Battery } },
            UiParentId = "context-menu-troubleshooting-menu",
            Targets = new Target[]
            {
                new RegTarget("MUIVerb", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\07BatteryReport" }, "MUIVerb", RegistryValueKind.String),
            },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Links = new[] { new Link("context-menu-classic", LinkKind.Requires, LocKey.Common.Enabled), new Link("context-menu-troubleshooting-menu", LinkKind.Requires, LocKey.Common.Enabled) },
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Of("Battery Report") },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\BatteryReport]

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\07BatteryReport]
""MUIVerb""=""Battery Report""
""Icon""=""powercfg.cpl""

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\07BatteryReport\command]
@=""PowerShell -ExecutionPolicy Bypass -windowstyle hidden -command \""powercfg /batteryreport /output $env:TEMP\\battery-report.html; Start-Process $env:TEMP\\battery-report.html\""""
") },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Roles = new[] { StateRole.Recommended, StateRole.WindowsDefault },
                    IsFallback = true,
                    Set = new Dictionary<string, StateValue> { ["MUIVerb"] = Absent },
                    Effects = new Effect[] { new RegContentEffect(@"Windows Registry Editor Version 5.00

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\Troubleshooting\shell\07BatteryReport]

[-HKEY_LOCAL_MACHINE\SOFTWARE\Classes\Directory\Background\shell\BatteryReport]
") },
                },
            },
        },
    };
}
