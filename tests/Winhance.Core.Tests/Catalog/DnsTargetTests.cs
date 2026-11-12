using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Localization;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class DnsTargetTests
{
    private sealed class FakeCtx(params DnsServer[] servers) : IDetectionContext
    {
        public WinBuild CurrentBuild => new(int.MaxValue);
        public object? GetValue(string keyPath, string? valueName) => null;
        public string[] GetSubKeyNames(string keyPath) => System.Array.Empty<string>();
        public bool KeyExists(string keyPath) => false;
        public bool? ScheduledTaskEnabled(string taskPath) => null;
        public IReadOnlyList<DnsServer> DnsServersOfActiveAdapter() => servers;
        public bool IsSystemRestoreEnabled() => false;
        public int? PowerCfgValue(string subgroupGuid, string settingGuid, PowerContext context) => null;
        public string? ActivePowerPlanGuid() => null;
    }

    private static Setting Dns => SettingCatalog.Find("gaming-dns-server")!;

    private static readonly string[] Quad9Servers = ["9.9.9.9", "149.112.112.112"];

    private static DnsTarget Target(string key) => Dns.Targets.OfType<DnsTarget>().Single(t => t.Key == key);

    [Fact]
    public void Detect_NoServers_ReadsAsAutomatic()
        => Assert.Equal(LocKey.Setting.GamingDnsServer.Option0.Value, CatalogDiscovery.Detect(Dns, new FakeCtx()).Label);

    [Fact]
    public void Detect_PresetPair_ReadsAsItsPreset()
        => Assert.Equal(LocKey.Setting.GamingDnsServer.Option4.Value,
            CatalogDiscovery.Detect(Dns, new FakeCtx(new DnsServer("8.8.8.8"), new DnsServer("8.8.4.4"))).Label);

    // A preset names only its servers, so encrypting them keeps the preset.
    [Fact]
    public void Detect_EncryptedPreset_StillReadsAsItsPreset()
        => Assert.Equal(LocKey.Setting.GamingDnsServer.Option1.Value, CatalogDiscovery.Detect(Dns, new FakeCtx(
            new DnsServer("1.1.1.1", DohMode.Automatic), new DnsServer("1.0.0.1", DohMode.Automatic))).Label);

    // The preset is the pair, so a lone preferred server or a typed pair is Custom.
    [Theory]
    [InlineData("1.1.1.1", null)]
    [InlineData("10.0.0.1", "10.0.0.2")]
    public void Detect_ServersNoPresetNames_ReadsAsCustom(string preferred, string? alternate)
        => Assert.Null(CatalogDiscovery.Detect(Dns, alternate is null
            ? new FakeCtx(new DnsServer(preferred))
            : new FakeCtx(new DnsServer(preferred), new DnsServer(alternate))).Label);

    [Fact]
    public void ReadDns_EachBox_ReadsItsOwnSlotAndFamily()
    {
        var ctx = new FakeCtx(
            new DnsServer("10.0.0.1"),
            new DnsServer("2001:db8::1", DohMode.Manual, "https://dns.example/q", Fallback: true));

        Assert.Equal("10.0.0.1", CatalogDiscovery.ReadDns(Target("primary"), ctx).Value);
        Assert.Null(CatalogDiscovery.ReadDns(Target("secondary"), ctx).Value);
        Assert.Equal("2001:db8::1", CatalogDiscovery.ReadDns(Target("primary6"), ctx).Value);
        Assert.Equal((int)DohMode.Manual, CatalogDiscovery.ReadDns(Target("primary6Doh"), ctx).Value);
        Assert.Equal("https://dns.example/q", CatalogDiscovery.ReadDns(Target("primary6Template"), ctx).Value);
        Assert.Equal(true, CatalogDiscovery.ReadDns(Target("primary6Fallback"), ctx).Value);
    }

    // Off reads as absent, so only an encryption the user chose shows up in a saved config.
    [Fact]
    public void ReadDns_UnencryptedServer_ReadsNoEncryption()
        => Assert.False(CatalogDiscovery.ReadDns(Target("primaryDoh"), new FakeCtx(new DnsServer("10.0.0.1"))).Present);

    [Fact]
    public void Build_Preset_AppliesItsServersUnencrypted()
    {
        var op = Assert.IsType<DnsSetOp>(Assert.Single(ApplyPlanBuilder.Build(Dns, LocKey.Setting.GamingDnsServer.Option5)));
        Assert.Equal(Quad9Servers, op.Servers.Select(s => s.Address));
        Assert.All(op.Servers, s => Assert.Equal(DohMode.Off, s.Doh));
    }

    // Deliberate: every preset is one write of the whole server list, so manual IPv6 servers and encryption go with it.
    [Fact]
    public void Build_EveryPreset_ClearsIpv6ServersAndEncryption()
        => Assert.All(Dns.States, state =>
        {
            var op = Assert.IsType<DnsSetOp>(Assert.Single(ApplyPlanBuilder.Build(Dns, state.Label)));
            Assert.DoesNotContain(op.Servers, s => s.IsIPv6);
            Assert.All(op.Servers, s => Assert.Equal(DohMode.Off, s.Doh));
        });

    // Windows Settings empties both families when DNS goes back to Automatic, and a preset is IPv4 only, so a PC with
    // manual IPv6 servers is on none of these states.
    [Fact]
    public void States_EveryState_ReadsAsItselfOnlyWithNoIpv6Servers()
        => Assert.All(Dns.States, state =>
        {
            Assert.Equal(StateValue.Absent, state.Set["primary6"]);
            Assert.Equal(StateValue.Absent, state.Set["secondary6"]);
        });

    [Fact]
    public void Build_Automatic_AppliesNoServers()
        => Assert.Empty(Assert.IsType<DnsSetOp>(Assert.Single(ApplyPlanBuilder.Build(Dns, LocKey.Setting.GamingDnsServer.Option0))).Servers);

    [Fact]
    public void BuildDnsCustomState_TypedBoxes_ApplyAsServersWithTheirEncryption()
    {
        var op = Assert.IsType<DnsSetOp>(Assert.Single(ApplyPlanBuilder.BuildDnsCustomState(Dns, new Dictionary<string, object>
        {
            ["primary"] = "10.0.0.1",
            ["primaryDoh"] = "2",
            ["primaryTemplate"] = "https://dns.example/q",
            ["primaryFallback"] = "true",
            ["primary6"] = "2001:db8::1",
            ["primary6Doh"] = "1",
        })!));

        Assert.Equal(new DnsServer("10.0.0.1", DohMode.Manual, "https://dns.example/q", Fallback: true), op.Servers[0]);
        Assert.Equal(new DnsServer("2001:db8::1", DohMode.Automatic), op.Servers[1]);
    }

    // Each of these would either hand the machine back to DHCP or write something Windows refuses.
    [Theory]
    [InlineData("secondary", "10.0.0.2")]
    [InlineData("primary", "2001:db8::1")]
    [InlineData("primary6", "10.0.0.1")]
    [InlineData("primaryDoh", "2")]
    public void BuildDnsCustomState_BoxesWindowsWouldRefuse_ReturnsNull(string key, string value)
    {
        var values = new Dictionary<string, object> { [key] = value };
        if (key == "primaryDoh")
            values["primary"] = "10.0.0.1";

        Assert.Null(ApplyPlanBuilder.BuildDnsCustomState(Dns, values));
    }

    // Settings' own values, read off a 26H2 VM.
    [Theory]
    [InlineData(DohMode.Automatic, false, 17)]
    [InlineData(DohMode.Automatic, true, 21)]
    [InlineData(DohMode.Manual, false, 2)]
    [InlineData(DohMode.Manual, true, 6)]
    public void DohFlags_EncryptionModes_MatchWhatWindowsSettingsWrites(DohMode doh, bool fallback, long flags)
    {
        var server = new DnsServer("1.1.1.1", doh, null, fallback);

        Assert.Equal(flags, server.DohFlags);
        Assert.Equal(server, DnsServer.FromFlags("1.1.1.1", flags, null));
    }

    [Theory]
    [InlineData("1.1.1.1", true)]
    [InlineData(" 208.67.222.222 ", true)]
    [InlineData("1.1", false)]
    [InlineData("256.1.1.1", false)]
    [InlineData("2606:4700:4700::1111", false)]
    [InlineData("dns.google", false)]
    public void IsIPv4_OnlyDottedIpv4Address_ReturnsTrue(string text, bool expected)
        => Assert.Equal(expected, DnsAddress.IsIPv4(text));

    [Theory]
    [InlineData("2606:4700:4700::1111", true)]
    [InlineData("::1", true)]
    [InlineData("1.1.1.1", false)]
    [InlineData("2606:zz::1", false)]
    public void IsIPv6_OnlyIpv6Address_ReturnsTrue(string text, bool expected)
        => Assert.Equal(expected, DnsAddress.IsIPv6(text));

    [Fact]
    public void Fields_DohBoxes_AreWindows11Only()
    {
        var doh = Dns.Fields.SelectMany(row => row).Where(f => f.Key.EndsWith("Doh", StringComparison.Ordinal)).ToList();

        Assert.Equal(4, doh.Count);
        Assert.All(doh, f => Assert.Equal(new[] { BuildRange.Windows11 }, f.Builds!));
    }

    [Fact]
    public void Validate_FieldsOnAToggleOrNamingNoTarget_AreRejected()
    {
        var toggle = SettingCatalog.Find("gaming-nagle-algorithm")! with { Fields = [[new Field("missing", FieldKind.Text, TestKeys.Of("x"))]] };

        var errors = CatalogValidator.Validate(toggle);

        Assert.Contains(errors, e => e.Message.Contains("only a Selection may carry them"));
        Assert.Contains(errors, e => e.Message.Contains("Field 'missing' names no target"));
    }

    [Theory]
    [InlineData("fe80::1%12")]
    [InlineData("fe80::1%'; calc; '")]
    public void IsIPv6_AddressWithAZone_IsRejected(string address)
        => Assert.False(DnsAddress.IsIPv6(address));

    [Fact]
    public void IsIPv6_PlainAddress_IsAccepted()
        => Assert.True(DnsAddress.IsIPv6("2606:4700:4700::1111"));
}
