using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class BuilderModeBarViewModelTests : IDisposable
{
    private readonly Mock<IApplicationModeService> _mockApplicationModeService = new();
    private readonly Mock<IBuilderSaveService> _mockBuilderSaveService = new();
    private readonly Mock<IDispatcherService> _mockDispatcherService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<IHardwareFilterService> _mockHardwareFilterService = new();
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

        _mockHardwareFilterService.Setup(f => f.IsFilterEnabled).Returns(true);
        _mockHardwareFilterService.Setup(f => f.SetAsync(It.IsAny<bool>())).Returns(Task.CompletedTask);
    }

    private BuilderModeBarViewModel CreateSut() => _sut = new(
        _mockApplicationModeService.Object,
        _mockBuilderSaveService.Object,
        _mockDispatcherService.Object,
        _mockLocalizationService.Object,
        _mockDialogService.Object,
        _mockHardwareFilterService.Object,
        _mockLogService.Object);

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
}
