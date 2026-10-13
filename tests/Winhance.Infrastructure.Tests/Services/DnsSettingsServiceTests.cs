using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Optimize.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class DnsSettingsServiceTests
{
    private const string Adapter = "{CAEC250C-47BA-49B1-98FC-A71066E577FE}";
    private static readonly string AdapterKey = $@"{DnsSettingsService.Interfaces}\{Adapter}";
    private static readonly string AdapterKey6 = $@"{DnsSettingsService.Interfaces6}\{Adapter}";
    private static readonly string DohKey = DnsSettingsService.DohKey(Adapter, ipv6: false);
    private static readonly string DohKey6 = DnsSettingsService.DohKey(Adapter, ipv6: true);

    private readonly Mock<IWindowsRegistryService> _reg = new();
    private readonly Mock<INetworkAdapters> _adapters = new();
    private readonly DnsSettingsService _sut;

    public DnsSettingsServiceTests()
    {
        _reg.Setup(r => r.SetValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<RegistryValueKind>())).Returns(true);
        _reg.Setup(r => r.CreateKey(It.IsAny<string>())).Returns(true);
        _reg.Setup(r => r.KeyExists(AdapterKey)).Returns(true);
        _reg.Setup(r => r.KeyExists(AdapterKey6)).Returns(true);
        _reg.Setup(r => r.GetValue($@"{DnsSettingsService.WellKnownServers}\1.1.1.1", "Template")).Returns("https://cloudflare-dns.com/dns-query");
        _adapters.Setup(a => a.Ids()).Returns([Adapter]);
        _adapters.Setup(a => a.NudgeDnsClient()).Returns(true);
        _sut = new DnsSettingsService(_reg.Object, _adapters.Object, new Mock<ILogService>().Object);
    }

    [Fact]
    public void Each_family_is_written_as_one_list_then_the_dns_client_rereads()
    {
        _sut.SetDns([new DnsServer("1.1.1.1"), new DnsServer("1.0.0.1"), new DnsServer("2606:4700:4700::1111")]).Should().BeTrue();

        _reg.Verify(r => r.SetValue(AdapterKey, "NameServer", "1.1.1.1,1.0.0.1", RegistryValueKind.String));
        _reg.Verify(r => r.SetValue(AdapterKey6, "NameServer", "2606:4700:4700::1111", RegistryValueKind.String));
        _adapters.Verify(a => a.NudgeDnsClient());
    }

    // The servers are in the registry and take effect on the next reconnect.
    [Fact]
    public void A_write_the_dns_client_does_not_reread_still_succeeds()
    {
        _adapters.Setup(a => a.NudgeDnsClient()).Returns(false);

        _sut.SetDns([new DnsServer("1.1.1.1"), new DnsServer("1.0.0.1")]).Should().BeTrue();

        _reg.Verify(r => r.SetValue(AdapterKey, "NameServer", "1.1.1.1,1.0.0.1", RegistryValueKind.String));
    }

    [Fact]
    public void A_refused_registry_write_fails()
    {
        _reg.Setup(r => r.SetValue(AdapterKey, "NameServer", It.IsAny<object>(), RegistryValueKind.String)).Returns(false);

        _sut.SetDns([new DnsServer("1.1.1.1")]).Should().BeFalse();
    }

    [Fact]
    public void An_adapter_with_no_tcpip_key_is_skipped()
    {
        _reg.Setup(r => r.KeyExists(AdapterKey)).Returns(false);

        _sut.SetDns([new DnsServer("1.1.1.1")]).Should().BeTrue();

        _reg.Verify(r => r.SetValue(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<RegistryValueKind>()), Times.Never);
    }

    // Deliberate: a preset is the whole DNS dialog, so its IPv4 pair empties the IPv6 list and removes all encryption.
    [Fact]
    public void A_presets_servers_clear_ipv6_and_encryption()
    {
        _sut.SetDns([new DnsServer("9.9.9.9"), new DnsServer("149.112.112.112")]);

        _reg.Verify(r => r.SetValue(AdapterKey6, "NameServer", "", RegistryValueKind.String));
        _reg.Verify(r => r.DeleteKey(DohKey));
        _reg.Verify(r => r.DeleteKey(DohKey6));
        _reg.Verify(r => r.CreateKey(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Automatic_writes_empty_lists_and_clears_encryption()
    {
        _sut.SetDns([]).Should().BeTrue();

        _reg.Verify(r => r.SetValue(AdapterKey, "NameServer", "", RegistryValueKind.String));
        _reg.Verify(r => r.SetValue(AdapterKey6, "NameServer", "", RegistryValueKind.String));
        _reg.Verify(r => r.DeleteKey(DohKey));
        _reg.Verify(r => r.DeleteKey(DohKey6));
        _reg.Verify(r => r.CreateKey(It.IsAny<string>()), Times.Never);
    }

    // What Settings writes for an automatic entry: its flags and the built-in template, copied.
    [Fact]
    public void Automatic_encryption_copies_the_built_in_template()
    {
        _sut.SetDns([new DnsServer("1.1.1.1", DohMode.Automatic)]);

        _reg.Verify(r => r.SetValue($@"{DohKey}\1.1.1.1", "DohFlags", 17L, RegistryValueKind.QWord));
        _reg.Verify(r => r.SetValue($@"{DohKey}\1.1.1.1", "DohTemplate", "https://cloudflare-dns.com/dns-query", RegistryValueKind.String));
    }

    [Fact]
    public void A_manual_template_and_fallback_go_under_the_servers_own_family()
    {
        _sut.SetDns([new DnsServer("2001:db8::1", DohMode.Manual, "https://dns.example/q", Fallback: true)]);

        _reg.Verify(r => r.SetValue($@"{DohKey6}\2001:db8::1", "DohFlags", 6L, RegistryValueKind.QWord));
        _reg.Verify(r => r.SetValue($@"{DohKey6}\2001:db8::1", "DohTemplate", "https://dns.example/q", RegistryValueKind.String));
    }

    [Fact]
    public void An_unencrypted_server_gets_no_key()
    {
        _sut.SetDns([new DnsServer("1.1.1.1")]);

        _reg.Verify(r => r.CreateKey(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Reading_returns_each_family_in_order_with_its_encryption()
    {
        _reg.Setup(r => r.GetValue(AdapterKey, "NameServer")).Returns("9.9.9.9 5.6.7.8");
        _reg.Setup(r => r.GetValue(AdapterKey6, "NameServer")).Returns("2620:fe::fe");
        _reg.Setup(r => r.GetValue($@"{DohKey}\9.9.9.9", "DohFlags")).Returns(21L);
        _reg.Setup(r => r.GetValue($@"{DohKey}\9.9.9.9", "DohTemplate")).Returns("https://dns.quad9.net/dns-query");
        _reg.Setup(r => r.GetValue($@"{DohKey}\5.6.7.8", "DohFlags")).Returns(2L);
        _reg.Setup(r => r.GetValue($@"{DohKey}\5.6.7.8", "DohTemplate")).Returns("https://dns.example.net/dns-query");

        DnsSettingsService.Read(_reg.Object, Adapter).Should().Equal(
            new DnsServer("9.9.9.9", DohMode.Automatic, "https://dns.quad9.net/dns-query", Fallback: true),
            new DnsServer("5.6.7.8", DohMode.Manual, "https://dns.example.net/dns-query"),
            new DnsServer("2620:fe::fe"));
    }

    [Fact]
    public void Reading_skips_an_address_of_the_wrong_family()
    {
        _reg.Setup(r => r.GetValue(AdapterKey, "NameServer")).Returns("fe80::1,1.1.1.1");

        DnsSettingsService.Read(_reg.Object, Adapter).Should().Equal(new DnsServer("1.1.1.1"));
    }
}
