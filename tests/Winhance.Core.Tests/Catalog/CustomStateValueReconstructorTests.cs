using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Models;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

// The reconstructor is the single seam that turns a typed detection reading back into the untyped bag the Builder,
// the config export and the apply resolver all consume, so the DNS custom state is pinned here, not at a call site.
public class CustomStateValueReconstructorTests
{
    private static readonly string[] TwoServers = ["10.5.0.1", "10.5.0.2"];
    private static readonly string[] OneServer = ["10.5.0.1"];

    private static Setting DnsSetting() => new()
    {
        Id = "dns",
        Display = new() { Name = "n", Description = "d" },
        States = new[]
        {
            new SettingState { Label = "Automatic" },
            new SettingState { Label = "Cloudflare" },
        },
        Detector = new DnsServerDetector("Automatic", new Dictionary<string, string> { ["1.1.1.1"] = "Cloudflare" }),
    };

    [Fact]
    public void Custom_dns_carries_both_addresses_next_to_the_index()
    {
        var values = CustomStateValueReconstructor.Build(DnsSetting(), new SettingStateResult
        {
            Success = true,
            CurrentValue = ComboBoxConstants.CustomStateIndex,
            DnsServers = TwoServers,
        });

        Assert.Equal(ComboBoxConstants.CustomStateIndex, values["DetectedIndex"]);
        Assert.Equal("10.5.0.1", values["primary"]);
        Assert.Equal("10.5.0.2", values["secondary"]);
    }

    // An empty string would substitute into the script as a real (blank) address; the key has to be absent so the
    // placeholder survives and the script's own guard decides.
    [Fact]
    public void Custom_dns_with_one_server_omits_secondary()
    {
        var values = CustomStateValueReconstructor.Build(DnsSetting(), new SettingStateResult
        {
            Success = true,
            CurrentValue = ComboBoxConstants.CustomStateIndex,
            DnsServers = OneServer,
        });

        Assert.Equal("10.5.0.1", values["primary"]);
        Assert.False(values.ContainsKey("secondary"));
    }

    [Fact]
    public void Custom_dns_with_no_servers_read_carries_only_the_index()
    {
        var values = CustomStateValueReconstructor.Build(DnsSetting(), new SettingStateResult
        {
            Success = true,
            CurrentValue = ComboBoxConstants.CustomStateIndex,
        });

        Assert.Equal(ComboBoxConstants.CustomStateIndex, values["DetectedIndex"]);
        Assert.False(values.ContainsKey("primary"));
        Assert.False(values.ContainsKey("secondary"));
    }

    // A reading that matched a preset option is not a custom state: the option's own baked scripts apply, so the
    // addresses would be dead weight in the file.
    [Fact]
    public void Resolved_dns_option_carries_only_the_index()
    {
        var values = CustomStateValueReconstructor.Build(DnsSetting(), new SettingStateResult
        {
            Success = true,
            CurrentValue = 1,
            DnsServers = TwoServers,
        });

        Assert.Equal(1, values["DetectedIndex"]);
        Assert.False(values.ContainsKey("primary"));
    }
}
