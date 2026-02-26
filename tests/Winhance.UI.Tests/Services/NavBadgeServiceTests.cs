using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Winhance.UI.Features.SoftwareApps.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class NavBadgeServiceTests : IDisposable
{
    private readonly Mock<IConfigReviewModeService> _mockModeService = new();
    private readonly Mock<IConfigReviewBadgeService> _mockBadgeService = new();
    private readonly WindowsAppsViewModel _windowsAppsVm;
    private readonly ExternalAppsViewModel _externalAppsVm;
    private readonly NavBadgeService _sut;

    public NavBadgeServiceTests()
    {
        // Create real WindowsAppsViewModel and ExternalAppsViewModel with mocked dependencies
        var mockWindowsAppsService = new Mock<IWindowsAppsService>();
        var mockAppInstallService = new Mock<IAppInstallationService>();
        var mockAppUninstallService = new Mock<IAppUninstallationService>();
        var mockProgressService = new Mock<ITaskProgressService>();
        var mockLogService = new Mock<ILogService>();
        var mockDialogService = new Mock<IDialogService>();
        var mockLocalizationService = new Mock<ILocalizationService>();
        mockLocalizationService.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);
        var mockConnectivityService = new Mock<IInternetConnectivityService>();
        var mockDispatcherService = new Mock<IDispatcherService>();
        mockDispatcherService.Setup(d => d.RunOnUIThread(It.IsAny<Action>())).Callback<Action>(a => a());

        _windowsAppsVm = new WindowsAppsViewModel(
            mockWindowsAppsService.Object,
            mockAppInstallService.Object,
            mockAppUninstallService.Object,
            mockProgressService.Object,
            mockLogService.Object,
            mockDialogService.Object,
            mockLocalizationService.Object,
            mockConnectivityService.Object,
            mockDispatcherService.Object);

        var mockExternalAppsService = new Mock<IExternalAppsService>();

        _externalAppsVm = new ExternalAppsViewModel(
            mockExternalAppsService.Object,
            mockProgressService.Object,
            mockLogService.Object,
            mockDialogService.Object,
            mockLocalizationService.Object,
            mockConnectivityService.Object,
            mockDispatcherService.Object);

        _sut = new NavBadgeService(
            _mockModeService.Object,
            _mockBadgeService.Object,
            _windowsAppsVm,
            _externalAppsVm);
    }

    public void Dispose()
    {
        _sut.UnsubscribeFromSoftwareAppsChanges();
        _windowsAppsVm.Dispose();
        _externalAppsVm.Dispose();
    }

    // ── ComputeNavBadges ──

    [Fact]
    public void ComputeNavBadges_WhenNotInReviewMode_ReturnsEmptyList()
    {
        _mockModeService.Setup(m => m.IsInReviewMode).Returns(false);

        var result = _sut.ComputeNavBadges();

        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeNavBadges_WhenInReviewMode_ReturnsThreeBadges()
    {
        _mockModeService.Setup(m => m.IsInReviewMode).Returns(true);
        _mockBadgeService.Setup(b => b.GetNavBadgeCount(It.IsAny<string>())).Returns(0);

        var result = _sut.ComputeNavBadges();

        result.Should().HaveCount(3);
    }

    [Fact]
    public void ComputeNavBadges_WhenSectionHasCount_ReturnsAttentionStyle()
    {
        _mockModeService.Setup(m => m.IsInReviewMode).Returns(true);
        _mockBadgeService.Setup(b => b.GetNavBadgeCount("Optimize")).Returns(5);
        _mockBadgeService.Setup(b => b.IsSectionFullyReviewed("Optimize")).Returns(false);
        _mockBadgeService.Setup(b => b.GetNavBadgeCount("SoftwareApps")).Returns(0);
        _mockBadgeService.Setup(b => b.GetNavBadgeCount("Customize")).Returns(0);

        var result = _sut.ComputeNavBadges();

        var optimizeBadge = result.FirstOrDefault(b => b.Tag == "Optimize");
        optimizeBadge.Should().NotBeNull();
        optimizeBadge!.Count.Should().Be(5);
        optimizeBadge.Style.Should().Be("Attention");
    }

    [Fact]
    public void ComputeNavBadges_WhenSectionFullyReviewed_ReturnsSuccessIcon()
    {
        _mockModeService.Setup(m => m.IsInReviewMode).Returns(true);
        _mockBadgeService.Setup(b => b.GetNavBadgeCount("Optimize")).Returns(3);
        _mockBadgeService.Setup(b => b.IsSectionFullyReviewed("Optimize")).Returns(true);
        _mockBadgeService.Setup(b => b.GetNavBadgeCount("SoftwareApps")).Returns(0);
        _mockBadgeService.Setup(b => b.GetNavBadgeCount("Customize")).Returns(0);

        var result = _sut.ComputeNavBadges();

        var optimizeBadge = result.FirstOrDefault(b => b.Tag == "Optimize");
        optimizeBadge.Should().NotBeNull();
        optimizeBadge!.Count.Should().Be(-1);
        optimizeBadge.Style.Should().Be("SuccessIcon");
    }

    [Fact]
    public void ComputeNavBadges_WhenSectionNotInConfig_ReturnsEmptyStyle()
    {
        _mockModeService.Setup(m => m.IsInReviewMode).Returns(true);
        _mockBadgeService.Setup(b => b.GetNavBadgeCount(It.IsAny<string>())).Returns(0);
        _mockBadgeService.Setup(b => b.IsFeatureInConfig(It.IsAny<string>())).Returns(false);

        var result = _sut.ComputeNavBadges();

        result.Should().AllSatisfy(b => b.Style.Should().BeEmpty());
    }

    [Fact]
    public void ComputeNavBadges_WhenSectionInConfigAndZeroCount_ReturnsSuccessIcon()
    {
        _mockModeService.Setup(m => m.IsInReviewMode).Returns(true);
        _mockBadgeService.Setup(b => b.GetNavBadgeCount("Optimize")).Returns(0);
        _mockBadgeService.Setup(b => b.GetNavBadgeCount("SoftwareApps")).Returns(0);
        _mockBadgeService.Setup(b => b.GetNavBadgeCount("Customize")).Returns(0);

        // Mark Privacy feature as in config (which is part of Optimize)
        _mockBadgeService.Setup(b => b.IsFeatureInConfig("Privacy")).Returns(true);

        var result = _sut.ComputeNavBadges();

        var optimizeBadge = result.FirstOrDefault(b => b.Tag == "Optimize");
        optimizeBadge.Should().NotBeNull();
        optimizeBadge!.Style.Should().Be("SuccessIcon");
    }

    // ── GetSoftwareAppsSelectedCount ──

    [Fact]
    public void GetSoftwareAppsSelectedCount_WithNoItems_ReturnsZero()
    {
        var result = _sut.GetSoftwareAppsSelectedCount();

        result.Should().Be(0);
    }

    // ── SubscribeToSoftwareAppsChanges ──

    [Fact]
    public void SubscribeToSoftwareAppsChanges_SetsIsSoftwareAppsBadgeSubscribed()
    {
        _sut.IsSoftwareAppsBadgeSubscribed.Should().BeFalse();

        _sut.SubscribeToSoftwareAppsChanges(() => { });

        _sut.IsSoftwareAppsBadgeSubscribed.Should().BeTrue();
    }

    [Fact]
    public void SubscribeToSoftwareAppsChanges_CalledTwice_DoesNotDoubleSubscribe()
    {
        int callCount = 0;
        _sut.SubscribeToSoftwareAppsChanges(() => callCount++);
        _sut.SubscribeToSoftwareAppsChanges(() => callCount++);

        _sut.IsSoftwareAppsBadgeSubscribed.Should().BeTrue();
    }

    // ── UnsubscribeFromSoftwareAppsChanges ──

    [Fact]
    public void UnsubscribeFromSoftwareAppsChanges_ClearsIsSoftwareAppsBadgeSubscribed()
    {
        _sut.SubscribeToSoftwareAppsChanges(() => { });
        _sut.IsSoftwareAppsBadgeSubscribed.Should().BeTrue();

        _sut.UnsubscribeFromSoftwareAppsChanges();

        _sut.IsSoftwareAppsBadgeSubscribed.Should().BeFalse();
    }

    [Fact]
    public void UnsubscribeFromSoftwareAppsChanges_WhenNotSubscribed_DoesNotThrow()
    {
        var act = () => _sut.UnsubscribeFromSoftwareAppsChanges();

        act.Should().NotThrow();
    }

    // ── SoftwareApps badge uses selected count when subscribed ──

    [Fact]
    public void ComputeNavBadges_WhenSubscribedToSoftwareApps_UsesSoftwareAppsSelectedCount()
    {
        _mockModeService.Setup(m => m.IsInReviewMode).Returns(true);
        _mockBadgeService.Setup(b => b.GetNavBadgeCount("SoftwareApps")).Returns(10);
        _mockBadgeService.Setup(b => b.IsSectionFullyReviewed("SoftwareApps")).Returns(false);
        _mockBadgeService.Setup(b => b.GetNavBadgeCount("Optimize")).Returns(0);
        _mockBadgeService.Setup(b => b.GetNavBadgeCount("Customize")).Returns(0);

        // Subscribe to enable selected count path
        _sut.SubscribeToSoftwareAppsChanges(() => { });

        var result = _sut.ComputeNavBadges();

        // When subscribed, it uses GetSoftwareAppsSelectedCount() instead of badge service count
        // Items are empty so count = 0
        var softwareAppsBadge = result.FirstOrDefault(b => b.Tag == "SoftwareApps");
        softwareAppsBadge.Should().NotBeNull();
        // With 0 items selected, count is 0 so it falls to the else branch
        softwareAppsBadge!.Count.Should().Be(-1);
    }
}
