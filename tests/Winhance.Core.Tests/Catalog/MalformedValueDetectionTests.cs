using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Xunit;
using Winhance.TestSupport;

using Winhance.Core.Features.Common.Localization;
namespace Winhance.Core.Tests.Catalog;

// A wrongly-typed value must report Malformed - NOT Custom, and never leak as a raw value into the matchers.
// Deliberately narrow: only the surgical shapes (bitmask / single byte / decimal-string flags / packed
// composite) can be malformed; a DWord target holding REG_SZ "1" still resolves (detection is numeric-lenient),
// and flagging it would regress settings that work today.
public class MalformedValueDetectionTests
{
    private const string Path = @"HKEY_CURRENT_USER\Control Panel\Desktop";

    private sealed class Ctx : IDetectionContext
    {
        private readonly object? _value;
        public Ctx(object? value) => _value = value;
        public WinBuild CurrentBuild => new(int.MaxValue);
        public object? GetValue(string keyPath, string? valueName) => _value;
        public bool KeyExists(string keyPath) => false;
        public string[] GetSubKeyNames(string keyPath) => Array.Empty<string>();
        public IReadOnlyList<DnsServer> DnsServersOfActiveAdapter() => Array.Empty<DnsServer>();
        public bool IsSystemRestoreEnabled() => false;
        public bool? ScheduledTaskEnabled(string taskPath) => null;
        public int? PowerCfgValue(string subgroupGuid, string settingGuid, PowerContext context) => null;
        public string? ActivePowerPlanGuid() => null;
    }

    private static RegTarget BitTarget() =>
        new("UserPreferencesMask", new[] { Path }, "UserPreferencesMask", RegistryValueKind.Binary)
        { ByteIndex = 1, BitMask = 0x08 };

    // The menu-animation shape: one bitmask target, Enabled=1 / Disabled=0 with a fallback.
    private static Setting BitSetting() => new()
    {
        Id = "fade-tooltip",
        Display = new() { Name = TestKeys.Of("Fade tooltips"), Description = TestKeys.Of("d") },
        Targets = new Target[] { BitTarget() },
        States = new[]
        {
            new SettingState
            {
                Label = LocKey.Common.Enabled,
                Set = new Dictionary<string, StateValue> { ["UserPreferencesMask"] = StateValue.Of(1) },
            },
            new SettingState
            {
                Label = LocKey.Common.Disabled,
                IsFallback = true,
                Set = new Dictionary<string, StateValue> { ["UserPreferencesMask"] = StateValue.Of(0) },
            },
        },
    };

    [Fact]
    public void Read_BitmaskTargetHoldingString_IsKindMismatchAndNotPresent()
    {
        var reading = RegTargetReader.Read(BitTarget(), new Ctx("ነ考"));

        Assert.True(reading.KindMismatch);
        // Must NOT be present: a malformed value that reached the matchers could match a state by accident.
        Assert.False(reading.Present);
        Assert.Null(reading.Value);
    }

    [Fact]
    public void Read_BitmaskTargetHoldingDword_IsKindMismatch()
    {
        Assert.True(RegTargetReader.Read(BitTarget(), new Ctx(1)).KindMismatch);
    }

    [Fact]
    public void Read_BitmaskTargetHoldingBytes_ReducesNormally()
    {
        var reading = RegTargetReader.Read(BitTarget(), new Ctx(new byte[] { 0x00, 0x08 }));

        Assert.False(reading.KindMismatch);
        Assert.True(reading.Present);
        Assert.True(reading.Value is bool set && set); // bit 0x08 of byte[1] is set
    }

    [Fact]
    public void Read_ByteOnlyTargetHoldingString_IsKindMismatch()
    {
        var t = new RegTarget("Settings", new[] { Path }, "Settings", RegistryValueKind.Binary)
        { ByteIndex = 8, ByteOnly = true };
        Assert.True(RegTargetReader.Read(t, new Ctx("nonsense")).KindMismatch);
    }

    [Fact]
    public void Read_StringFlagTargetHoldingBytes_IsKindMismatch()
    {
        var t = new RegTarget("Flags", new[] { Path }, "Flags", RegistryValueKind.String) { StringFlagMask = 0x04 };
        Assert.True(RegTargetReader.Read(t, new Ctx(new byte[] { 1, 2 })).KindMismatch);
    }

    [Fact]
    public void Read_CompositeStringTargetHoldingBytes_IsKindMismatch()
    {
        var t = new RegTarget("Packed", new[] { Path }, "Packed", RegistryValueKind.String)
        { CompositeStringKey = "Sub" };
        Assert.True(RegTargetReader.Read(t, new Ctx(new byte[] { 1 })).KindMismatch);
    }

    [Fact]
    public void Read_PlainTarget_IsNeverMalformedHoweverItIsStored()
    {
        var t = new RegTarget("V", new[] { Path }, "V", RegistryValueKind.DWord);

        // A DWord target holding REG_SZ "1" reads fine and still matches Of(1) via the lenient comparer.
        var reading = RegTargetReader.Read(t, new Ctx("1"));
        Assert.False(reading.KindMismatch);
        Assert.True(reading.Present);
        Assert.True(StateValue.Of(1).Matches(reading.Value, reading.Present));
    }

    [Fact]
    public void Read_ShortBlob_IsAbsentNotMalformed()
    {
        // Right type, just too short for the byte index - genuinely different from a wrong type: there is
        // nothing to repair.
        var reading = RegTargetReader.Read(BitTarget(), new Ctx(new byte[] { 0x00 }));
        Assert.False(reading.KindMismatch);
        Assert.False(reading.Present);
    }

    [Fact]
    public void Read_UnparseableFlagsString_IsAbsentNotMalformed()
    {
        var t = new RegTarget("Flags", new[] { Path }, "Flags", RegistryValueKind.String) { StringFlagMask = 0x04 };
        var reading = RegTargetReader.Read(t, new Ctx("not-a-number"));
        Assert.False(reading.KindMismatch);
        Assert.False(reading.Present);
    }

    [Fact]
    public void Read_CompositeStringMissingSubKey_IsAbsentNotMalformed()
    {
        var t = new RegTarget("Packed", new[] { Path }, "Packed", RegistryValueKind.String)
        { CompositeStringKey = "Sub" };
        var reading = RegTargetReader.Read(t, new Ctx("Other=1;Another=2"));
        Assert.False(reading.KindMismatch);
        Assert.False(reading.Present);
    }

    [Fact]
    public void Detect_MalformedTarget_ReportsMalformed()
    {
        var detection = CatalogDiscovery.Detect(BitSetting(), new Ctx("ነ考"));

        Assert.Equal(SettingDetectionOutcome.Malformed, detection.Outcome);
        Assert.Null(detection.Label);
        Assert.NotNull(detection.Detail);
        Assert.Contains("Binary", detection.Detail!); // names what the catalog expects
    }

    [Fact]
    public void Detect_MalformedValue_WinsOverFallbackState()
    {
        // The regression this guards: a malformed value must not quietly resolve to the IsFallback state.
        // Absence falls back; an unreadable value does not - we do not know what it says.
        var detection = CatalogDiscovery.Detect(BitSetting(), new Ctx("ነ考"));
        Assert.NotEqual("Disabled", detection.Label);
        Assert.Equal(SettingDetectionOutcome.Malformed, detection.Outcome);
    }

    [Fact]
    public void Detect_ReadableValueMatchingNoState_IsCustomNotMalformed()
    {
        var setting = new Setting
        {
            Id = "plain",
            Display = new() { Name = TestKeys.Of("p"), Description = TestKeys.Of("d") },
            Targets = new Target[] { new RegTarget("V", new[] { Path }, "V", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState { Label = LocKey.Common.Enabled, Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(1) } },
                new SettingState { Label = LocKey.Common.Disabled, Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(0) } },
            },
        };

        var detection = CatalogDiscovery.Detect(setting, new Ctx(7));
        Assert.Equal(SettingDetectionOutcome.Custom, detection.Outcome);
    }

    [Fact]
    public void Detect_ResolvingSetting_ReportsResolved()
    {
        var detection = CatalogDiscovery.Detect(BitSetting(), new Ctx(new byte[] { 0x00, 0x08 }));
        Assert.Equal(SettingDetectionOutcome.Resolved, detection.Outcome);
        Assert.Equal(LocKey.Common.Enabled.Value, detection.Label);
    }

    [Fact]
    public void Detect_MalformedApplyOnlyTarget_DoesNotTaintSetting()
    {
        // ApplyOnly targets are written but never read, so their stored type cannot affect what the user
        // sees. Only the read-authoritative target decides.
        var setting = new Setting
        {
            Id = "mirrored",
            Display = new() { Name = TestKeys.Of("m"), Description = TestKeys.Of("d") },
            Targets = new Target[]
            {
                new RegTarget("V", new[] { Path }, "V", RegistryValueKind.DWord),
                new RegTarget("Mirror", new[] { Path }, "Mirror", RegistryValueKind.Binary)
                { ByteIndex = 0, BitMask = 0x01, ApplyOnly = true },
            },
            States = new[]
            {
                new SettingState { Label = LocKey.Common.Enabled, Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(1) } },
                new SettingState { Label = LocKey.Common.Disabled, Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(0) } },
            },
        };

        // The single Ctx value ("1") is served for BOTH targets: it resolves the plain one and would be a
        // kind mismatch on the ApplyOnly mirror.
        var detection = CatalogDiscovery.Detect(setting, new Ctx(1));
        Assert.Equal(SettingDetectionOutcome.Resolved, detection.Outcome);
        Assert.Equal(LocKey.Common.Enabled.Value, detection.Label);
    }
}
