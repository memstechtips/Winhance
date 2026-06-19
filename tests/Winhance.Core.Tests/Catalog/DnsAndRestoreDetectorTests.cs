using System.Collections.Generic;
using Winhance.Core.Features.Common.Catalog;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class DnsAndRestoreDetectorTests
{
    private sealed class FakeCtx : IDetectionContext
    {
        public string? PrimaryDns;
        public bool RestoreEnabled;
        public object? GetValue(string keyPath, string? valueName) => null;
        public string[] GetSubKeyNames(string keyPath) => System.Array.Empty<string>();
        public string? PrimaryDnsV4OfActiveAdapter() => PrimaryDns;
        public bool IsSystemRestoreEnabled() => RestoreEnabled;
    }

    private static readonly Setting Dummy = new() { Id = "d", Name = "d", Description = "d" };

    private static readonly DnsServerDetector Dns = new("Automatic",
        new Dictionary<string, string> { ["1.1.1.1"] = "Cloudflare", ["8.8.8.8"] = "Google" });

    [Fact]
    public void Dhcp_resolves_to_automatic()
        => Assert.Equal("Automatic", Dns.Detect(Dummy, new FakeCtx { PrimaryDns = null }));

    [Fact]
    public void Known_primary_resolves_to_its_label()
        => Assert.Equal("Cloudflare", Dns.Detect(Dummy, new FakeCtx { PrimaryDns = "1.1.1.1" }));

    [Fact]
    public void Unknown_primary_is_custom()
        => Assert.Null(Dns.Detect(Dummy, new FakeCtx { PrimaryDns = "9.9.9.9" }));

    private static readonly SystemRestoreDetector Restore = new("On", "Off");

    [Fact]
    public void Restore_enabled_resolves_to_on()
        => Assert.Equal("On", Restore.Detect(Dummy, new FakeCtx { RestoreEnabled = true }));

    [Fact]
    public void Restore_disabled_resolves_to_off()
        => Assert.Equal("Off", Restore.Detect(Dummy, new FakeCtx { RestoreEnabled = false }));
}
