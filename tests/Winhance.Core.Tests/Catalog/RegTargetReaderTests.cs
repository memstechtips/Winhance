using System;
using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class RegTargetReaderTests
{
    /// <summary>Fake context: value reads come from <paramref name="get"/>; key-existence from
    /// <paramref name="keyExists"/> (defaults to "no key exists" for the value-based tests).</summary>
    private sealed class Ctx : IDetectionContext
    {
        public WinBuild CurrentBuild => new(int.MaxValue);
        private readonly Func<string, string?, object?> _get;
        private readonly Func<string, bool> _keyExists;
        public Ctx(Func<string, string?, object?> get, Func<string, bool>? keyExists = null)
        {
            _get = get;
            _keyExists = keyExists ?? (_ => false);
        }
        public object? GetValue(string keyPath, string? valueName) => _get(keyPath, valueName);
        public bool KeyExists(string keyPath) => _keyExists(keyPath);
        public string[] GetSubKeyNames(string keyPath) => Array.Empty<string>();
        public string? PrimaryDnsV4OfActiveAdapter() => null;
        public bool IsSystemRestoreEnabled() => false;
        public bool? ScheduledTaskEnabled(string taskPath) => null;
        public int? PowerCfgValue(string subgroupGuid, string settingGuid, PowerContext context) => null;
        public string? ActivePowerPlanGuid() => null;
    }

    private static RegTarget Reg(string[] paths, string? valueName = "V") =>
        new("K", paths, valueName, RegistryValueKind.DWord);

    [Fact]
    public void Single_path_returns_the_value_present()
    {
        var t = Reg(new[] { @"HKLM\A" });
        var (val, present) = RegTargetReader.Read(t, new Ctx((p, v) => p == @"HKLM\A" ? 1 : null));
        Assert.True(present);
        Assert.Equal(1, val);
    }

    [Fact]
    public void String_flag_mask_reduces_to_the_bit_state()
    {
        var t = new RegTarget("K", new[] { @"HKCU\A" }, "Flags", RegistryValueKind.String)
        { StringFlagMask = 0x04, StringFlagAbsentBase = 62 };

        var (val, present) = RegTargetReader.Read(t, new Ctx((p, v) => "62"));
        Assert.True(present);
        Assert.Equal(true, val);      // default 62 has MKF_HOTKEYACTIVE set

        (val, present) = RegTargetReader.Read(t, new Ctx((p, v) => "130"));
        Assert.True(present);
        Assert.Equal(false, val);     // 130 lacks the bit

        (val, present) = RegTargetReader.Read(t, new Ctx((p, v) => "not-a-number"));
        Assert.False(present);        // unparseable reads as absent, never as a guess
    }

    [Fact]
    public void Absent_everywhere_is_not_present()
    {
        var t = Reg(new[] { @"HKLM\A", @"HKCU\B" });
        var (val, present) = RegTargetReader.Read(t, new Ctx((p, v) => null));
        Assert.False(present);
        Assert.Null(val);
    }

    [Fact]
    public void Mirror_takes_first_non_null_hklm_first()
    {
        // value only in the HKCU path; HKLM is ordered first but null, so HKCU wins as first non-null
        var t = Reg(new[] { @"HKCU\B", @"HKEY_LOCAL_MACHINE\A" });
        var (val, present) = RegTargetReader.Read(t, new Ctx((p, v) => p == @"HKCU\B" ? 7 : null));
        Assert.True(present);
        Assert.Equal(7, val);
    }

    [Fact]
    public void Mirror_prefers_hklm_when_both_present()
    {
        var t = Reg(new[] { @"HKEY_CURRENT_USER\B", @"HKEY_LOCAL_MACHINE\A" });
        var (val, _) = RegTargetReader.Read(t, new Ctx((p, v) => p.StartsWith("HKEY_LOCAL_MACHINE") ? 9 : 1));
        Assert.Equal(9, val); // HKLM ordered first, its non-null wins
    }

    [Fact]
    public void Bitmask_reduces_to_true_when_bit_set()
    {
        var t = new RegTarget("K", new[] { @"HKLM\A" }, "V", RegistryValueKind.Binary)
        { ByteIndex = 1, BitMask = 0x04 };
        var (val, present) = RegTargetReader.Read(t, new Ctx((p, v) => new byte[] { 0x00, 0x04 }));
        Assert.True(present);
        Assert.Equal(true, val);
    }

    [Fact]
    public void Bitmask_reduces_to_false_when_bit_clear()
    {
        var t = new RegTarget("K", new[] { @"HKLM\A" }, "V", RegistryValueKind.Binary)
        { ByteIndex = 1, BitMask = 0x04 };
        var (val, _) = RegTargetReader.Read(t, new Ctx((p, v) => new byte[] { 0x00, 0x00 }));
        Assert.Equal(false, val);
    }

    [Fact]
    public void ByteOnly_returns_the_byte_at_index()
    {
        var t = new RegTarget("K", new[] { @"HKLM\A" }, "V", RegistryValueKind.Binary)
        { ByteIndex = 2, ByteOnly = true };
        var (val, present) = RegTargetReader.Read(t, new Ctx((p, v) => new byte[] { 0, 0, 42 }));
        Assert.True(present);
        Assert.Equal((byte)42, val);
    }

    [Fact]
    public void Bitmask_blob_too_short_is_absent()
    {
        var t = new RegTarget("K", new[] { @"HKLM\A" }, "V", RegistryValueKind.Binary)
        { ByteIndex = 5, BitMask = 0x01 };
        var (val, present) = RegTargetReader.Read(t, new Ctx((p, v) => new byte[] { 0x01 }));
        Assert.False(present);
        Assert.Null(val);
    }

    [Fact]
    public void Composite_extracts_the_subkey_value()
    {
        var t = new RegTarget("K", new[] { @"HKCU\A" }, "DirectXUserGlobalSettings", RegistryValueKind.String)
        { CompositeStringKey = "SwapEffectUpgradeEnable" };
        var (val, present) = RegTargetReader.Read(t,
            new Ctx((p, v) => "VRROptimizeEnable=0;SwapEffectUpgradeEnable=1;AutoHDREnable=0"));
        Assert.True(present);
        Assert.Equal("1", val);
    }

    [Fact]
    public void Composite_subkey_is_case_insensitive()
    {
        var t = new RegTarget("K", new[] { @"HKCU\A" }, "V", RegistryValueKind.String)
        { CompositeStringKey = "swapeffectupgradeenable" };
        var (val, _) = RegTargetReader.Read(t, new Ctx((p, v) => "SwapEffectUpgradeEnable=1"));
        Assert.Equal("1", val);
    }

    [Fact]
    public void Composite_missing_subkey_is_absent()
    {
        var t = new RegTarget("K", new[] { @"HKCU\A" }, "V", RegistryValueKind.String)
        { CompositeStringKey = "NotThere" };
        var (val, present) = RegTargetReader.Read(t, new Ctx((p, v) => "SwapEffectUpgradeEnable=1"));
        Assert.False(present);
        Assert.Null(val);
    }

    [Fact]
    public void Composite_absent_value_is_absent()
    {
        var t = new RegTarget("K", new[] { @"HKCU\A" }, "V", RegistryValueKind.String)
        { CompositeStringKey = "X" };
        var (_, present) = RegTargetReader.Read(t, new Ctx((p, v) => null));
        Assert.False(present);
    }

    [Fact]
    public void KeyExistence_target_is_present_when_key_exists()
    {
        // ValueName == null -> the reading is (null, key-exists). The value reader is never consulted.
        var t = new RegTarget("KeyExists", new[] { @"HKEY_LOCAL_MACHINE\A" }, null, RegistryValueKind.None);
        var (val, present) = RegTargetReader.Read(t,
            new Ctx((p, v) => "should-be-ignored", keyExists: p => p == @"HKEY_LOCAL_MACHINE\A"));
        Assert.True(present);
        Assert.Null(val);
    }

    [Fact]
    public void KeyExistence_target_is_absent_when_no_mirror_key_exists()
    {
        var t = new RegTarget("KeyExists",
            new[] { @"HKEY_LOCAL_MACHINE\A", @"HKEY_LOCAL_MACHINE\B" }, null, RegistryValueKind.None);
        var (_, present) = RegTargetReader.Read(t, new Ctx((p, v) => null, keyExists: _ => false));
        Assert.False(present);
    }

    [Fact]
    public void KeyExistence_target_is_present_when_any_mirror_key_exists()
    {
        var t = new RegTarget("KeyExists",
            new[] { @"HKEY_LOCAL_MACHINE\A", @"HKEY_LOCAL_MACHINE\B" }, null, RegistryValueKind.None);
        var (_, present) = RegTargetReader.Read(t, new Ctx((p, v) => null, keyExists: p => p == @"HKEY_LOCAL_MACHINE\B"));
        Assert.True(present);
    }
}
