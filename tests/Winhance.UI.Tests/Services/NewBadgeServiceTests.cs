using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class NewBadgeServiceTests
{
    private static readonly string[] Version0410Only = ["26.04.10"];
    private static readonly string[] Versions0421And0417And0301 = ["26.04.21", "26.04.17", "26.03.01"];
    private static readonly string[] Versions0421And0417 = ["26.04.21", "26.04.17"];
    private static readonly string[] Versions0301And0420 = ["26.03.01", "26.04.20"];
    private static readonly string[] Versions0420And0301 = ["26.04.20", "26.03.01"];
    private static readonly string[] Version0420Only = ["26.04.20"];

    private readonly Mock<IUserPreferencesService> _prefs = new();
    private readonly Mock<ILogService> _log = new();
    private readonly Dictionary<string, string> _store = new();

    public NewBadgeServiceTests()
    {
        _prefs.Setup(p => p.GetPreference(It.IsAny<string>(), It.IsAny<string>()))
              .Returns((string key, string def) => _store.TryGetValue(key, out var v) ? v : def);
        _prefs.Setup(p => p.SetPreferenceAsync(It.IsAny<string>(), It.IsAny<string>()))
              .Callback<string, string>((key, value) => _store[key] = value)
              .ReturnsAsync(OperationResult.Succeeded());

        _prefs.Setup(p => p.GetPreference(It.IsAny<string>(), It.IsAny<bool>()))
              .Returns((string key, bool def) =>
                  _store.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : def);
        _prefs.Setup(p => p.SetPreferenceAsync(It.IsAny<string>(), It.IsAny<bool>()))
              .Callback<string, bool>((key, value) => _store[key] = value.ToString())
              .ReturnsAsync(OperationResult.Succeeded());
    }

    private NewBadgeService CreateSut() => new NewBadgeService(_prefs.Object, _log.Object);

    // Branch A: no usable stored state (fresh install, or prefs that predate the badge system).

    [Fact]
    public void FreshInstall_ShowsNoNewBadges_AndSeedsHighestAndBaseline()
    {
        var sut = CreateSut();

        sut.Initialize(new[] { "26.04.10", "26.03.01", (string?)null, "" });

        sut.IsSettingNew("26.04.10", "s1").Should().BeFalse();
        sut.IsSettingNew("26.03.01", "s2").Should().BeFalse();

        _store[UserPreferenceKeys.HighestSeenAddedInVersion].Should().Be("26.4.10");
        _store["NewBadgeBaseline"].Should().Be("26.4.10");
    }

    [Fact]
    public void NoStoredHighest_RespectsUserShowNewBadgesPreference()
    {
        // User previously turned NEW badges off — we must not flip it back on here.
        _store[UserPreferenceKeys.ShowNewBadges] = "False";

        var sut = CreateSut();
        sut.Initialize(Version0410Only);

        sut.ShowNewBadges.Should().BeFalse();
    }

    [Fact]
    public void NoStoredHighest_WithNoTaggedSettings_DoesNotSeedHighest()
    {
        var sut = CreateSut();

        sut.Initialize(Array.Empty<string?>());

        _store.ContainsKey(UserPreferenceKeys.HighestSeenAddedInVersion).Should().BeFalse();
        sut.IsSettingNew("26.04.10", "s1").Should().BeFalse();
    }

    [Fact]
    public void HalfPopulatedState_MissingNewBadgeBaseline_RecoversToNoBadges()
    {
        // HighestSeen written by an older build without NewBadgeBaseline: Branch A reseeds both.
        _store[UserPreferenceKeys.HighestSeenAddedInVersion] = "26.04.21";

        var sut = CreateSut();
        sut.Initialize(Versions0421And0417And0301);

        sut.IsSettingNew("26.04.21", "s1").Should().BeFalse();
        sut.IsSettingNew("26.04.17", "s2").Should().BeFalse();
        sut.IsSettingNew("26.03.01", "s3").Should().BeFalse();

        _store["NewBadgeBaseline"].Should().Be("26.4.21");
        _store[UserPreferenceKeys.HighestSeenAddedInVersion].Should().Be("26.4.21");
    }

    [Fact]
    public void HalfPopulatedState_MissingHighestSeen_RecoversToNoBadges()
    {
        _store["NewBadgeBaseline"] = "26.04.17";

        var sut = CreateSut();
        sut.Initialize(Versions0421And0417);

        sut.IsSettingNew("26.04.21", "s1").Should().BeFalse();
        sut.IsSettingNew("26.04.17", "s2").Should().BeFalse();

        _store["NewBadgeBaseline"].Should().Be("26.4.21");
        _store[UserPreferenceKeys.HighestSeenAddedInVersion].Should().Be("26.4.21");
    }

    // Branch B: effective upgrade (registry highest > stored).

    [Fact]
    public void EffectiveUpgrade_ResetsShowNewBadges_AndUpdatesHighestSeen()
    {
        _store[UserPreferenceKeys.HighestSeenAddedInVersion] = "26.03.01";
        _store["NewBadgeBaseline"] = "26.03.01";
        _store[UserPreferenceKeys.ShowNewBadges] = "False";

        var sut = CreateSut();

        sut.Initialize(Versions0301And0420);

        sut.IsSettingNew("26.04.20", "s1").Should().BeTrue();
        sut.IsSettingNew("26.03.01", "s2").Should().BeFalse();

        _store[UserPreferenceKeys.HighestSeenAddedInVersion].Should().Be("26.4.20");

        sut.ShowNewBadges.Should().BeTrue();
    }

    [Fact]
    public void UpgradeFirstRun_ShowsBadges_SecondRunHides()
    {
        _store[UserPreferenceKeys.HighestSeenAddedInVersion] = "26.03.01";
        _store["NewBadgeBaseline"] = "26.03.01";
        _store[UserPreferenceKeys.ShowNewBadges] = "False";

        var first = CreateSut();
        first.Initialize(Versions0301And0420);
        first.ShowNewBadges.Should().BeTrue();
        first.IsSettingNew("26.04.20", "s1").Should().BeTrue();

        var second = CreateSut();
        second.Initialize(Versions0301And0420);
        second.ShowNewBadges.Should().BeFalse();
    }

    [Fact]
    public void ManualReEnable_AfterAutoHide_PersistsAcrossRuns()
    {
        _store[UserPreferenceKeys.HighestSeenAddedInVersion] = "26.03.01";
        _store["NewBadgeBaseline"] = "26.03.01";

        CreateSut().Initialize(Versions0301And0420);
        var second = CreateSut();
        second.Initialize(Versions0301And0420);
        second.ShowNewBadges = true;

        var third = CreateSut();
        third.Initialize(Versions0301And0420);
        third.ShowNewBadges.Should().BeTrue();
    }

    [Fact]
    public void NextUpgrade_ShowsBadgesAgain()
    {
        _store[UserPreferenceKeys.HighestSeenAddedInVersion] = "26.03.01";
        _store["NewBadgeBaseline"] = "26.03.01";

        CreateSut().Initialize(Versions0301And0420);
        CreateSut().Initialize(Versions0301And0420);

        var upgraded = CreateSut();
        upgraded.Initialize(["26.03.01", "26.04.20", "26.05.01"]);

        upgraded.ShowNewBadges.Should().BeTrue();
        upgraded.IsSettingNew("26.05.01", "s1").Should().BeTrue();
        upgraded.IsSettingNew("26.04.20", "s2").Should().BeFalse();
    }

    // Branch C: no upgrade since last run.

    [Fact]
    public void NoUpgrade_LoadsStoredBaseline_AndLeavesShowNewBadgesAlone()
    {
        _store[UserPreferenceKeys.HighestSeenAddedInVersion] = "26.04.20";
        _store["NewBadgeBaseline"] = "26.04.20";
        _store[UserPreferenceKeys.ShowNewBadges] = "False";

        var sut = CreateSut();

        sut.Initialize(Versions0420And0301);

        sut.IsSettingNew("26.04.20", "s1").Should().BeFalse();
        sut.IsSettingNew("26.03.01", "s2").Should().BeFalse();

        sut.ShowNewBadges.Should().BeFalse();

        _store[UserPreferenceKeys.HighestSeenAddedInVersion].Should().Be("26.04.20");
    }

    [Fact]
    public void NoUpgrade_WithShowNewBadgesTrue_StaysTrue()
    {
        _store[UserPreferenceKeys.HighestSeenAddedInVersion] = "26.04.20";
        _store["NewBadgeBaseline"] = "26.04.20";

        var sut = CreateSut();

        sut.Initialize(Version0420Only);

        sut.ShowNewBadges.Should().BeTrue(); // default
    }

    [Fact]
    public void NoUpgrade_AfterEffectiveUpgrade_PreservesNewBadgesAcrossRuns()
    {
        // Simulate the state written by Branch B on a previous launch:
        // user was on 26.04.17 when they upgraded to a build with 26.04.21 settings.
        _store[UserPreferenceKeys.HighestSeenAddedInVersion] = "26.04.21";
        _store["NewBadgeBaseline"] = "26.04.17";

        var sut = CreateSut();
        sut.Initialize(Versions0421And0417And0301);

        // Baseline should still be 26.04.17 — the badge added in 26.04.21 must still show.
        sut.IsSettingNew("26.04.21", "s1").Should().BeTrue();
        sut.IsSettingNew("26.04.17", "s2").Should().BeFalse();
        sut.IsSettingNew("26.03.01", "s3").Should().BeFalse();

        _store[UserPreferenceKeys.HighestSeenAddedInVersion].Should().Be("26.04.21");
    }

    [Fact]
    public void IsSettingNew_ReturnsFalse_WhenAddedInVersionIsNullOrEmpty()
    {
        var sut = CreateSut();
        sut.Initialize(Version0420Only);

        sut.IsSettingNew(null, "s1").Should().BeFalse();
        sut.IsSettingNew("", "s2").Should().BeFalse();
    }

    [Fact]
    public void IsSettingNew_ReturnsFalse_WhenAddedInVersionUnparseable()
    {
        var sut = CreateSut();
        sut.Initialize(Version0420Only);

        sut.IsSettingNew("not-a-version", "s1").Should().BeFalse();
    }

    [Fact]
    public void Initialize_WritesLastRunVersion_ForFutureMigrationUse()
    {
        var sut = CreateSut();
        sut.Initialize(Version0420Only);

        _store.ContainsKey("LastRunVersion").Should().BeTrue();
    }
}
