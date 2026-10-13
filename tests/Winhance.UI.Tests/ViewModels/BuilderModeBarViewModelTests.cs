using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.ViewModels;
using Xunit;
using Winhance.TestSupport;

namespace Winhance.UI.Tests.ViewModels;

public class BuilderModeBarViewModelTests : IDisposable
{
    private readonly Mock<IApplicationModeService> _mockApplicationModeService = new();
    private readonly Mock<IBuilderSaveService> _mockBuilderSaveService = new();
    private readonly Mock<IDispatcherService> _mockDispatcherService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<IHardwareFilterService> _mockHardwareFilterService = new();
    private readonly Mock<ITargetWindowsService> _mockTargetWindows = new();
    private readonly Mock<ICatalogScopeProvider> _mockScopeProvider = new();
    private readonly Mock<ICatalogSettingsRegistry> _mockCatalogSettingsRegistry = new();
    private readonly Mock<ILogService> _mockLogService = new();

    private BuilderModeBarViewModel? _sut;

    public BuilderModeBarViewModelTests()
    {
        _mockDispatcherService
            .Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(f => f());

        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string key, object[] args) => $"{key}:{string.Join(",", args)}");

        _mockApplicationModeService.Setup(m => m.GetBuilderEdits()).Returns(new List<SettingChoice>());

        _mockHardwareFilterService.Setup(f => f.IsFilterEnabled).Returns(true);
        _mockHardwareFilterService.Setup(f => f.SetAsync(It.IsAny<bool>())).Returns(Task.CompletedTask);

        _mockScopeProvider.Setup(p => p.Current).Returns(BuilderScope with { IncludeOtherHardware = true });
        _mockTargetWindows.Setup(t => t.Build).Returns(26300);
        _mockTargetWindows.Setup(t => t.SetAsync(It.IsAny<int?>())).Returns(Task.CompletedTask);
    }

    private BuilderModeBarViewModel CreateSut() => _sut = new(
        _mockApplicationModeService.Object,
        _mockBuilderSaveService.Object,
        _mockDispatcherService.Object,
        _mockLocalizationService.Object,
        _mockDialogService.Object,
        _mockHardwareFilterService.Object,
        _mockTargetWindows.Object,
        _mockScopeProvider.Object,
        _mockCatalogSettingsRegistry.Object,
        _mockLogService.Object);

    private void EditsAre(params string[] settingIds)
    {
        _mockApplicationModeService.Setup(m => m.GetBuilderEdits()).Returns(
            settingIds.Select(id => new SettingChoice(id, new ChoiceValue.Toggle(true))).ToList());
    }

    // An expression tree cannot contain a with-expression, so the scope is built once outside the Moq lambdas.
    private static readonly CatalogScope BuilderScope = CatalogScope.CurrentMachine with { IncludeAnswerFileOnly = true };

    private void InScope(params string[] settingIds)
    {
        foreach (string id in settingIds)
        {
            _mockCatalogSettingsRegistry
                .Setup(r => r.GetById(id, BuilderScope))
                .Returns(new Setting { Id = id, Display = new Display { Name = TestKeys.Of(id), Description = TestKeys.Of(id)} });
        }
    }

    public void Dispose()
    {
        _sut?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ShowOtherHardware_MirrorsTheFilterInverted()
    {
        var sut = CreateSut();

        sut.ShowOtherHardware.Should().BeFalse();

        _mockHardwareFilterService.Setup(f => f.IsFilterEnabled).Returns(false);

        sut.ShowOtherHardware.Should().BeTrue();
    }

    [Fact]
    public async Task SetShowOtherHardwareAsync_ShowingOtherHardware_TurnsTheFilterOff()
    {
        var sut = CreateSut();

        await sut.SetShowOtherHardwareAsync(true);

        _mockHardwareFilterService.Verify(f => f.SetAsync(false), Times.Once);
    }

    [Fact]
    public async Task SetShowOtherHardwareAsync_HidingOtherHardware_TurnsTheFilterOn()
    {
        var sut = CreateSut();

        await sut.SetShowOtherHardwareAsync(false);

        _mockHardwareFilterService.Verify(f => f.SetAsync(true), Times.Once);
    }

    [Fact]
    public async Task SetShowOtherHardwareAsync_HidingOtherHardware_WithEveryEditInScope_NarrowsWithoutAsking()
    {
        EditsAre("in-scope-a", "in-scope-b");
        InScope("in-scope-a", "in-scope-b");
        var sut = CreateSut();

        await sut.SetShowOtherHardwareAsync(false);

        _mockDialogService.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Never);
        _mockHardwareFilterService.Verify(f => f.SetAsync(true), Times.Once);
    }

    [Fact]
    public async Task SetShowOtherHardwareAsync_HidingOtherHardware_WithOutOfScopeEdits_AsksNamingTheCount()
    {
        EditsAre("absent-a", "in-scope", "absent-b");
        InScope("in-scope");
        ConfirmationRequest? shown = null;
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .Callback<ConfirmationRequest>(request => shown = request)
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });
        var sut = CreateSut();

        await sut.SetShowOtherHardwareAsync(false);

        shown!.Title.Should().Be("Dialog_NarrowHardware_Title");
        shown.Message.Should().Be("Dialog_NarrowHardware_Message:2");
        _mockHardwareFilterService.Verify(f => f.SetAsync(true), Times.Once);
    }

    [Fact]
    public async Task SetShowOtherHardwareAsync_HidingOtherHardware_WhenDeclined_KeepsTheFilterAndTheCheckbox()
    {
        EditsAre("absent-a", "absent-b");
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });
        var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        await sut.SetShowOtherHardwareAsync(false);

        _mockHardwareFilterService.Verify(f => f.SetAsync(It.IsAny<bool>()), Times.Never);
        changed.Should().Contain(nameof(BuilderModeBarViewModel.ShowOtherHardware));
    }

    [Fact]
    public async Task SetShowOtherHardwareAsync_ShowingOtherHardware_NeverAsks()
    {
        EditsAre("absent-a", "absent-b");
        var sut = CreateSut();

        await sut.SetShowOtherHardwareAsync(true);

        _mockDialogService.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Never);
        _mockHardwareFilterService.Verify(f => f.SetAsync(false), Times.Once);
    }

    [Fact]
    public void FilterStateChanged_RaisesPropertyChangedForShowOtherHardware()
    {
        var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _mockHardwareFilterService.Raise(f => f.FilterStateChanged += null, _mockHardwareFilterService.Object, false);

        changed.Should().Contain(nameof(BuilderModeBarViewModel.ShowOtherHardware));
    }

    [Fact]
    public void Dispose_StopsListeningToTheFilter()
    {
        var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        sut.Dispose();
        _mockHardwareFilterService.Raise(f => f.FilterStateChanged += null, _mockHardwareFilterService.Object, false);

        changed.Should().BeEmpty();
    }

    [Fact]
    public void ShowOtherHardwareText_ComesFromLocalization()
    {
        var sut = CreateSut();

        sut.ShowOtherHardwareText.Should().Be("Builder_Mode_ShowOtherHardware");
        sut.ShowOtherHardwareTooltip.Should().Be("Builder_Mode_ShowOtherHardware_Tooltip");
    }

    [Fact]
    public void LanguageChanged_RefreshesTheHardwareToggleText()
    {
        var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _mockLocalizationService.Raise(l => l.LanguageChanged += null, _mockLocalizationService.Object, EventArgs.Empty);

        changed.Should().Contain(nameof(BuilderModeBarViewModel.ShowOtherHardwareText));
        changed.Should().Contain(nameof(BuilderModeBarViewModel.ShowOtherHardwareTooltip));
    }

    private static readonly CatalogScope Narrowed25H2 =
        BuilderScope with { IncludeOtherHardware = true, TargetBuild = new WinBuild(26200, int.MaxValue) };

    [Fact]
    public void TargetWindowsText_NamesAKnownRelease()
    {
        var sut = CreateSut();

        sut.TargetWindowsText.Should().Be("Windows 11 26H2");
    }

    [Fact]
    public void TargetWindowsText_FallsBackToTheBuildForAnUnlistedRelease()
    {
        _mockTargetWindows.Setup(t => t.Build).Returns(28000);

        CreateSut().TargetWindowsText.Should().Be("Builder_Mode_TargetWindows_Build:28000");
    }

    [Fact]
    public void TargetWindowsText_SaysAllVersionsWithNoTarget()
    {
        _mockTargetWindows.Setup(t => t.Build).Returns((int?)null);

        CreateSut().TargetWindowsText.Should().Be("Builder_Mode_TargetWindows_AllVersions");
    }

    [Fact]
    public async Task SetTargetWindowsAsync_WithEveryEditOnTheNewTarget_SwitchesWithoutAsking()
    {
        EditsAre("on-both");
        _mockCatalogSettingsRegistry
            .Setup(r => r.GetById("on-both", Narrowed25H2))
            .Returns(new Setting { Id = "on-both", Display = new Display { Name = TestKeys.Of("on-both"), Description = TestKeys.Of("on-both") } });
        var sut = CreateSut();

        await sut.SetTargetWindowsAsync(26200);

        _mockDialogService.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Never);
        _mockTargetWindows.Verify(t => t.SetAsync(26200), Times.Once);
    }

    [Fact]
    public async Task SetTargetWindowsAsync_DroppingEdits_AsksNamingTheCountAndTheRelease()
    {
        EditsAre("only-on-26h2");
        ConfirmationRequest? shown = null;
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .Callback<ConfirmationRequest>(request => shown = request)
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });
        var sut = CreateSut();

        await sut.SetTargetWindowsAsync(26200);

        shown!.Title.Should().Be("Dialog_NarrowWindows_Title");
        shown.Message.Should().Be("Dialog_NarrowWindows_Message:1,Windows 11 25H2");
        _mockTargetWindows.Verify(t => t.SetAsync(It.IsAny<int?>()), Times.Never);
    }

    [Fact]
    public async Task SetTargetWindowsAsync_AllVersions_NeverAsks()
    {
        EditsAre("anything");
        var sut = CreateSut();

        await sut.SetTargetWindowsAsync(null);

        _mockDialogService.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Never);
        _mockTargetWindows.Verify(t => t.SetAsync(null), Times.Once);
    }

    [Fact]
    public void TargetChanged_RefreshesTheLabel()
    {
        var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _mockTargetWindows.Raise(t => t.Changed += null, _mockTargetWindows.Object, EventArgs.Empty);

        changed.Should().Contain(nameof(BuilderModeBarViewModel.TargetWindowsText));
    }

    [Fact]
    public async Task CancelCommand_AsksLeaveOrStay_AndLeaveReturnsToNormalMode()
    {
        ConfirmationRequest? shown = null;
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .Callback<ConfirmationRequest>(request => shown = request)
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });
        var sut = CreateSut();

        await sut.CancelCommand.ExecuteAsync(null);

        shown!.ConfirmButtonText.Should().Be("Button_Leave");
        shown.CancelButtonText.Should().Be("Button_Stay");
        _mockApplicationModeService.Verify(m => m.EnterNormalMode(), Times.Once);
    }

    [Fact]
    public async Task CancelCommand_Stay_KeepsBuilderMode()
    {
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });
        var sut = CreateSut();

        await sut.CancelCommand.ExecuteAsync(null);

        _mockApplicationModeService.Verify(m => m.EnterNormalMode(), Times.Never);
    }
}
