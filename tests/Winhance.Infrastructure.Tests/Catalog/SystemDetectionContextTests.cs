using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Catalog;
using Xunit;

namespace Winhance.Infrastructure.Tests.Catalog;

public class SystemDetectionContextTests
{
    private static readonly string[] ParentSubKeys = ["a", "b"];

    private static readonly string CurrentVersionKey =
        @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    private static (SystemDetectionContext ctx,
        Mock<IWindowsRegistryService> reg,
        Mock<ISystemRestoreService> restore,
        Mock<IScheduledTaskStateService> tasks,
        Mock<IPowerSettingsQueryService> power) Build()
    {
        var reg = new Mock<IWindowsRegistryService>();
        var restore = new Mock<ISystemRestoreService>();
        var tasks = new Mock<IScheduledTaskStateService>();
        // Default to the real contract: an entry for EVERY requested path, null meaning "not registered
        // on this machine". Without this Moq hands back a null dictionary, which is a shape the service
        // never produces. Individual tests override this Setup to return specific states.
        tasks.Setup(t => t.GetTasksEnabled(It.IsAny<IReadOnlyCollection<string>>()))
            .Returns((IReadOnlyCollection<string> paths) => paths.ToDictionary(p => p, _ => (bool?)null));
        var power = new Mock<IPowerSettingsQueryService>();
        var log = new Mock<ILogService>();
        var ctx = new SystemDetectionContext(reg.Object, restore.Object, tasks.Object, power.Object, log.Object);
        return (ctx, reg, restore, tasks, power);
    }

    private static Setting SettingWith(params Target[] targets) => new()
    {
        Id = "s",
        Display = new() { Name = "s", Description = "s" },
        Targets = targets,
    };

    [Fact]
    public void GetValue_delegates_to_registry()
    {
        var (ctx, reg, _, _, _) = Build();
        reg.Setup(r => r.GetValue("HKLM\\Test", "ValueName")).Returns(42);

        Assert.Equal(42, ctx.GetValue("HKLM\\Test", "ValueName"));
    }

    [Fact]
    public void GetValue_maps_null_value_name_to_empty_string()
    {
        var (ctx, reg, _, _, _) = Build();
        reg.Setup(r => r.GetValue("HKLM\\Test", "")).Returns(7);

        Assert.Equal(7, ctx.GetValue("HKLM\\Test", null));
    }

    [Fact]
    public void KeyExists_and_GetSubKeyNames_delegate_to_registry()
    {
        var (ctx, reg, _, _, _) = Build();
        reg.Setup(r => r.KeyExists("HKLM\\Present")).Returns(true);
        reg.Setup(r => r.GetSubKeyNames("HKLM\\Parent")).Returns(ParentSubKeys);

        Assert.True(ctx.KeyExists("HKLM\\Present"));
        Assert.Equal(ParentSubKeys, ctx.GetSubKeyNames("HKLM\\Parent"));
    }

    [Fact]
    public void IsSystemRestoreEnabled_delegates_to_restore_service()
    {
        var (ctx, _, restore, _, _) = Build();
        restore.Setup(r => r.IsEnabledForC()).Returns(true);

        Assert.True(ctx.IsSystemRestoreEnabled());
    }

    [Fact]
    public void CurrentBuild_reads_build_number_and_ubr()
    {
        var (ctx, reg, _, _, _) = Build();
        reg.Setup(r => r.GetValue(CurrentVersionKey, "CurrentBuildNumber")).Returns("22631");
        reg.Setup(r => r.GetValue(CurrentVersionKey, "UBR")).Returns(3527);

        var build = ctx.CurrentBuild;

        Assert.Equal(22631, build.Build);
        Assert.Equal(3527, build.Revision);
    }

    [Fact]
    public async Task ScheduledTaskEnabled_returns_the_prefetched_value()
    {
        var (ctx, _, _, tasks, _) = Build();
        tasks.Setup(t => t.GetTasksEnabled(It.IsAny<IReadOnlyCollection<string>>()))
            .Returns(new Dictionary<string, bool?> { ["\\MyTask"] = true });
        var setting = SettingWith(new TaskTarget("Task", "\\MyTask"));

        await ctx.PrefetchAsync(new[] { setting });

        Assert.True(ctx.ScheduledTaskEnabled("\\MyTask"));
    }

    [Fact]
    public async Task ScheduledTaskEnabled_returns_null_for_an_unfetched_path()
    {
        var (ctx, _, _, _, _) = Build();

        await ctx.PrefetchAsync(new[] { SettingWith() });

        Assert.Null(ctx.ScheduledTaskEnabled("\\NeverFetched"));
    }

    [Fact]
    public async Task PowerCfgValue_serves_ac_and_dc_from_the_batched_read()
    {
        var (ctx, _, _, _, power) = Build();
        power.Setup(p => p.GetAllPowerSettingsACDCAsync("SCHEME_CURRENT"))
            .ReturnsAsync(new Dictionary<string, (int? acValue, int? dcValue)> { ["set-guid"] = (5, 9) });
        var setting = SettingWith(new PowerCfgTarget("Power", "sub-guid", "set-guid", PowerModeSupport.Both));

        await ctx.PrefetchAsync(new[] { setting });

        Assert.Equal(5, ctx.PowerCfgValue("sub-guid", "set-guid", PowerContext.AC));
        Assert.Equal(9, ctx.PowerCfgValue("sub-guid", "set-guid", PowerContext.DC));
    }

    [Fact]
    public async Task PowerCfgValue_returns_null_when_the_setting_is_absent_from_the_read_set()
    {
        var (ctx, _, _, _, power) = Build();
        power.Setup(p => p.GetAllPowerSettingsACDCAsync("SCHEME_CURRENT"))
            .ReturnsAsync(new Dictionary<string, (int? acValue, int? dcValue)>());
        var setting = SettingWith(new PowerCfgTarget("Power", "sub-guid", "set-guid", PowerModeSupport.Both));

        await ctx.PrefetchAsync(new[] { setting });

        Assert.Null(ctx.PowerCfgValue("sub-guid", "set-guid", PowerContext.AC));
    }

    [Fact]
    public void PowerCfgValue_returns_null_before_any_prefetch()
    {
        var (ctx, _, _, _, _) = Build();

        Assert.Null(ctx.PowerCfgValue("sub-guid", "set-guid", PowerContext.AC));
    }

    [Fact]
    public async Task ActivePowerPlanGuid_returns_the_lowercased_active_scheme()
    {
        var (ctx, _, _, _, power) = Build();
        power.Setup(p => p.GetActivePowerPlanAsync())
            .ReturnsAsync(new PowerPlan { Guid = "AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE", Name = "High", IsActive = true });
        power.Setup(p => p.GetAvailablePowerPlansAsync()).ReturnsAsync(new List<PowerPlan>());
        var setting = new Setting
        {
            Id = "power-plan-selection",
            Display = new() { Name = "p", Description = "p" },
            Detector = new PowerPlanDetector(),
        };

        await ctx.PrefetchAsync(new[] { setting });

        Assert.Equal("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", ctx.ActivePowerPlanGuid());
    }

    [Fact]
    public async Task PrefetchAsync_does_not_read_power_when_no_setting_needs_it()
    {
        var (ctx, reg, _, _, power) = Build();
        reg.Setup(r => r.GetValue(CurrentVersionKey, It.IsAny<string>())).Returns("0");

        await ctx.PrefetchAsync(new[] { SettingWith(new TaskTarget("Task", "\\T")) });

        power.Verify(p => p.GetAllPowerSettingsACDCAsync(It.IsAny<string>()), Times.Never);
        power.Verify(p => p.GetActivePowerPlanAsync(), Times.Never);
        power.Verify(p => p.GetAvailablePowerPlansAsync(), Times.Never);
    }

    private static Setting PowerPlanSetting() => new()
    {
        Id = "power-plan-selection",
        Display = new() { Name = "p", Description = "p" },
        OptionSource = new PowerPlanOptionSource(),
    };

    [Fact]
    public async Task InstalledPowerPlans_reflects_the_prefetched_plans_via_PowerPlanOptions_Build()
    {
        var (ctx, _, _, _, power) = Build();
        var systemPlans = new List<PowerPlan>
        {
            new() { Name = "Balanced", Guid = "381b4222-f694-41f0-9685-ff5bb260df2e" },
            new() { Name = "My Custom Plan", Guid = "AAAAAAAA-0000-0000-0000-000000000000" },
        };
        power.Setup(p => p.GetActivePowerPlanAsync())
            .ReturnsAsync(new PowerPlan { Guid = "381b4222-f694-41f0-9685-ff5bb260df2e", Name = "Balanced", IsActive = true });
        power.Setup(p => p.GetAvailablePowerPlansAsync()).ReturnsAsync(systemPlans);

        // An OptionSource (no Detector) must trigger the per-batch plan pre-fetch, which populates the installed
        // plans via the faithful PowerPlanOptions.Build port (content equivalence is covered by
        // PowerPlanOptionsConformanceTests; this asserts the context delegates to it on the prefetched plans).
        await ctx.PrefetchAsync(new[] { PowerPlanSetting() });

        Assert.Equal(PowerPlanOptions.Build(systemPlans), ctx.InstalledPowerPlans());
    }

    [Fact]
    public void InstalledPowerPlans_returns_empty_before_any_prefetch()
    {
        var (ctx, _, _, _, _) = Build();

        Assert.Empty(ctx.InstalledPowerPlans());
    }

    [Fact]
    public async Task PowerPlanOptionSource_enumerates_options_and_reports_the_active_selection()
    {
        var (ctx, _, _, _, power) = Build();
        power.Setup(p => p.GetActivePowerPlanAsync())
            .ReturnsAsync(new PowerPlan { Guid = "CCCCCCCC-0000-0000-0000-000000000000", Name = "High performance", IsActive = true });
        power.Setup(p => p.GetAvailablePowerPlansAsync()).ReturnsAsync(new List<PowerPlan>
        {
            new() { Name = "Balanced", Guid = "BBBBBBBB-0000-0000-0000-000000000000" },
            new() { Name = "High performance", Guid = "CCCCCCCC-0000-0000-0000-000000000000" },
        });
        await ctx.PrefetchAsync(new[] { PowerPlanSetting() });

        var source = new PowerPlanOptionSource();

        Assert.Equal(ctx.InstalledPowerPlans(), source.EnumerateOptions(ctx));
        Assert.Equal("cccccccc-0000-0000-0000-000000000000", source.CurrentSelection(ctx));
    }
}
