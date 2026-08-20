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

    // Branch A: no stored HighestSeenAddedInVersion (first-ever install OR
    // returning user whose prefs predate the badge system — same treatment).

    [Fact]
    public void NoStoredHighest_AllTaggedSettingsAreNew_AndSeedsHighestOnExit()
    {
        var sut = CreateSut();

        sut.Initialize(new[] { "26.04.10", "26.03.01", (string?)null, "" });

        sut.IsSettingNew("26.04.10", "s1").Should().BeTrue();
        sut.IsSettingNew("26.03.01", "s2").Should().BeTrue();

        // Highest is seeded from the registry so the next run hits Branch B/C
        _store[UserPreferenceKeys.HighestSeenAddedInVersion].Should().Be("26.4.10");
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
        sut.IsSettingNew("26.04.10", "s1").Should().BeTrue(); // baseline 0.0.0
    }

    [Fact]
    public void HalfPopulatedState_MissingNewBadgeBaseline_RecoversToAllTaggedNew()
    {
        // Real-world scenario: HighestSeen was written by an older build that didn't
        // also write NewBadgeBaseline. Without recovery, Branch C would read an empty
        // NewBadgeBaseline and fall back to HighestSeen as the baseline, hiding every
        // badge forever. Branch A should catch this and reset cleanly.
        _store[UserPreferenceKeys.HighestSeenAddedInVersion] = "26.04.21";

        var sut = CreateSut();
        sut.Initialize(Versions0421And0417And0301);

        sut.IsSettingNew("26.04.21", "s1").Should().BeTrue();
        sut.IsSettingNew("26.04.17", "s2").Should().BeTrue();
        sut.IsSettingNew("26.03.01", "s3").Should().BeTrue();

        _store["NewBadgeBaseline"].Should().Be("0.0.0");
        _store[UserPreferenceKeys.HighestSeenAddedInVersion].Should().Be("26.4.21");
    }

    [Fact]
    public void HalfPopulatedState_MissingHighestSeen_RecoversToAllTaggedNew()
    {
        _store["NewBadgeBaseline"] = "26.04.17";

        var sut = CreateSut();
        sut.Initialize(Versions0421And0417);

        sut.IsSettingNew("26.04.21", "s1").Should().BeTrue();
        sut.IsSettingNew("26.04.17", "s2").Should().BeTrue();

        _store["NewBadgeBaseline"].Should().Be("0.0.0");
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
