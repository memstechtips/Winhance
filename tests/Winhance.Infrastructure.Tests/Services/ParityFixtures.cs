using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Tests.Services;

// Minimal synthetic Settings, one per control kind, for the selection-model tests.
internal static class ParityFixtures
{
    private static readonly string[] HkcuT = [@"HKEY_CURRENT_USER\Software\T"];
    private static readonly string[] HkcuS = [@"HKEY_CURRENT_USER\Software\S"];

    public static Setting Toggle(string id) => new()
    {
        Id = id, Display = new() { Name = id, Description = id },
        Targets = new Target[] { new RegTarget("V", HkcuT, "V", RegistryValueKind.DWord) },
        States = new[]
        {
            new SettingState { Label = "Enabled", Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(1) } },
            new SettingState { Label = "Disabled", Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(0) } },
        },
    };

    public static Setting Selection(string id) => new()
    {
        Id = id, Display = new() { Name = id, Description = id },
        Targets = new Target[] { new RegTarget("M", HkcuS, "Mode", RegistryValueKind.DWord) },
        States = new[]
        {
            new SettingState { Label = "A", Set = new Dictionary<string, StateValue> { ["M"] = StateValue.Of(0) } },
            new SettingState { Label = "B", Set = new Dictionary<string, StateValue> { ["M"] = StateValue.Of(1) } },
        },
    };

    // Payloads deliberately unequal to their option indices, as PowerOptions.TimeIntervals is: value-to-index
    // and index-to-value are the translations these tests exist to protect, and 0/1 passes a pass-through.
    public static Setting PowerCfgSelection(string id) => new()
    {
        Id = id, Display = new() { Name = id, Description = id },
        Targets = new Target[] { new PowerCfgTarget("P", "sub", "set", PowerModeSupport.Separate) },
        States = new[]
        {
            new SettingState { Label = "5 minutes", Set = new Dictionary<string, StateValue> { ["P"] = StateValue.Of(300) } },
            new SettingState { Label = "15 minutes", Set = new Dictionary<string, StateValue> { ["P"] = StateValue.Of(900) } },
        },
    };

    public static Setting Slider(string id, PowerModeSupport mode) => new()
    {
        Id = id, Display = new() { Name = id, Description = id },
        Targets = new Target[] { new PowerCfgTarget("Q", "sub", "set", mode) },
        Numeric = new() { Min = 0, Max = 100, Units = "minutes" },
    };

    public static Setting PowerPlanSetting() => new()
    {
        Id = SettingIds.PowerPlanSelection, Display = new() { Name = "plan", Description = "plan" },
        OptionSource = new StubOptionSource(),
    };

    private sealed class StubOptionSource : IDynamicOptionSource
    {
        public IReadOnlyList<DynamicOption> EnumerateOptions(IDetectionContext context) => Array.Empty<DynamicOption>();
        public string? CurrentSelection(IDetectionContext context) => null;
    }
}
