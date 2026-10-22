using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Models;
using Xunit;
using Winhance.TestSupport;

namespace Winhance.Core.Tests.Catalog;

// The reconstructor is the single seam that turns a typed detection reading back into the untyped bag the Builder,
// the config export and the apply resolver all consume.
public class CustomStateValueReconstructorTests
{
    private static Setting Dns() => new()
    {
        Id = "dns",
        Display = new() { Name = TestKeys.Of("n"), Description = TestKeys.Of("d") },
        Targets = new Target[] { new DnsTarget("primary", DnsSlot.Preferred), new DnsTarget("secondary", DnsSlot.Alternate) },
        States = new[] { new SettingState { Label = TestKeys.Of("Automatic") } },
    };

    // A Custom DNS is portable only as its addresses, under the keys the custom-state apply reads them by.
    [Fact]
    public void Dns_servers_ride_in_the_readings_under_their_target_keys()
    {
        var values = CustomStateValueReconstructor.Build(Dns(), new SettingStateResult
        {
            Success = true,
            CurrentValue = ComboBoxConstants.CustomStateIndex,
            Readings = new Dictionary<string, object?> { ["primary"] = "10.5.0.1", ["secondary"] = "10.5.0.2" },
        });

        Assert.Equal("10.5.0.1", values["primary"]);
        Assert.Equal("10.5.0.2", values["secondary"]);
        Assert.False(values.ContainsKey("DetectedIndex"));
    }

    [Fact]
    public void System_tray_carries_the_detected_index()
    {
        var setting = new Setting
        {
            Id = "tray",
            Display = new() { Name = TestKeys.Of("n"), Description = TestKeys.Of("d") },
            States = new[] { new SettingState { Label = TestKeys.Of("Show") }, new SettingState { Label = TestKeys.Of("Hide") } },
            Detector = new SystemTrayDetector(TestKeys.Of("Show"), TestKeys.Of("Hide")),
        };

        var values = CustomStateValueReconstructor.Build(setting, new SettingStateResult { Success = true, CurrentValue = 1 });

        Assert.Equal(1, values["DetectedIndex"]);
    }
}
