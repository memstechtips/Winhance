using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Tests.AdvancedTools;

// One synthetic Setting per write shape the autounattend must emit. Both the old script sections and the new
// ApplyOpScriptEmitter are run over this catalog and compared line-for-line (AutounattendWriterParityTests).
internal static class ParityCatalog
{
    public static readonly WinBuild Build = new(26100, 4000);

    private static Display D(string name) => new() { Name = name, Description = $"{name} description" };
    private static SettingState On(string key, object payload, params StateRole[] roles) =>
        new() { Label = "Enabled", Roles = roles, Set = new Dictionary<string, StateValue> { [key] = StateValue.Of(payload) } };
    private static SettingState Off(string key, object payload) =>
        new() { Label = "Disabled", Set = new Dictionary<string, StateValue> { [key] = StateValue.Of(payload) } };

    public static readonly IReadOnlyList<Setting> Settings = new Setting[]
    {
        new()
        {
            Id = "parity-toggle-hklm", Display = D("Toggle HKLM"),
            Targets = new Target[] { new RegTarget("V", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Parity" }, "V", RegistryValueKind.DWord) },
            States = new[] { On("V", 1), Off("V", 0) },
        },
        new()
        {
            Id = "parity-toggle-hkcu-delete", Display = D("Toggle HKCU delete"),
            Targets = new Target[] { new RegTarget("V", new[] { @"HKEY_CURRENT_USER\Software\Parity" }, "V", RegistryValueKind.String) },
            States = new[]
            {
                On("V", "yes"),
                new SettingState { Label = "Disabled", Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Absent } },
            },
        },
        new()
        {
            Id = "parity-key-exists", Display = D("Key existence"),
            Targets = new Target[] { new RegTarget("K", new[] { @"HKEY_CURRENT_USER\Software\Classes\CLSID\{PARITY}" }, null, RegistryValueKind.String) },
            States = new[]
            {
                new SettingState { Label = "Enabled", Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Exists } },
                new SettingState { Label = "Disabled", Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Absent } },
            },
        },
        new()
        {
            Id = "parity-bit", Display = D("Binary bit"),
            Targets = new Target[] { new RegTarget("B", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "UserPreferencesMask", RegistryValueKind.Binary) { ByteIndex = 4, BitMask = 0x20 } },
            States = new[] { On("B", true), Off("B", false) },
        },
        new()
        {
            Id = "parity-byte", Display = D("Binary byte"),
            Targets = new Target[] { new RegTarget("Y", new[] { @"HKEY_CURRENT_USER\Control Panel\Desktop" }, "MenuShowDelay", RegistryValueKind.Binary) { ByteIndex = 0, ByteOnly = true } },
            States = new[] { On("Y", 3), Off("Y", 0) },
        },
        new()
        {
            Id = "parity-per-subkey", Display = D("Per subkey"),
            Targets = new Target[] { new RegTarget("N", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\Parity\Interfaces" }, "N", RegistryValueKind.DWord) { PerNetworkInterface = true } },
            States = new[] { On("N", 1), new SettingState { Label = "Disabled", Set = new Dictionary<string, StateValue> { ["N"] = StateValue.Absent } } },
        },
        new()
        {
            Id = "parity-scripts", Display = D("Scripts"),
            Targets = new Target[] { new RegTarget("S", new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\ParityScripts" }, "S", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { ["S"] = StateValue.Of(1) },
                    Effects = new Effect[] { new ScriptEffect("Write-Host 'system side'", RunContext.System), new ScriptEffect("Write-Host 'user side'", RunContext.User) },
                },
                Off("S", 0),
            },
        },
        new()
        {
            Id = "parity-regcontent", Display = D("Reg content"),
            Targets = new Target[] { new RegTarget("R", new[] { @"HKEY_CURRENT_USER\Software\ParityReg" }, "R", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { ["R"] = StateValue.Of(1) },
                    Effects = new Effect[] { new RegContentEffect("Windows Registry Editor Version 5.00\r\n\r\n[HKEY_CURRENT_USER\\Software\\ParityReg]\r\n\"Imported\"=dword:00000001\r\n") },
                },
                Off("R", 0),
            },
        },
        new()
        {
            Id = "parity-task", Display = D("Scheduled task"),
            Targets = new Target[] { new TaskTarget("T", @"\Microsoft\Windows\Parity\Task") },
            States = new[]
            {
                new SettingState { Label = "Enabled", Set = new Dictionary<string, StateValue> { ["T"] = StateValue.Of(true) } },
                new SettingState { Label = "Disabled", Set = new Dictionary<string, StateValue> { ["T"] = StateValue.Of(false) } },
            },
        },
        new()
        {
            Id = "parity-selection", Display = D("Selection"),
            Targets = new Target[] { new RegTarget("M", new[] { @"HKEY_CURRENT_USER\Software\ParitySel" }, "Mode", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState { Label = "Low", Set = new Dictionary<string, StateValue> { ["M"] = StateValue.Of(0) } },
                new SettingState { Label = "Mid", Set = new Dictionary<string, StateValue> { ["M"] = StateValue.Of(1) }, Roles = new[] { StateRole.WindowsDefault } },
                new SettingState { Label = "High", Set = new Dictionary<string, StateValue> { ["M"] = StateValue.Of(2) }, Roles = new[] { StateRole.Recommended } },
            },
        },
        new()
        {
            Id = "parity-powercfg-selection", Display = D("Powercfg selection"),
            Targets = new Target[] { new PowerCfgTarget("P", "2a737441-1930-4402-8d77-b2bebba308a3", "0853a681-27c8-4100-a2fd-82013e970683", PowerModeSupport.Separate) },
            States = new[]
            {
                new SettingState { Label = "Off", Set = new Dictionary<string, StateValue> { ["P"] = StateValue.Of(0) } },
                new SettingState { Label = "On", Set = new Dictionary<string, StateValue> { ["P"] = StateValue.Of(1) } },
            },
        },
        new()
        {
            Id = "parity-slider", Display = D("Powercfg slider"),
            Targets = new Target[] { new PowerCfgTarget("Q", "7516b95f-f776-4464-8c53-06167f40cc99", "3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e", PowerModeSupport.Separate) },
            Numeric = new() { Min = 0, Max = 120, Units = "minutes" },
        },
        new()
        {
            Id = "parity-composite", Display = D("Composite string"),
            Targets = new Target[] { new RegTarget("C", new[] { @"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences" }, "DirectXUserGlobalSettings", RegistryValueKind.String) { CompositeStringKey = "SwapEffectUpgradeEnable" } },
            States = new[] { On("C", "1"), Off("C", "0") },
        },
        new()
        {
            Id = "parity-string-flag", Display = D("String flag"),
            Targets = new Target[] { new RegTarget("F", new[] { @"HKEY_CURRENT_USER\Control Panel\Accessibility\MouseKeys" }, "Flags", RegistryValueKind.String) { StringFlagMask = 0x04, StringFlagAbsentBase = 62 } },
            States = new[] { On("F", true), Off("F", false) },
        },
        new()
        {
            Id = "parity-lock", Display = D("Locked key"),
            Targets = new Target[] { new RegTarget("L", new[] { @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\ParitySvc" }, "Start", RegistryValueKind.DWord) { LockWhenValue = 4 } },
            States = new[] { On("L", 4), Off("L", 3) },
        },
        new()
        {
            Id = "parity-resetset", Display = D("Reset set"),
            Targets = new Target[] { new RegTarget("E", new[] { @"HKEY_CURRENT_USER\Software\ParityReset" }, "E", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["E"] = StateValue.Of(1).OrAbsent() },
                    ResetSet = new Dictionary<string, StateValue> { ["E"] = StateValue.Absent },
                },
                Off("E", 0),
            },
        },
        new()
        {
            Id = "parity-action", Display = D("Action"),
            Effects = new Effect[]
            {
                new RegistryWriteEffect(@"HKEY_LOCAL_MACHINE\SOFTWARE\ParityAction", "Ran", RegistryValueKind.DWord, 1),
                new ScriptEffect("Write-Host 'action script'", RunContext.System),
            },
        },
    };

    public const string FeatureId = FeatureIds.ExplorerCustomization;   // any Customize feature id; the sections key on it

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<Setting>> ByFeature =
        new Dictionary<string, IReadOnlyList<Setting>> { [FeatureId] = Settings };
}
