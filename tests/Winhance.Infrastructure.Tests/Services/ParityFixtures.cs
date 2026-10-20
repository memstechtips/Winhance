using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Models;
using Winhance.TestSupport;

using Winhance.Core.Features.Common.Localization;
namespace Winhance.Infrastructure.Tests.Services;

// Minimal synthetic Settings, one per control kind, for the selection-model tests.
internal static class ParityFixtures
{
    private static readonly string[] HkcuT = [@"HKEY_CURRENT_USER\Software\T"];
    private static readonly string[] HkcuS = [@"HKEY_CURRENT_USER\Software\S"];

    public static Setting Toggle(string id) => new()
    {
        Id = id, Display = new() { Name = TestKeys.Of(id), Description = TestKeys.Of(id)},
        Targets = new Target[] { new RegTarget("V", HkcuT, "V", RegistryValueKind.DWord) },
        States = new[]
        {
            new SettingState { Label = LocKey.Common.Enabled, Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(1) } },
            new SettingState { Label = LocKey.Common.Disabled, Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(0) } },
        },
    };

    public static Setting CheckBox(string id) => new()
    {
        Id = id, Display = new() { Name = TestKeys.Of(id), Description = TestKeys.Of(id)},
        Targets = new Target[] { new RegTarget("V", HkcuT, "V", RegistryValueKind.DWord) },
        States = new[]
        {
            new SettingState { Label = LocKey.Common.Checked, Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(1) } },
            new SettingState { Label = LocKey.Common.Unchecked, Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(0) } },
        },
    };

    public static Setting Selection(string id) => new()
    {
        Id = id, Display = new() { Name = TestKeys.Of(id), Description = TestKeys.Of(id)},
        Targets = new Target[] { new RegTarget("M", HkcuS, "Mode", RegistryValueKind.DWord) },
        States = new[]
        {
            new SettingState { Label = TestKeys.Of("A"), Set = new Dictionary<string, StateValue> { ["M"] = StateValue.Of(0) } },
            new SettingState { Label = TestKeys.Of("B"), Set = new Dictionary<string, StateValue> { ["M"] = StateValue.Of(1) } },
        },
    };

    // Payloads deliberately unequal to their option indices, as PowerOptions.TimeIntervals is: value-to-index
    // and index-to-value are the translations these tests exist to protect, and 0/1 passes a pass-through.
    public static Setting PowerCfgSelection(string id) => new()
    {
        Id = id, Display = new() { Name = TestKeys.Of(id), Description = TestKeys.Of(id)},
        Targets = new Target[] { new PowerCfgTarget("P", "sub", "set", PowerModeSupport.Separate) },
        States = new[]
        {
            new SettingState { Label = TestKeys.Of("5 minutes"), Set = new Dictionary<string, StateValue> { ["P"] = StateValue.Of(300) } },
            new SettingState { Label = TestKeys.Of("15 minutes"), Set = new Dictionary<string, StateValue> { ["P"] = StateValue.Of(900) } },
        },
    };

    public static Setting Slider(string id, PowerModeSupport mode) => new()
    {
        Id = id, Display = new() { Name = TestKeys.Of(id), Description = TestKeys.Of(id)},
        Targets = new Target[] { new PowerCfgTarget("Q", "sub", "set", mode) },
        Numeric = new() { Min = 0, Max = 100, Units = "minutes" },
    };

    public static Setting TextSetting(string id) => new()
    {
        Id = id, Display = new() { Name = TestKeys.Of(id), Description = TestKeys.Of(id)},
        Targets = new Target[] { new AutounattendElement("K", "specialize", "Microsoft-Windows-Shell-Setup", "ComputerName") },
        TextBox = new(new TextRule("^.+$", UpperCase: false, TestKeys.Of("Anything."))),
    };

    public static Setting ListSetting(string id) => new()
    {
        Id = id, Display = new() { Name = TestKeys.Of(id), Description = TestKeys.Of(id)},
        Targets = new Target[] { new AutounattendElement("K", "oobeSystem", "Microsoft-Windows-Shell-Setup", "UserAccounts/LocalAccounts") },
        List = new(
        [
            new Field("name", FieldKind.Text, TestKeys.Of("name"),
                new TextRule("^.+$", UpperCase: false, TestKeys.Of("Anything."))),
            new Field("display-name", FieldKind.Text, TestKeys.Of("display-name")),
            new Field("group", FieldKind.Selection, TestKeys.Of("group"),
                Options: [TestKeys.Of("Administrators"), TestKeys.Of("Users")], Default: "0"),
            new Field("password", FieldKind.Password, TestKeys.Of("password")),
            new Field("obscure", FieldKind.CheckBox, TestKeys.Of("obscure"), Default: "true"),
            new Field("auto-logon", FieldKind.CheckBox, TestKeys.Of("auto-logon"), OneRowOnly: true),
        ]),
    };

    public static Setting PowerPlanSetting() => new()
    {
        Id = "power-plan-selection", Display = new() { Name = TestKeys.Of("plan"), Description = TestKeys.Of("plan") },
        Options = new(OptionSource.PowerPlans),
    };
}
