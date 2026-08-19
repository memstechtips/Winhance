using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class SelectionSetBuilderTests
{
    private readonly Mock<ISettingSnapshotSource> _snapshot = new();
    private readonly Mock<IAppSelectionSource> _apps = new();
    private readonly Mock<IApplicationModeService> _mode = new();
    private readonly Mock<IWindowsVersionFilterService> _versionFilter = new();

    public SelectionSetBuilderTests()
    {
        _versionFilter.Setup(f => f.IsFilterEnabled).Returns(true);

        _snapshot.Setup(s => s.CaptureAsync(It.IsAny<CatalogScope>()))
            .ReturnsAsync(new List<SettingChoice>());

        _apps.Setup(a => a.CheckedWindowsAppsAsync()).ReturnsAsync(new List<AppChoice>());
        _apps.Setup(a => a.InstalledWindowsAppsAsync()).ReturnsAsync(new List<AppChoice>());
        _apps.Setup(a => a.CheckedExternalAppsAsync()).ReturnsAsync(new List<AppChoice>());

        _mode.Setup(m => m.GetBuilderEdits()).Returns(new List<SettingChoice>());
    }

    private SelectionSetBuilder CreateSut() =>
        new(_snapshot.Object, _apps.Object, _mode.Object, _versionFilter.Object);

    private static AppChoice Appx(string id) => new(id, id, [$"{id}.Package"], null, null, null);

    private static AppChoice External(string id) => new(id, id, null, null, null, $"Publisher.{id}");

    [Fact]
    public async Task FromMachine_UsesCheckedApps_AndCurrentScope()
    {
        var settings = new List<SettingChoice> { new("t", new ChoiceValue.Toggle(true)) };
        _snapshot.Setup(s => s.CaptureAsync(It.IsAny<CatalogScope>())).ReturnsAsync(settings);
        _apps.Setup(a => a.CheckedWindowsAppsAsync()).ReturnsAsync(new List<AppChoice> { Appx("checked-win") });
        _apps.Setup(a => a.CheckedExternalAppsAsync()).ReturnsAsync(new List<AppChoice> { External("checked-ext") });

        var set = await CreateSut().FromMachineAsync();

        set.Settings.Should().Equal(settings);
        set.WindowsApps.Select(a => a.Id).Should().Equal("checked-win");
        set.ExternalApps.Select(a => a.Id).Should().Equal("checked-ext");
        set.Autounattend.Should().BeSameAs(AutounattendChoices.None);

        _snapshot.Verify(s => s.CaptureAsync(new CatalogScope(IncludeOtherOsVersions: false, IncludeOtherHardware: false)), Times.Once);
        _apps.Verify(a => a.InstalledWindowsAppsAsync(), Times.Never);
    }

    [Fact]
    public async Task FromMachineForBackup_UsesInstalledWindowsApps_NoExternalApps()
    {
        _apps.Setup(a => a.InstalledWindowsAppsAsync()).ReturnsAsync(new List<AppChoice> { Appx("installed-win") });
        _apps.Setup(a => a.CheckedWindowsAppsAsync()).ReturnsAsync(new List<AppChoice> { Appx("checked-win") });

        var set = await CreateSut().FromMachineForBackupAsync();

        set.WindowsApps.Select(a => a.Id).Should().Equal("installed-win");
        set.ExternalApps.Should().BeEmpty();

        _apps.Verify(a => a.CheckedWindowsAppsAsync(), Times.Never);
        _apps.Verify(a => a.CheckedExternalAppsAsync(), Times.Never);
    }

    [Fact]
    public async Task FromBuilderSession_OverlaysEditsById_KeepsUnauthored()
    {
        _snapshot.Setup(s => s.CaptureAsync(It.IsAny<CatalogScope>()))
            .ReturnsAsync(new List<SettingChoice>
            {
                new("t", new ChoiceValue.Toggle(false)),
                new("s", new ChoiceValue.Option(0)),
            });

        _mode.Setup(m => m.GetBuilderEdits())
            .Returns(new List<SettingChoice> { new("s", new ChoiceValue.Option(2)) });

        var set = await CreateSut().FromBuilderSessionAsync();

        set.Settings.Should().Equal(
            new SettingChoice("t", new ChoiceValue.Toggle(false)),
            new SettingChoice("s", new ChoiceValue.Option(2)));
    }

    [Fact]
    public async Task FromBuilderSession_EditForSettingNotInSnapshot_IsAppended()
    {
        _snapshot.Setup(s => s.CaptureAsync(It.IsAny<CatalogScope>()))
            .ReturnsAsync(new List<SettingChoice> { new("t", new ChoiceValue.Toggle(false)) });

        // A power plan authored on a machine whose active scheme could not be read: the snapshot has no entry,
        // the user's choice must still reach Save.
        _mode.Setup(m => m.GetBuilderEdits())
            .Returns(new List<SettingChoice>
            {
                new("t", new ChoiceValue.Toggle(true)),
                new("power-plan-selection", new ChoiceValue.PowerPlan("g-bal", "Balanced")),
            });

        var set = await CreateSut().FromBuilderSessionAsync();

        set.Settings.Should().Equal(
            new SettingChoice("t", new ChoiceValue.Toggle(true)),
            new SettingChoice("power-plan-selection", new ChoiceValue.PowerPlan("g-bal", "Balanced")));
    }

    [Fact]
    public async Task CurrentScope_ReflectsTheVersionFilter()
    {
        var sut = CreateSut();

        sut.CurrentScope.Should().Be(new CatalogScope(IncludeOtherOsVersions: false, IncludeOtherHardware: false));

        _versionFilter.Setup(f => f.IsFilterEnabled).Returns(false);

        sut.CurrentScope.Should().Be(new CatalogScope(IncludeOtherOsVersions: true, IncludeOtherHardware: false));

        await sut.FromMachineAsync();

        _snapshot.Verify(s => s.CaptureAsync(new CatalogScope(IncludeOtherOsVersions: true, IncludeOtherHardware: false)), Times.Once);
    }
}
