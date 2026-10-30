using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Xunit;
using Winhance.TestSupport;
using Winhance.Core.Features.Common.Localization;

namespace Winhance.UI.Tests.Services;

public class SettingLocalizationServiceTests
{
    private readonly Mock<ILocalizationService> _localizationService = new();
    private readonly Mock<ICatalogSettingsRegistry> _catalogSettingsRegistry = new();
    private readonly Mock<ICatalogScopeProvider> _scopeProvider = new();

    public SettingLocalizationServiceTests()
    {
        // Default: return the key wrapped in brackets to indicate "not found"
        _localizationService.Setup(l => l.GetString(It.IsAny<string>()))
            .Returns<string>(k => $"[{k}]");
        // Mirrors the stub above onto TryGetString - an unstubbed Moq answers "missing" for every key.
        _localizationService.MirrorTryGetString();
        // Default: both filters ON (the normal mode) -> the service passes the current-machine scope
        _scopeProvider.Setup(p => p.Current).Returns(CatalogScope.CurrentMachine);
    }

    private SettingLocalizationService CreateSut() => new(
        _localizationService.Object,
        _catalogSettingsRegistry.Object,
        _scopeProvider.Object);

    private static Setting CreateTestSetting(
        string id = "test-setting",
        Dictionary<string, LocKey>? crossGroupChildSettings = null) => new()
    {
        Id = id,
        Display = new Display
        {
            Name = TestKeys.Of("Test Setting"),
            Description = TestKeys.Of("Test Description"),
            GroupName = TestKeys.Of("TestGroup"),
            CrossGroupChildSettings = crossGroupChildSettings,
        },
    };

    [Fact]
    public void BuildCrossGroupInfoMessage_WhenNoCustomProperties_ReturnsNull()
    {
        var sut = CreateSut();
        var setting = CreateTestSetting();

        var result = sut.BuildCrossGroupInfoMessage(setting);

        result.Should().BeNull();
    }

    [Fact]
    public void BuildCrossGroupInfoMessage_WhenNoCrossGroupSettings_ReturnsNull()
    {
        var sut = CreateSut();
        var setting = CreateTestSetting(crossGroupChildSettings: new Dictionary<string, LocKey>());

        var result = sut.BuildCrossGroupInfoMessage(setting);

        result.Should().BeNull();
    }

    [Fact]
    public void BuildCrossGroupInfoMessage_WhenChildSettingsExist_BuildsMessage()
    {
        var crossGroupSettings = new Dictionary<string, LocKey>
        {
            ["privacy-child1"] = TestKeys.Of("Setting_Child1_Name")
        };

        var childSetting = new Setting
        {
            Id = "privacy-child1",
            Display = new Display
            {
                Name = TestKeys.Of("Child Setting 1"),
                Description = TestKeys.Of("Child desc"),
                GroupName = TestKeys.Of("SettingGroup_PrivacyGroup"),
            },
        };
        _catalogSettingsRegistry.Setup(r => r.GetById("privacy-child1", It.IsAny<CatalogScope>()))
            .Returns(childSetting);

        _localizationService.Setup(l => l.GetString("Setting_CrossGroupWarning_Header"))
            .Returns("Warning Header");
        _localizationService.Setup(l => l.GetString("Setting_Child1_Name"))
            .Returns("Localized Child");
        _localizationService.Setup(l => l.GetString("Feature_Privacy_Name"))
            .Returns("Privacy & Security");
        _localizationService.Setup(l => l.GetString("SettingGroup_PrivacyGroup"))
            .Returns("Privacy Group Localized");

        var sut = CreateSut();
        var setting = CreateTestSetting(crossGroupChildSettings: crossGroupSettings);

        var result = sut.BuildCrossGroupInfoMessage(setting);

        result.Should().NotBeNull();
        result.Should().Contain("Warning Header");
        result.Should().Contain("Localized Child");
        // Pin the group-key construction (feature name + localized group) so a wrong Display field read fails.
        result.Should().Contain("Privacy & Security (Privacy Group Localized)");
        // Pin the mode threading: both filters ON must query the current-machine scope.
        _catalogSettingsRegistry.Verify(r => r.GetById("privacy-child1", CatalogScope.CurrentMachine), Times.Once);
    }

    [Fact]
    public void BuildCrossGroupInfoMessage_WhenChildNotResolved_SkipsSetting()
    {
        var crossGroupSettings = new Dictionary<string, LocKey>
        {
            ["unknown-child1"] = TestKeys.Of("Setting_Unknown_Name")
        };

        // An id outside the mode-scoped catalog membership resolves to null.
        _catalogSettingsRegistry.Setup(r => r.GetById("unknown-child1", It.IsAny<CatalogScope>()))
            .Returns((Setting?)null);

        var sut = CreateSut();
        var setting = CreateTestSetting(crossGroupChildSettings: crossGroupSettings);

        var result = sut.BuildCrossGroupInfoMessage(setting);

        result.Should().BeNull();
    }

    [Fact]
    public void BuildCrossGroupInfoMessage_WhenFilterOff_QueriesOtherOsScope()
    {
        var crossGroupSettings = new Dictionary<string, LocKey>
        {
            ["privacy-child1"] = TestKeys.Of("Setting_Child1_Name")
        };

        _scopeProvider.Setup(p => p.Current).Returns(new CatalogScope(true, false));

        var childSetting = new Setting
        {
            Id = "privacy-child1",
            Display = new Display
            {
                Name = TestKeys.Of("Child Setting 1"),
                Description = TestKeys.Of("Child desc"),
                GroupName = TestKeys.Of("SettingGroup_PrivacyGroup"),
            },
        };
        // Strict on the scope arg: only the other-OS-versions scope resolves - the current-machine scope
        // returns null and fails NotBeNull, so the provider threading is load-bearing here.
        _catalogSettingsRegistry.Setup(r => r.GetById("privacy-child1", new CatalogScope(true, false)))
            .Returns(childSetting);

        var sut = CreateSut();
        var setting = CreateTestSetting(crossGroupChildSettings: crossGroupSettings);

        var result = sut.BuildCrossGroupInfoMessage(setting);

        result.Should().NotBeNull();
        _catalogSettingsRegistry.Verify(r => r.GetById("privacy-child1", new CatalogScope(true, false)), Times.Once);
    }
}
