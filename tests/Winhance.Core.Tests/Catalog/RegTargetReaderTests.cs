using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class RegTargetReaderTests
{
    private static RegTarget Reg(string[] paths, string? valueName = "V") =>
        new("K", paths, valueName, RegistryValueKind.DWord);

    [Fact]
    public void Single_path_returns_the_value_present()
    {
        var t = Reg(new[] { @"HKLM\A" });
        var (val, present) = RegTargetReader.Read(t, (p, v) => p == @"HKLM\A" ? 1 : null);
        Assert.True(present);
        Assert.Equal(1, val);
    }

    [Fact]
    public void Absent_everywhere_is_not_present()
    {
        var t = Reg(new[] { @"HKLM\A", @"HKCU\B" });
        var (val, present) = RegTargetReader.Read(t, (p, v) => null);
        Assert.False(present);
        Assert.Null(val);
    }

    [Fact]
    public void Mirror_takes_first_non_null_hklm_first()
    {
        // value only in the HKCU path; HKLM is ordered first but null, so HKCU wins as first non-null
        var t = Reg(new[] { @"HKCU\B", @"HKEY_LOCAL_MACHINE\A" });
        var (val, present) = RegTargetReader.Read(t, (p, v) => p == @"HKCU\B" ? 7 : null);
        Assert.True(present);
        Assert.Equal(7, val);
    }

    [Fact]
    public void Mirror_prefers_hklm_when_both_present()
    {
        var t = Reg(new[] { @"HKEY_CURRENT_USER\B", @"HKEY_LOCAL_MACHINE\A" });
        var (val, _) = RegTargetReader.Read(t, (p, v) => p.StartsWith("HKEY_LOCAL_MACHINE") ? 9 : 1);
        Assert.Equal(9, val); // HKLM ordered first, its non-null wins
    }

    [Fact]
    public void Bitmask_reduces_to_true_when_bit_set()
    {
        var t = new RegTarget("K", new[] { @"HKLM\A" }, "V", RegistryValueKind.Binary)
        { ByteIndex = 1, BitMask = 0x04 };
        var (val, present) = RegTargetReader.Read(t, (p, v) => new byte[] { 0x00, 0x04 });
        Assert.True(present);
        Assert.Equal(true, val);
    }

    [Fact]
    public void Bitmask_reduces_to_false_when_bit_clear()
    {
        var t = new RegTarget("K", new[] { @"HKLM\A" }, "V", RegistryValueKind.Binary)
        { ByteIndex = 1, BitMask = 0x04 };
        var (val, _) = RegTargetReader.Read(t, (p, v) => new byte[] { 0x00, 0x00 });
        Assert.Equal(false, val);
    }

    [Fact]
    public void ByteOnly_returns_the_byte_at_index()
    {
        var t = new RegTarget("K", new[] { @"HKLM\A" }, "V", RegistryValueKind.Binary)
        { ByteIndex = 2, ByteOnly = true };
        var (val, present) = RegTargetReader.Read(t, (p, v) => new byte[] { 0, 0, 42 });
        Assert.True(present);
        Assert.Equal((byte)42, val);
    }

    [Fact]
    public void Bitmask_blob_too_short_is_absent()
    {
        var t = new RegTarget("K", new[] { @"HKLM\A" }, "V", RegistryValueKind.Binary)
        { ByteIndex = 5, BitMask = 0x01 };
        var (val, present) = RegTargetReader.Read(t, (p, v) => new byte[] { 0x01 });
        Assert.False(present);
        Assert.Null(val);
    }

    [Fact]
    public void Composite_extracts_the_subkey_value()
    {
        var t = new RegTarget("K", new[] { @"HKCU\A" }, "DirectXUserGlobalSettings", RegistryValueKind.String)
        { CompositeStringKey = "SwapEffectUpgradeEnable" };
        var (val, present) = RegTargetReader.Read(t,
            (p, v) => "VRROptimizeEnable=0;SwapEffectUpgradeEnable=1;AutoHDREnable=0");
        Assert.True(present);
        Assert.Equal("1", val);
    }

    [Fact]
    public void Composite_subkey_is_case_insensitive()
    {
        var t = new RegTarget("K", new[] { @"HKCU\A" }, "V", RegistryValueKind.String)
        { CompositeStringKey = "swapeffectupgradeenable" };
        var (val, _) = RegTargetReader.Read(t, (p, v) => "SwapEffectUpgradeEnable=1");
        Assert.Equal("1", val);
    }

    [Fact]
    public void Composite_missing_subkey_is_absent()
    {
        var t = new RegTarget("K", new[] { @"HKCU\A" }, "V", RegistryValueKind.String)
        { CompositeStringKey = "NotThere" };
        var (val, present) = RegTargetReader.Read(t, (p, v) => "SwapEffectUpgradeEnable=1");
        Assert.False(present);
        Assert.Null(val);
    }

    [Fact]
    public void Composite_absent_value_is_absent()
    {
        var t = new RegTarget("K", new[] { @"HKCU\A" }, "V", RegistryValueKind.String)
        { CompositeStringKey = "X" };
        var (_, present) = RegTargetReader.Read(t, (p, v) => null);
        Assert.False(present);
    }
}
