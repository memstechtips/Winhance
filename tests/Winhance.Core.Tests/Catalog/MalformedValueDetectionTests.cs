using System;
using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

/// <summary>
/// A value stored under a type its target cannot reduce must report Malformed - NOT Custom, and never as a
/// raw value leaked into the matchers. Before this, the type guards in <see cref="RegTargetReader"/> fell
/// through to "return the raw value, marked present", so the value matched nothing and the setting surfaced
/// as "Custom", telling the user their VALUE was unrecognized when in fact its FORMAT was wrong.
///
/// Deliberately narrow: only the surgical shapes (bitmask / single byte / decimal-string flags / packed
/// composite) can be malformed, because only they need a specific CLR type to reduce at all. A plain value
/// is never malformed - detection is numeric-lenient, so a DWord target holding REG_SZ "1" still resolves
/// correctly, and flagging it would regress settings that work today.
/// </summary>
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
        public string? PrimaryDnsV4OfActiveAdapter() => null;
        public bool IsSystemRestoreEnabled() => false;
        public bool? ScheduledTaskEnabled(string taskPath) => null;
        public int? PowerCfgValue(string subgroupGuid, string settingGuid, PowerContext context) => null;
        public string? ActivePowerPlanGuid() => null;
    }

    private static RegTarget BitTarget() =>
        new("UserPreferencesMask", new[] { Path }, "UserPreferencesMask", RegistryValueKind.Binary)
        { ByteIndex = 1, BitMask = 0x08 };

    /// <summary>The menu-animation shape: one bitmask target, Enabled=1 / Disabled=0 with a fallback.</summary>
    private static Setting BitSetting() => new()
    {
        Id = "fade-tooltip",
        Display = new() { Name = "Fade tooltips", Description = "d" },
        Targets = new Target[] { BitTarget() },
        States = new[]
        {
            new SettingState
            {
                Label = "Enabled",
                Set = new Dictionary<string, StateValue> { ["UserPreferencesMask"] = StateValue.Of(1) },
            },
            new SettingState
            {
                Label = "Disabled",
                IsFallback = true,
                Set = new Dictionary<string, StateValue> { ["UserPreferencesMask"] = StateValue.Of(0) },
            },
        },
    };

    // ----------------------------------------------------------------------------------- reader level

    [Fact]
    public void Bitmask_target_holding_a_string_is_a_kind_mismatch_and_not_present()
    {
        var reading = RegTargetReader.Read(BitTarget(), new Ctx("ነ考"));

        Assert.True(reading.KindMismatch);
        // Must NOT be present: a malformed value that reached the matchers could match a state by accident.
        Assert.False(reading.Present);
        Assert.Null(reading.Value);
    }

    [Fact]
    public void Bitmask_target_holding_a_dword_is_a_kind_mismatch()
    {
        Assert.True(RegTargetReader.Read(BitTarget(), new Ctx(1)).KindMismatch);
    }

    [Fact]
    public void Bitmask_target_holding_bytes_reduces_normally()
    {
        var reading = RegTargetReader.Read(BitTarget(), new Ctx(new byte[] { 0x00, 0x08 }));

        Assert.False(reading.KindMismatch);
        Assert.True(reading.Present);
        Assert.True(reading.Value is bool set && set); // bit 0x08 of byte[1] is set
    }

    [Fact]
    public void Byte_only_target_holding_a_string_is_a_kind_mismatch()
    {
        var t = new RegTarget("Settings", new[] { Path }, "Settings", RegistryValueKind.Binary)
        { ByteIndex = 8, ByteOnly = true };
        Assert.True(RegTargetReader.Read(t, new Ctx("nonsense")).KindMismatch);
    }

    [Fact]
    public void String_flag_target_holding_bytes_is_a_kind_mismatch()
    {
        var t = new RegTarget("Flags", new[] { Path }, "Flags", RegistryValueKind.String) { StringFlagMask = 0x04 };
        Assert.True(RegTargetReader.Read(t, new Ctx(new byte[] { 1, 2 })).KindMismatch);
    }

    [Fact]
    public void Composite_string_target_holding_bytes_is_a_kind_mismatch()
    {
        var t = new RegTarget("Packed", new[] { Path }, "Packed", RegistryValueKind.String)
        { CompositeStringKey = "Sub" };
        Assert.True(RegTargetReader.Read(t, new Ctx(new byte[] { 1 })).KindMismatch);
    }

    [Fact]
    public void A_plain_target_is_never_malformed_however_it_is_stored()
    {
        var t = new RegTarget("V", new[] { Path }, "V", RegistryValueKind.DWord);

        // A DWord target holding REG_SZ "1" reads fine and still matches Of(1) via the lenient comparer.
        var reading = RegTargetReader.Read(t, new Ctx("1"));
        Assert.False(reading.KindMismatch);
        Assert.True(reading.Present);
        Assert.True(StateValue.Of(1).Matches(reading.Value, reading.Present));
    }

    // ------------------------------------------------------------------------------- reader edge cases

    [Fact]
    public void A_short_blob_is_absent_not_malformed()
    {
        // Right type, just too short for the byte index: unchanged pre-existing behaviour, and genuinely
        // different from a wrong type - there is nothing to repair.
        var reading = RegTargetReader.Read(BitTarget(), new Ctx(new byte[] { 0x00 }));
        Assert.False(reading.KindMismatch);
        Assert.False(reading.Present);
    }

    [Fact]
    public void An_unparseable_flags_string_is_absent_not_malformed()
    {
        var t = new RegTarget("Flags", new[] { Path }, "Flags", RegistryValueKind.String) { StringFlagMask = 0x04 };
        var reading = RegTargetReader.Read(t, new Ctx("not-a-number"));
        Assert.False(reading.KindMismatch);
        Assert.False(reading.Present);
    }

    [Fact]
    public void A_composite_string_missing_its_subkey_is_absent_not_malformed()
    {
        var t = new RegTarget("Packed", new[] { Path }, "Packed", RegistryValueKind.String)
        { CompositeStringKey = "Sub" };
        var reading = RegTargetReader.Read(t, new Ctx("Other=1;Another=2"));
        Assert.False(reading.KindMismatch);
        Assert.False(reading.Present);
    }

    // -------------------------------------------------------------------------------- discovery level

    [Fact]
    public void A_malformed_target_makes_the_setting_report_Malformed()
    {
        var detection = CatalogDiscovery.Detect(BitSetting(), new Ctx("ነ考"));

        Assert.Equal(SettingDetectionOutcome.Malformed, detection.Outcome);
        Assert.Null(detection.Label);
        Assert.NotNull(detection.Detail);
        Assert.Contains("Binary", detection.Detail!); // names what the catalog expects
    }

    [Fact]
    public void Malformed_wins_over_the_fallback_state()
    {
        // The regression this guards: a malformed value must not quietly resolve to the IsFallback state.
        // Absence falls back; an unreadable value does not - we do not know what it says.
        var detection = CatalogDiscovery.Detect(BitSetting(), new Ctx("ነ考"));
        Assert.NotEqual("Disabled", detection.Label);
        Assert.Equal(SettingDetectionOutcome.Malformed, detection.Outcome);
    }

    [Fact]
    public void A_readable_value_matching_no_state_is_still_Custom_not_Malformed()
    {
        var setting = new Setting
        {
            Id = "plain",
            Display = new() { Name = "p", Description = "d" },
            Targets = new Target[] { new RegTarget("V", new[] { Path }, "V", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState { Label = "Enabled", Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(1) } },
                new SettingState { Label = "Disabled", Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(0) } },
            },
        };

        var detection = CatalogDiscovery.Detect(setting, new Ctx(7));
        Assert.Equal(SettingDetectionOutcome.Custom, detection.Outcome);
    }

    [Fact]
    public void A_resolving_setting_reports_Resolved()
    {
        var detection = CatalogDiscovery.Detect(BitSetting(), new Ctx(new byte[] { 0x00, 0x08 }));
        Assert.Equal(SettingDetectionOutcome.Resolved, detection.Outcome);
        Assert.Equal("Enabled", detection.Label);
    }

    [Fact]
    public void An_apply_only_target_being_malformed_does_not_taint_the_setting()
    {
        // ApplyOnly targets are written but never read, so their stored type cannot affect what the user
        // sees. Only the read-authoritative target decides.
        var setting = new Setting
        {
            Id = "mirrored",
            Display = new() { Name = "m", Description = "d" },
            Targets = new Target[]
            {
                new RegTarget("V", new[] { Path }, "V", RegistryValueKind.DWord),
                new RegTarget("Mirror", new[] { Path }, "Mirror", RegistryValueKind.Binary)
                { ByteIndex = 0, BitMask = 0x01, ApplyOnly = true },
            },
            States = new[]
            {
                new SettingState { Label = "Enabled", Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(1) } },
                new SettingState { Label = "Disabled", Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(0) } },
            },
        };

        // The single Ctx value ("1") is served for BOTH targets: it resolves the plain one and would be a
        // kind mismatch on the ApplyOnly mirror.
        var detection = CatalogDiscovery.Detect(setting, new Ctx(1));
        Assert.Equal(SettingDetectionOutcome.Resolved, detection.Outcome);
        Assert.Equal("Enabled", detection.Label);
    }
}
