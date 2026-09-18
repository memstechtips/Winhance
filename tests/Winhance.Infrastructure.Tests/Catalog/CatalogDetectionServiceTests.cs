using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Catalog;
using Winhance.Infrastructure.Tests.Services;
using Xunit;
using Winhance.TestSupport;

using Winhance.Core.Features.Common.Localization;
namespace Winhance.Infrastructure.Tests.Catalog;

public class CatalogDetectionServiceTests
{
    private static readonly string[] TestPaths = [@"HKEY_LOCAL_MACHINE\TEST"];
    private static readonly string[] HkcuXPath = [@"HKEY_CURRENT_USER\X"];
    private static readonly string[] WallpapersPath = [@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Wallpapers"];

    private sealed class FakeContext : IPrefetchableDetectionContext
    {
        public int PrefetchCount;
        private readonly Func<string, string?, object?> _get;
        private readonly Func<string, string, PowerContext, int?> _power;
        private readonly IReadOnlyList<DynamicOption> _plans;
        private readonly string? _activeGuid;
        private readonly Func<byte[], string?> _shellFolder;

        public FakeContext(
            Func<string, string?, object?>? get = null,
            Func<string, string, PowerContext, int?>? power = null,
            IReadOnlyList<DynamicOption>? plans = null,
            string? activeGuid = null,
            Func<byte[], string?>? shellFolder = null)
        {
            _get = get ?? ((_, _) => null);
            _power = power ?? ((_, _, _) => null);
            _plans = plans ?? Array.Empty<DynamicOption>();
            _activeGuid = activeGuid;
            _shellFolder = shellFolder ?? (_ => null);
        }

        public WinBuild CurrentBuild => new(int.MaxValue);
        public object? GetValue(string keyPath, string? valueName) => _get(keyPath, valueName);
        public string[] GetSubKeyNames(string keyPath) => Array.Empty<string>();
        public bool KeyExists(string keyPath) => false;
        public string? PrimaryDnsV4OfActiveAdapter() => null;
        public IReadOnlyList<string> DnsV4ServersOfActiveAdapter() => Array.Empty<string>();
        public bool IsSystemRestoreEnabled() => false;
        public bool? ScheduledTaskEnabled(string taskPath) => null;
        public int? PowerCfgValue(string subgroupGuid, string settingGuid, PowerContext context)
            => _power(subgroupGuid, settingGuid, context);
        public string? ActivePowerPlanGuid() => _activeGuid;
        public IReadOnlyList<DynamicOption> InstalledPowerPlans() => _plans;
        public string? ShellFolderPath(byte[] itemList) => _shellFolder(itemList);

        public Task PrefetchAsync(IReadOnlyCollection<Setting> settings)
        {
            PrefetchCount++;
            return Task.CompletedTask;
        }
    }

    private static CatalogDetectionService ServiceWith(FakeContext ctx) => ServiceWith(ctx, new Mock<ILogService>().Object);

    private static CatalogDetectionService ServiceWith(FakeContext ctx, ILogService log)
    {
        var factory = new Mock<ISystemDetectionContextFactory>();
        factory.Setup(f => f.Create()).Returns(ctx);
        return new CatalogDetectionService(
            factory.Object, log, OptionProviderFixtures.Registry(), OptionProviderFixtures.ThemeService(Mock.Of<ILocalizationService>()));
    }

    private static Setting Toggle() => new()
    {
        Id = "toggle",
        Display = new() { Name = TestKeys.Of("t"), Description = TestKeys.Of("t") },
        Targets = new Target[] { new RegTarget("Mode", TestPaths, "Flag", RegistryValueKind.DWord) },
        States = new[]
        {
            new SettingState { Label = LocKey.Common.Enabled, Set = new Dictionary<string, StateValue> { ["Mode"] = StateValue.Of(1) } },
            new SettingState { Label = LocKey.Common.Disabled, Set = new Dictionary<string, StateValue> { ["Mode"] = StateValue.Of(0) } },
        },
    };

    private static Setting NumericSetting() => new()
    {
        Id = "numeric",
        Display = new() { Name = TestKeys.Of("n"), Description = TestKeys.Of("n") },
        Targets = new Target[] { new PowerCfgTarget("Power", "sub-guid", "set-guid", PowerModeSupport.Both) },
        Numeric = new() { Min = 0, Max = 100 },
    };

    [Fact]
    public async Task DetectAsync_resolves_a_toggle_to_its_state_label()
    {
        var service = ServiceWith(new FakeContext(get: (_, _) => 1));

        var results = await service.DetectAsync(new[] { Toggle() });

        Assert.True(results["toggle"].Detected);
        Assert.Equal(LocKey.Common.Enabled.Value, results["toggle"].StateLabel);
        Assert.Null(results["toggle"].Value);
    }

    [Fact]
    public async Task A_reading_that_matches_no_state_is_logged_with_what_was_read()
    {
        var log = new Mock<ILogService>();
        var service = ServiceWith(new FakeContext(get: (_, _) => 7), log.Object);

        var results = await service.DetectAsync(new[] { Toggle() });

        Assert.Null(results["toggle"].StateLabel);
        log.Verify(l => l.Log(
            LogLevel.Info,
            It.Is<string>(m => m.Contains("'toggle' matched no state") && m.Contains("Flag=7")),
            null,
            It.IsAny<string>()), Times.Once);
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

    private static Setting AlbumSetting() => new()
    {
        Id = "album",
        Display = new() { Name = TestKeys.Of("a"), Description = TestKeys.Of("a") },
        Targets = new Target[]
        {
            new DesktopSlideshowTarget("album"),
            new RegTarget("BackgroundType", WallpapersPath, "BackgroundType", RegistryValueKind.DWord) { ReadOnly = true },
            new RegTarget("SlideshowDirectoryPath1", WallpapersPath, "SlideshowDirectoryPath1", RegistryValueKind.String) { ReadOnly = true },
        },
        TextBox = new(new TextRule("^.{1,}$", UpperCase: false, TestKeys.Of("a")), SeedKey: "album"),
    };

    private static Func<string, string?, object?> Wallpapers(int? backgroundType) => (_, name) => name switch
    {
        "SlideshowDirectoryPath1" => "AAAA",
        "BackgroundType" => (object?)backgroundType,
        _ => null,
    };

    [Fact]
    public async Task DetectAsync_files_the_decoded_slideshow_folder_under_the_targets_own_key()
    {
        var service = ServiceWith(new FakeContext(
            get: Wallpapers(2),
            shellFolder: _ => @"D:\Pictures\Holiday"));

        var readings = (await service.DetectAsync(new[] { AlbumSetting() }))["album"].Readings;

        Assert.NotNull(readings);
        Assert.Equal(@"D:\Pictures\Holiday", readings!["album"]);
    }

    [Fact]
    public async Task DetectAsync_reads_no_album_when_the_shell_cannot_resolve_the_stored_id()
    {
        var service = ServiceWith(new FakeContext(get: Wallpapers(2)));

        var readings = (await service.DetectAsync(new[] { AlbumSetting() }))["album"].Readings;

        Assert.NotNull(readings);
        Assert.Null(readings!["album"]);
    }

    // Windows leaves the folder id behind when the background stops being a slideshow.
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task DetectAsync_reads_no_album_when_the_background_is_not_a_slideshow(int? backgroundType)
    {
        var service = ServiceWith(new FakeContext(
            get: Wallpapers(backgroundType),
            shellFolder: _ => @"D:\Pictures\Holiday"));

        var readings = (await service.DetectAsync(new[] { AlbumSetting() }))["album"].Readings;

        Assert.NotNull(readings);
        Assert.Null(readings!["album"]);
    }

    private static Setting PowerPlanSetting() => new()
    {
        Id = "power-plan-selection",
        Display = new() { Name = TestKeys.Of("p"), Description = TestKeys.Of("p") },
        Options = new(OptionSource.PowerPlans),
    };

    [Fact]
    public async Task DetectAsync_surfaces_dynamic_options_and_the_current_selection_for_a_keyed_setting()
    {
        var plans = new[]
        {
            new DynamicOption("Balanced", "bbbbbbbb-0000-0000-0000-000000000000"),
            new DynamicOption("High performance", "cccccccc-0000-0000-0000-000000000000"),
        };
        var service = ServiceWith(new FakeContext(plans: plans, activeGuid: "cccccccc-0000-0000-0000-000000000000"));

        var results = await service.DetectAsync(new[] { PowerPlanSetting() });

        var r = results["power-plan-selection"];
        Assert.Equal(plans, r.Options);
        Assert.Equal("cccccccc-0000-0000-0000-000000000000", r.StateLabel); // current selection's Value (GUID)
        Assert.True(r.Detected);
    }

    [Fact]
    public async Task DetectAsync_marks_a_keyed_setting_undetected_when_nothing_is_selected()
    {
        var plans = new[] { new DynamicOption("Balanced", "bbbbbbbb-0000-0000-0000-000000000000") };
        var service = ServiceWith(new FakeContext(plans: plans, activeGuid: null));

        var results = await service.DetectAsync(new[] { PowerPlanSetting() });

        var r = results["power-plan-selection"];
        Assert.Equal(plans, r.Options);
        Assert.Null(r.StateLabel);
        Assert.False(r.Detected);
    }

    // A detection failure is OUR failure: Undetermined, never Custom - Custom is a statement about the user's
    // machine and an ACTIONABLE state whose dialog would write over data we could not read.
    [Fact]
    public async Task DetectAsync_reports_Undetermined_when_detection_throws()
    {
        var service = ServiceWith(new FakeContext(get: (_, _) => throw new InvalidOperationException("registry exploded")));

        var results = await service.DetectAsync(new[] { ThrowingSetting() });

        var r = results["throwing"];
        Assert.Equal(SettingDetectionOutcome.Undetermined, r.Outcome);
        Assert.NotEqual(SettingDetectionOutcome.Custom, r.Outcome);
        Assert.False(r.Detected);
        Assert.Contains("registry exploded", r.OutcomeDetail);
    }

    // The diagnostic naming what the catalog expected is the single most useful line in a report about a setting
    // "showing the wrong thing".
    [Fact]
    public async Task DetectAsync_reports_Malformed_for_a_wrongly_typed_binary_value()
    {
        var service = ServiceWith(new FakeContext(get: (_, _) => "not-bytes"));

        var results = await service.DetectAsync(new[] { BitmaskSetting() });

        var r = results["bitmask"];
        Assert.Equal(SettingDetectionOutcome.Malformed, r.Outcome);
        Assert.Null(r.StateLabel);
        Assert.Contains("Binary", r.OutcomeDetail);
    }

    // The default must not leak a problem onto healthy settings.
    [Fact]
    public async Task DetectAsync_reports_Resolved_for_a_healthy_setting()
    {
        var service = ServiceWith(new FakeContext(get: (_, _) => new byte[] { 0x00, 0x08 }));

        var results = await service.DetectAsync(new[] { BitmaskSetting() });

        Assert.Equal(SettingDetectionOutcome.Resolved, results["bitmask"].Outcome);
    }

    private static Setting ThrowingSetting() => new()
    {
        Id = "throwing",
        Display = new() { Name = TestKeys.Of("t"), Description = TestKeys.Of("d") },
        Targets = new Target[] { new RegTarget("V", HkcuXPath, "V", RegistryValueKind.DWord) },
        States = new[]
        {
            new SettingState { Label = LocKey.Common.Enabled, Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(1) } },
            new SettingState { Label = LocKey.Common.Disabled, Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(0) } },
        },
    };

    private static Setting BitmaskSetting() => new()
    {
        Id = "bitmask",
        Display = new() { Name = TestKeys.Of("b"), Description = TestKeys.Of("d") },
        Targets = new Target[]
        {
            new RegTarget("Mask", HkcuXPath, "Mask", RegistryValueKind.Binary)
            { ByteIndex = 1, BitMask = 0x08 },
        },
        States = new[]
        {
            new SettingState { Label = LocKey.Common.Enabled, Set = new Dictionary<string, StateValue> { ["Mask"] = StateValue.Of(1) } },
            new SettingState { Label = LocKey.Common.Disabled, IsFallback = true, Set = new Dictionary<string, StateValue> { ["Mask"] = StateValue.Of(0) } },
        },
    };
}
