using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class RegTargetReaderTests
{
    private static readonly string[] HklmA = [@"HKLM\A"];
    private static readonly string[] HkcuA = [@"HKCU\A"];
    private static readonly string[] HklmAThenHkcuB = [@"HKLM\A", @"HKCU\B"];
    private static readonly string[] HkcuBThenLongHklmA = [@"HKCU\B", @"HKEY_LOCAL_MACHINE\A"];
    private static readonly string[] LongHkcuBThenLongHklmA = [@"HKEY_CURRENT_USER\B", @"HKEY_LOCAL_MACHINE\A"];
    private static readonly string[] LongHklmA = [@"HKEY_LOCAL_MACHINE\A"];
    private static readonly string[] LongHklmAAndB = [@"HKEY_LOCAL_MACHINE\A", @"HKEY_LOCAL_MACHINE\B"];

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
        public IReadOnlyList<DnsServer> DnsServersOfActiveAdapter() => Array.Empty<DnsServer>();
        public bool IsSystemRestoreEnabled() => false;
        public bool? ScheduledTaskEnabled(string taskPath) => null;
        public int? PowerCfgValue(string subgroupGuid, string settingGuid, PowerContext context) => null;
        public string? ActivePowerPlanGuid() => null;
    }

    private static RegTarget Reg(string[] paths, string? valueName = "V") =>
        new("K", paths, valueName, RegistryValueKind.DWord);

    [Fact]
    public void Read_SinglePath_ReturnsTheValueAsPresent()
    {
        var t = Reg(HklmA);
        var (val, present, _) = RegTargetReader.Read(t, new Ctx((p, v) => p == @"HKLM\A" ? 1 : null));
        Assert.True(present);
        Assert.Equal(1, val);
    }

    [Fact]
    public void Read_StringFlagMask_ReducesToTheBitState()
    {
        var t = new RegTarget("K", HkcuA, "Flags", RegistryValueKind.String)
        { StringFlagMask = 0x04, StringFlagAbsentBase = 62 };

        var (val, present, _) = RegTargetReader.Read(t, new Ctx((p, v) => "62"));
        Assert.True(present);
        Assert.Equal(true, val);      // default 62 has MKF_HOTKEYACTIVE set

        (val, present, _) = RegTargetReader.Read(t, new Ctx((p, v) => "130"));
        Assert.True(present);
        Assert.Equal(false, val);     // 130 lacks the bit

        (val, present, _) = RegTargetReader.Read(t, new Ctx((p, v) => "not-a-number"));
        Assert.False(present);        // unparseable reads as absent, never as a guess
    }

    [Theory]
    [InlineData("hide:home", true)]
    [InlineData("hide:printers; HOME", true)]
    [InlineData("hide:printers", false)]
    [InlineData("hide:homegroup", false)]
    [InlineData("showonly:home", false)]
    [InlineData("showonly:display;HOME", false)]
    [InlineData("showonly:display;sound", true)]
    [InlineData("showonly:", true)]
    [InlineData("showonly: ; ", true)]
    [InlineData("something else", false)]
    public void List_item_reads_whether_the_item_is_in_the_prefixed_list(string value, bool expected)
    {
        var t = new RegTarget("K", HklmA, "SettingsPageVisibility", RegistryValueKind.String) { ListItem = "home", ListPrefix = "hide:" };

        var (val, present, _) = RegTargetReader.Read(t, new Ctx((p, v) => value));

        Assert.True(present);
        Assert.Equal(expected, val);
    }

    [Fact]
    public void Read_AbsentEverywhere_IsNotPresent()
    {
        var t = Reg(HklmAThenHkcuB);
        var (val, present, _) = RegTargetReader.Read(t, new Ctx((p, v) => null));
        Assert.False(present);
        Assert.Null(val);
    }

    [Fact]
    public void Read_MirrorWithHklmFirst_TakesTheFirstNonNull()
    {
        // value only in the HKCU path; HKLM is ordered first but null, so HKCU wins as first non-null
        var t = Reg(HkcuBThenLongHklmA);
        var (val, present, _) = RegTargetReader.Read(t, new Ctx((p, v) => p == @"HKCU\B" ? 7 : null));
        Assert.True(present);
        Assert.Equal(7, val);
    }

    [Fact]
    public void Read_MirrorWithBothPresent_PrefersHklm()
    {
        var t = Reg(LongHkcuBThenLongHklmA);
        var (val, _, _) = RegTargetReader.Read(t, new Ctx((p, v) => p.StartsWith("HKEY_LOCAL_MACHINE") ? 9 : 1));
        Assert.Equal(9, val); // HKLM ordered first, its non-null wins
    }

    [Fact]
    public void Read_BitmaskWithBitSet_ReducesToTrue()
    {
        var t = new RegTarget("K", HklmA, "V", RegistryValueKind.Binary)
        { ByteIndex = 1, BitMask = 0x04 };
        var (val, present, _) = RegTargetReader.Read(t, new Ctx((p, v) => new byte[] { 0x00, 0x04 }));
        Assert.True(present);
        Assert.Equal(true, val);
    }

    [Fact]
    public void Read_BitmaskWithBitClear_ReducesToFalse()
    {
        var t = new RegTarget("K", HklmA, "V", RegistryValueKind.Binary)
        { ByteIndex = 1, BitMask = 0x04 };
        var (val, _, _) = RegTargetReader.Read(t, new Ctx((p, v) => new byte[] { 0x00, 0x00 }));
        Assert.Equal(false, val);
    }

    [Fact]
    public void Read_ByteOnly_ReturnsTheByteAtIndex()
    {
        var t = new RegTarget("K", HklmA, "V", RegistryValueKind.Binary)
        { ByteIndex = 2, ByteOnly = true };
        var (val, present, _) = RegTargetReader.Read(t, new Ctx((p, v) => new byte[] { 0, 0, 42 }));
        Assert.True(present);
        Assert.Equal((byte)42, val);
    }

    [Fact]
    public void Read_BitmaskBlobTooShort_IsAbsent()
    {
        var t = new RegTarget("K", HklmA, "V", RegistryValueKind.Binary)
        { ByteIndex = 5, BitMask = 0x01 };
        var (val, present, _) = RegTargetReader.Read(t, new Ctx((p, v) => new byte[] { 0x01 }));
        Assert.False(present);
        Assert.Null(val);
    }

    [Fact]
    public void Read_Composite_ExtractsTheSubkeyValue()
    {
        var t = new RegTarget("K", HkcuA, "DirectXUserGlobalSettings", RegistryValueKind.String)
        { CompositeStringKey = "SwapEffectUpgradeEnable" };
        var (val, present, _) = RegTargetReader.Read(t,
            new Ctx((p, v) => "VRROptimizeEnable=0;SwapEffectUpgradeEnable=1;AutoHDREnable=0"));
        Assert.True(present);
        Assert.Equal("1", val);
    }

    [Fact]
    public void Read_CompositeSubkeyInOtherCase_StillMatches()
    {
        var t = new RegTarget("K", HkcuA, "V", RegistryValueKind.String)
        { CompositeStringKey = "swapeffectupgradeenable" };
        var (val, _, _) = RegTargetReader.Read(t, new Ctx((p, v) => "SwapEffectUpgradeEnable=1"));
        Assert.Equal("1", val);
    }

    [Fact]
    public void Read_CompositeMissingSubkey_IsAbsent()
    {
        var t = new RegTarget("K", HkcuA, "V", RegistryValueKind.String)
        { CompositeStringKey = "NotThere" };
        var (val, present, _) = RegTargetReader.Read(t, new Ctx((p, v) => "SwapEffectUpgradeEnable=1"));
        Assert.False(present);
        Assert.Null(val);
    }

    [Fact]
    public void Read_CompositeAbsentValue_IsAbsent()
    {
        var t = new RegTarget("K", HkcuA, "V", RegistryValueKind.String)
        { CompositeStringKey = "X" };
        var (_, present, _) = RegTargetReader.Read(t, new Ctx((p, v) => null));
        Assert.False(present);
    }

    [Fact]
    public void Read_KeyExistenceTargetWithKeyPresent_IsPresent()
    {
        // ValueName == null -> the reading is (null, key-exists). The value reader is never consulted.
        var t = new RegTarget("KeyExists", LongHklmA, null, RegistryValueKind.None);
        var (val, present, _) = RegTargetReader.Read(t,
            new Ctx((p, v) => "should-be-ignored", keyExists: p => p == @"HKEY_LOCAL_MACHINE\A"));
        Assert.True(present);
        Assert.Null(val);
    }

    [Fact]
    public void Read_KeyExistenceTargetWithNoMirrorKey_IsAbsent()
    {
        var t = new RegTarget("KeyExists",
            LongHklmAAndB, null, RegistryValueKind.None);
        var (_, present, _) = RegTargetReader.Read(t, new Ctx((p, v) => null, keyExists: _ => false));
        Assert.False(present);
    }

    [Fact]
    public void Read_KeyExistenceTargetWithAnyMirrorKey_IsPresent()
    {
        var t = new RegTarget("KeyExists",
            LongHklmAAndB, null, RegistryValueKind.None);
        var (_, present, _) = RegTargetReader.Read(t, new Ctx((p, v) => null, keyExists: p => p == @"HKEY_LOCAL_MACHINE\B"));
        Assert.True(present);
    }
}
