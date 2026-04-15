using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class NewBadgeServiceTests
{
    private readonly Mock<IUserPreferencesService> _prefs = new();
    private readonly Mock<ILogService> _log = new();
    private readonly Dictionary<string, string> _store = new();

    public NewBadgeServiceTests()
    {
        // String preference get/set with in-memory backing
        _prefs.Setup(p => p.GetPreference(It.IsAny<string>(), It.IsAny<string>()))
              .Returns((string key, string def) => _store.TryGetValue(key, out var v) ? v : def);
        _prefs.Setup(p => p.SetPreferenceAsync(It.IsAny<string>(), It.IsAny<string>()))
              .Callback<string, string>((key, value) => _store[key] = value)
              .Returns(Task.CompletedTask);

        // Boolean preference get/set with same backing (stored as "True"/"False")
        _prefs.Setup(p => p.GetPreference(It.IsAny<string>(), It.IsAny<bool>()))
              .Returns((string key, bool def) =>
                  _store.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : def);
        _prefs.Setup(p => p.SetPreferenceAsync(It.IsAny<string>(), It.IsAny<bool>()))
              .Callback<string, bool>((key, value) => _store[key] = value.ToString())
              .Returns(Task.CompletedTask);
    }

    [Fact]
    public void ShowNewBadges_DefaultsToTrue_OnFirstInit()
    {
        var sut = new NewBadgeService(_prefs.Object, _log.Object);

        sut.Initialize();

        sut.ShowNewBadges.Should().BeTrue();
    }

    [Fact]
    public void ShowNewBadges_RemainsFalse_WhenSetFalseAndSameVersion()
    {
        // Arrange: simulate "same version" by pre-seeding LastRunVersion to current
        var sut = new NewBadgeService(_prefs.Object, _log.Object);
        sut.Initialize();                       // first run -> writes LastRunVersion
        sut.ShowNewBadges = false;              // user toggles off

        // Act: re-init with same version
        var sut2 = new NewBadgeService(_prefs.Object, _log.Object);
        sut2.Initialize();

        // Assert: still false
        sut2.ShowNewBadges.Should().BeFalse();
    }

    [Fact]
    public void ShowNewBadges_ResetsToTrue_OnVersionUpgrade()
    {
        // Arrange: pretend last-run was an older version
        _store["LastRunVersion"] = "0.0.1";
        _store["NewBadgeBaseline"] = "0.0.1";
        _store["ShowNewBadges"] = "False";

        // Act
        var sut = new NewBadgeService(_prefs.Object, _log.Object);
        sut.Initialize();

        // Assert
        sut.ShowNewBadges.Should().BeTrue();
    }

    [Fact]
    public void IsSettingNew_ReturnsTrue_WhenAddedAfterBaseline()
    {
        _store["LastRunVersion"] = "26.04.10";
        _store["NewBadgeBaseline"] = "26.04.05";
        var sut = new NewBadgeService(_prefs.Object, _log.Object);
        sut.Initialize();

        sut.IsSettingNew("26.04.10", "setting1").Should().BeTrue();
    }

    [Fact]
    public void IsSettingNew_ReturnsFalse_WhenAddedAtOrBeforeBaseline()
    {
        _store["LastRunVersion"] = "26.04.10";
        _store["NewBadgeBaseline"] = "26.04.10";
        var sut = new NewBadgeService(_prefs.Object, _log.Object);
        sut.Initialize();

        sut.IsSettingNew("26.04.05", "setting1").Should().BeFalse();
    }

    [Fact]
    public void IsSettingNew_ReturnsFalse_WhenAddedInVersionIsNullOrEmpty()
    {
        var sut = new NewBadgeService(_prefs.Object, _log.Object);
        sut.Initialize();

        sut.IsSettingNew(null, "setting1").Should().BeFalse();
        sut.IsSettingNew("", "setting2").Should().BeFalse();
    }
}
