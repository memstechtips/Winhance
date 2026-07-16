using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Catalog;
using Xunit;

namespace Winhance.Infrastructure.Tests.Catalog;

public class CatalogDetectionServiceTests
{
    /// <summary>A fully-controllable detection context: registry reads and powercfg reads come from the supplied
    /// delegates; PrefetchAsync just counts its calls.</summary>
    private sealed class FakeContext : IPrefetchableDetectionContext
    {
        public int PrefetchCount;
        private readonly Func<string, string?, object?> _get;
        private readonly Func<string, string, PowerContext, int?> _power;
        private readonly IReadOnlyList<DynamicOption> _plans;
        private readonly string? _activeGuid;

        public FakeContext(
            Func<string, string?, object?>? get = null,
            Func<string, string, PowerContext, int?>? power = null,
            IReadOnlyList<DynamicOption>? plans = null,
            string? activeGuid = null)
        {
            _get = get ?? ((_, _) => null);
            _power = power ?? ((_, _, _) => null);
            _plans = plans ?? Array.Empty<DynamicOption>();
            _activeGuid = activeGuid;
        }

        public WinBuild CurrentBuild => new(int.MaxValue);
        public object? GetValue(string keyPath, string? valueName) => _get(keyPath, valueName);
        public string[] GetSubKeyNames(string keyPath) => Array.Empty<string>();
        public bool KeyExists(string keyPath) => false;
        public string? PrimaryDnsV4OfActiveAdapter() => null;
        public bool IsSystemRestoreEnabled() => false;
        public bool? ScheduledTaskEnabled(string taskPath) => null;
        public int? PowerCfgValue(string subgroupGuid, string settingGuid, PowerContext context)
            => _power(subgroupGuid, settingGuid, context);
        public string? ActivePowerPlanGuid() => _activeGuid;
        public IReadOnlyList<DynamicOption> InstalledPowerPlans() => _plans;

        public Task PrefetchAsync(IReadOnlyCollection<Setting> settings)
        {
            PrefetchCount++;
            return Task.CompletedTask;
        }
    }

    private static CatalogDetectionService ServiceWith(FakeContext ctx)
    {
        var factory = new Mock<ISystemDetectionContextFactory>();
        factory.Setup(f => f.Create()).Returns(ctx);
        return new CatalogDetectionService(factory.Object, new Mock<ILogService>().Object);
    }

    private static Setting Toggle() => new()
    {
        Id = "toggle",
        Display = new() { Name = "t", Description = "t" },
        Targets = new Target[] { new RegTarget("Mode", new[] { @"HKEY_LOCAL_MACHINE\TEST" }, "Flag", RegistryValueKind.DWord) },
        States = new[]
        {
            new SettingState { Label = "Enabled", Set = new Dictionary<string, StateValue> { ["Mode"] = StateValue.Of(1) } },
            new SettingState { Label = "Disabled", Set = new Dictionary<string, StateValue> { ["Mode"] = StateValue.Of(0) } },
        },
    };

    private static Setting NumericSetting() => new()
    {
        Id = "numeric",
        Display = new() { Name = "n", Description = "n" },
        Targets = new Target[] { new PowerCfgTarget("Power", "sub-guid", "set-guid", PowerModeSupport.Both) },
        Numeric = new() { Min = 0, Max = 100 },
    };

    [Fact]
    public async Task DetectAsync_resolves_a_toggle_to_its_state_label()
    {
        var service = ServiceWith(new FakeContext(get: (_, _) => 1));

        var results = await service.DetectAsync(new[] { Toggle() });

        Assert.True(results["toggle"].Detected);
        Assert.Equal("Enabled", results["toggle"].StateLabel);
        Assert.Null(results["toggle"].Value);
    }

    [Fact]
    public async Task DetectAsync_reads_a_numeric_value_via_DetectValue()
    {
        var service = ServiceWith(new FakeContext(power: (_, _, _) => 42));

        var results = await service.DetectAsync(new[] { NumericSetting() });

        Assert.True(results["numeric"].Detected);
        Assert.Equal(42, results["numeric"].Value);
        Assert.Null(results["numeric"].StateLabel);
    }

    [Fact]
    public async Task DetectAsync_populates_Ac_and_Dc_for_a_powercfg_setting()
    {
        // Distinct AC vs DC so the per-context read is exercised, not one shared value.
        var service = ServiceWith(new FakeContext(power: (_, _, ctx) => ctx == PowerContext.DC ? 7 : 3));

        var results = await service.DetectAsync(new[] { NumericSetting() });

        Assert.Equal(3, results["numeric"].AcValue);
        Assert.Equal(7, results["numeric"].DcValue);
    }

    [Fact]
    public async Task DetectAsync_leaves_Ac_and_Dc_null_for_a_registry_setting()
    {
        var service = ServiceWith(new FakeContext(get: (_, _) => 1));

        var results = await service.DetectAsync(new[] { Toggle() });

        Assert.Null(results["toggle"].AcValue);
        Assert.Null(results["toggle"].DcValue);
    }

    [Fact]
    public async Task DetectAsync_prefetches_once_before_detecting()
    {
        var ctx = new FakeContext();
        var service = ServiceWith(ctx);

        await service.DetectAsync(new[] { Toggle(), NumericSetting() });

        Assert.Equal(1, ctx.PrefetchCount);
    }

    [Fact]
    public async Task DetectAsync_marks_an_unresolved_state_as_not_detected()
    {
        var service = ServiceWith(new FakeContext(get: (_, _) => null));

        var results = await service.DetectAsync(new[] { Toggle() });

        Assert.False(results["toggle"].Detected);
        Assert.Null(results["toggle"].StateLabel);
    }

    private static Setting OptionSourceSetting() => new()
    {
        Id = "power-plan-selection",
        Display = new() { Name = "p", Description = "p" },
        OptionSource = new PowerPlanOptionSource(),
    };

    [Fact]
    public async Task DetectAsync_surfaces_dynamic_options_and_the_current_selection_for_an_option_source_setting()
    {
        var plans = new[]
        {
            new DynamicOption("Balanced", "bbbbbbbb-0000-0000-0000-000000000000"),
            new DynamicOption("High performance", "cccccccc-0000-0000-0000-000000000000"),
        };
        var service = ServiceWith(new FakeContext(plans: plans, activeGuid: "cccccccc-0000-0000-0000-000000000000"));

        var results = await service.DetectAsync(new[] { OptionSourceSetting() });

        var r = results["power-plan-selection"];
        Assert.Equal(plans, r.Options);
        Assert.Equal("cccccccc-0000-0000-0000-000000000000", r.StateLabel); // current selection's Value (GUID)
        Assert.True(r.Detected);
    }

    [Fact]
    public async Task DetectAsync_marks_an_option_source_setting_undetected_when_nothing_is_selected()
    {
        var plans = new[] { new DynamicOption("Balanced", "bbbbbbbb-0000-0000-0000-000000000000") };
        var service = ServiceWith(new FakeContext(plans: plans, activeGuid: null));

        var results = await service.DetectAsync(new[] { OptionSourceSetting() });

        var r = results["power-plan-selection"];
        Assert.Equal(plans, r.Options);
        Assert.Null(r.StateLabel);
        Assert.False(r.Detected);
    }
}
