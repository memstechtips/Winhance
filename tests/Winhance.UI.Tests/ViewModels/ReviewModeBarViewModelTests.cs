using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class ReviewModeBarViewModelTests : IDisposable
{
    private readonly Mock<IConfigReviewModeService> _mockConfigReviewModeService = new();
    private readonly Mock<IConfigReviewDiffService> _mockConfigReviewDiffService = new();
    private readonly Mock<IConfigReviewBadgeService> _mockConfigReviewBadgeService = new();
    private readonly Mock<IConfigurationService> _mockConfigurationService = new();
    private readonly Mock<IDispatcherService> _mockDispatcherService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<ILogService> _mockLogService = new();

    private readonly ReviewModeBarViewModel _sut;

    public ReviewModeBarViewModelTests()
    {
        // Set up dispatcher to execute actions synchronously
        _mockDispatcherService
            .Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(a => a());
        _mockDispatcherService
            .Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(f => f());

        // Default localization returns null so fallbacks are used
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => null!);

        _sut = new ReviewModeBarViewModel(
            _mockConfigReviewModeService.Object,
            _mockConfigReviewDiffService.Object,
            _mockConfigReviewBadgeService.Object,
            _mockConfigurationService.Object,
            _mockDispatcherService.Object,
            _mockLocalizationService.Object,
            _mockDialogService.Object,
            _mockLogService.Object);
    }

    public void Dispose()
    {
        _sut.Dispose();
    }

    // ── Constructor ──

    [Fact]
    public void Constructor_InitializesDefaultProperties()
    {
        _sut.IsInReviewMode.Should().BeFalse();
        _sut.ReviewModeStatusText.Should().BeEmpty();
        _sut.CanApplyReviewedConfig.Should().BeFalse();
    }

    // ── Localized Strings with Fallbacks ──

    [Fact]
    public void ReviewModeTitleText_ReturnsFallbackWhenLocalizationReturnsNull()
    {
        _sut.ReviewModeTitleText.Should().Be("Config Review Mode");
    }

    [Fact]
    public void ReviewModeApplyButtonText_ReturnsFallbackWhenLocalizationReturnsNull()
    {
        _sut.ReviewModeApplyButtonText.Should().Be("Apply Config");
    }

    [Fact]
    public void ReviewModeCancelButtonText_ReturnsFallbackWhenLocalizationReturnsNull()
    {
        _sut.ReviewModeCancelButtonText.Should().Be("Cancel");
    }

    [Fact]
    public void ReviewModeDescriptionText_ReturnsFallbackWhenLocalizationReturnsNull()
    {
        _sut.ReviewModeDescriptionText.Should().Contain("Review the changes below");
    }

    // ── Review Mode Changed Event ──

    [Fact]
    public void ReviewModeChanged_EnterReviewMode_UpdatesIsInReviewMode()
    {
        _mockConfigReviewModeService
            .Setup(s => s.IsInReviewMode)
            .Returns(true);

        _mockConfigReviewModeService.Raise(s => s.ReviewModeChanged += null, this, EventArgs.Empty);

        _sut.IsInReviewMode.Should().BeTrue();
    }

    [Fact]
    public void ReviewModeChanged_ExitReviewMode_UpdatesIsInReviewMode()
    {
        // First enter review mode
        _mockConfigReviewModeService
            .Setup(s => s.IsInReviewMode)
            .Returns(true);
        _mockConfigReviewModeService.Raise(s => s.ReviewModeChanged += null, this, EventArgs.Empty);
        _sut.IsInReviewMode.Should().BeTrue();

        // Now exit
        _mockConfigReviewModeService
            .Setup(s => s.IsInReviewMode)
            .Returns(false);
        _mockConfigReviewModeService.Raise(s => s.ReviewModeChanged += null, this, EventArgs.Empty);

        _sut.IsInReviewMode.Should().BeFalse();
    }

    [Fact]
    public void ReviewModeChanged_ExitReviewMode_ClearsStatusText()
    {
        _mockConfigReviewModeService
            .Setup(s => s.IsInReviewMode)
            .Returns(false);

        _mockConfigReviewModeService.Raise(s => s.ReviewModeChanged += null, this, EventArgs.Empty);

        _sut.ReviewModeStatusText.Should().BeEmpty();
    }

    // ── Status Text ──

    [Fact]
    public void StatusText_WhenInReviewModeWithChanges_ShowsReviewedCount()
    {
        _mockConfigReviewModeService
            .Setup(s => s.IsInReviewMode)
            .Returns(true);
        _mockConfigReviewDiffService
            .Setup(d => d.TotalChanges)
            .Returns(5);
        _mockConfigReviewDiffService
            .Setup(d => d.ReviewedChanges)
            .Returns(2);
        _mockConfigReviewDiffService
            .Setup(d => d.ApprovedChanges)
            .Returns(1);

        _mockConfigReviewModeService.Raise(s => s.ReviewModeChanged += null, this, EventArgs.Empty);

        _sut.ReviewModeStatusText.Should().Contain("2");
        _sut.ReviewModeStatusText.Should().Contain("5");
        _sut.ReviewModeStatusText.Should().Contain("1");
    }

    [Fact]
    public void StatusText_WhenInReviewModeNoChanges_WithConfigItems_ShowsAllMatch()
    {
        _mockConfigReviewModeService
            .Setup(s => s.IsInReviewMode)
            .Returns(true);
        _mockConfigReviewDiffService
            .Setup(d => d.TotalChanges)
            .Returns(0);
        _mockConfigReviewDiffService
            .Setup(d => d.TotalConfigItems)
            .Returns(10);

        _mockConfigReviewModeService.Raise(s => s.ReviewModeChanged += null, this, EventArgs.Empty);

        _sut.ReviewModeStatusText.Should().Contain("match");
    }

    [Fact]
    public void StatusText_WhenInReviewModeNoChanges_NoConfigItems_ShowsNoItems()
    {
        _mockConfigReviewModeService
            .Setup(s => s.IsInReviewMode)
            .Returns(true);
        _mockConfigReviewDiffService
            .Setup(d => d.TotalChanges)
            .Returns(0);
        _mockConfigReviewDiffService
            .Setup(d => d.TotalConfigItems)
            .Returns(0);

        _mockConfigReviewModeService.Raise(s => s.ReviewModeChanged += null, this, EventArgs.Empty);

        _sut.ReviewModeStatusText.Should().Contain("No configuration items");
    }

    // ── Approval Count Changed Event ──

    [Fact]
    public void ApprovalCountChanged_UpdatesStatusTextAndCanApply()
    {
        // Enter review mode first
        _mockConfigReviewModeService
            .Setup(s => s.IsInReviewMode)
            .Returns(true);
        _mockConfigReviewModeService.Raise(s => s.ReviewModeChanged += null, this, EventArgs.Empty);

        // Update diff counts
        _mockConfigReviewDiffService
            .Setup(d => d.TotalChanges)
            .Returns(3);
        _mockConfigReviewDiffService
            .Setup(d => d.ReviewedChanges)
            .Returns(3);
        _mockConfigReviewDiffService
            .Setup(d => d.ApprovedChanges)
            .Returns(2);

        // Mock badge service for full review
        _mockConfigReviewBadgeService
            .Setup(b => b.IsSoftwareAppsReviewed)
            .Returns(true);
        _mockConfigReviewBadgeService
            .Setup(b => b.IsSectionFullyReviewed("Optimize"))
            .Returns(true);
        _mockConfigReviewBadgeService
            .Setup(b => b.IsSectionFullyReviewed("Customize"))
            .Returns(true);

        _mockConfigReviewDiffService.Raise(d => d.ApprovalCountChanged += null, this, EventArgs.Empty);

        _sut.ReviewModeStatusText.Should().Contain("3");
    }

    // ── CanApplyReviewedConfig ──

    [Fact]
    public void CanApplyReviewedConfig_NotInReviewMode_ReturnsFalse()
    {
        _sut.IsInReviewMode = false;

        _mockConfigReviewDiffService.Raise(d => d.ApprovalCountChanged += null, this, EventArgs.Empty);

        _sut.CanApplyReviewedConfig.Should().BeFalse();
    }

    [Fact]
    public void CanApplyReviewedConfig_AllReviewed_ReturnsTrue()
    {
        _mockConfigReviewModeService
            .Setup(s => s.IsInReviewMode)
            .Returns(true);
        _mockConfigReviewModeService.Raise(s => s.ReviewModeChanged += null, this, EventArgs.Empty);

        _mockConfigReviewDiffService
            .Setup(d => d.TotalChanges)
            .Returns(3);
        _mockConfigReviewDiffService
            .Setup(d => d.ReviewedChanges)
            .Returns(3);

        _mockConfigReviewBadgeService
            .Setup(b => b.IsSoftwareAppsReviewed)
            .Returns(true);
        _mockConfigReviewBadgeService
            .Setup(b => b.IsSectionFullyReviewed("Optimize"))
            .Returns(true);
        _mockConfigReviewBadgeService
            .Setup(b => b.IsSectionFullyReviewed("Customize"))
            .Returns(true);

        _mockConfigReviewDiffService.Raise(d => d.ApprovalCountChanged += null, this, EventArgs.Empty);

        _sut.CanApplyReviewedConfig.Should().BeTrue();
    }

    [Fact]
    public void CanApplyReviewedConfig_NotAllReviewed_ReturnsFalse()
    {
        _mockConfigReviewModeService
            .Setup(s => s.IsInReviewMode)
            .Returns(true);
        _mockConfigReviewModeService.Raise(s => s.ReviewModeChanged += null, this, EventArgs.Empty);

        _mockConfigReviewDiffService
            .Setup(d => d.TotalChanges)
            .Returns(5);
        _mockConfigReviewDiffService
            .Setup(d => d.ReviewedChanges)
            .Returns(2);

        _mockConfigReviewBadgeService
            .Setup(b => b.IsSoftwareAppsReviewed)
            .Returns(true);
        _mockConfigReviewBadgeService
            .Setup(b => b.IsSectionFullyReviewed("Optimize"))
            .Returns(true);
        _mockConfigReviewBadgeService
            .Setup(b => b.IsSectionFullyReviewed("Customize"))
            .Returns(true);

        _mockConfigReviewDiffService.Raise(d => d.ApprovalCountChanged += null, this, EventArgs.Empty);

        _sut.CanApplyReviewedConfig.Should().BeFalse();
    }

    [Fact]
    public void CanApplyReviewedConfig_ZeroTotalChanges_ReturnsTrue_WhenAllSectionsReviewed()
    {
        _mockConfigReviewModeService
            .Setup(s => s.IsInReviewMode)
            .Returns(true);
        _mockConfigReviewModeService.Raise(s => s.ReviewModeChanged += null, this, EventArgs.Empty);

        _mockConfigReviewDiffService
            .Setup(d => d.TotalChanges)
            .Returns(0);

        _mockConfigReviewBadgeService
            .Setup(b => b.IsSoftwareAppsReviewed)
            .Returns(true);
        _mockConfigReviewBadgeService
            .Setup(b => b.IsSectionFullyReviewed("Optimize"))
            .Returns(true);
        _mockConfigReviewBadgeService
            .Setup(b => b.IsSectionFullyReviewed("Customize"))
            .Returns(true);

        _mockConfigReviewDiffService.Raise(d => d.ApprovalCountChanged += null, this, EventArgs.Empty);

        _sut.CanApplyReviewedConfig.Should().BeTrue();
    }

    // ── ApplyReviewedConfigCommand ──

    [Fact]
    public async Task ApplyReviewedConfigCommand_DelegatesToConfigurationService()
    {
        _mockConfigurationService
            .Setup(c => c.ApplyReviewedConfigAsync())
            .Returns(Task.CompletedTask);

        await _sut.ApplyReviewedConfigCommand.ExecuteAsync(null);

        _mockConfigurationService.Verify(c => c.ApplyReviewedConfigAsync(), Times.Once);
    }

    [Fact]
    public async Task ApplyReviewedConfigCommand_ServiceThrows_DoesNotRethrow()
    {
        _mockConfigurationService
            .Setup(c => c.ApplyReviewedConfigAsync())
            .ThrowsAsync(new Exception("Apply failed"));

        var act = async () => await _sut.ApplyReviewedConfigCommand.ExecuteAsync(null);

        await act.Should().NotThrowAsync();
    }

    // ── CancelReviewModeCommand ──

    [Fact]
    public async Task CancelReviewModeCommand_UserConfirms_CallsCancelReviewMode()
    {
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        _mockConfigurationService
            .Setup(c => c.CancelReviewModeAsync())
            .Returns(Task.CompletedTask);

        await _sut.CancelReviewModeCommand.ExecuteAsync(null);

        _mockConfigurationService.Verify(c => c.CancelReviewModeAsync(), Times.Once);
    }

    [Fact]
    public async Task CancelReviewModeCommand_UserDeclines_DoesNotCallCancelReviewMode()
    {
        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        await _sut.CancelReviewModeCommand.ExecuteAsync(null);

        _mockConfigurationService.Verify(c => c.CancelReviewModeAsync(), Times.Never);
    }

    // ── Badge State Changed Event ──

    [Fact]
    public void BadgeStateChanged_UpdatesCanApplyReviewedConfig()
    {
        _mockConfigReviewModeService
            .Setup(s => s.IsInReviewMode)
            .Returns(true);
        _mockConfigReviewModeService.Raise(s => s.ReviewModeChanged += null, this, EventArgs.Empty);

        _mockConfigReviewDiffService
            .Setup(d => d.TotalChanges)
            .Returns(0);
        _mockConfigReviewBadgeService
            .Setup(b => b.IsSoftwareAppsReviewed)
            .Returns(true);
        _mockConfigReviewBadgeService
            .Setup(b => b.IsSectionFullyReviewed("Optimize"))
            .Returns(true);
        _mockConfigReviewBadgeService
            .Setup(b => b.IsSectionFullyReviewed("Customize"))
            .Returns(true);

        _mockConfigReviewBadgeService.Raise(b => b.BadgeStateChanged += null, this, EventArgs.Empty);

        _sut.CanApplyReviewedConfig.Should().BeTrue();
    }

    // ── Language Change ──

    [Fact]
    public void LanguageChanged_WhenInReviewMode_NotifiesLocalizedProperties()
    {
        _sut.IsInReviewMode = true;

        var changedProperties = new List<string>();
        _sut.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        _mockLocalizationService.Raise(l => l.LanguageChanged += null, this, EventArgs.Empty);

        changedProperties.Should().Contain(nameof(_sut.ReviewModeTitleText));
        changedProperties.Should().Contain(nameof(_sut.ReviewModeDescriptionText));
        changedProperties.Should().Contain(nameof(_sut.ReviewModeApplyButtonText));
        changedProperties.Should().Contain(nameof(_sut.ReviewModeCancelButtonText));
    }

    [Fact]
    public void LanguageChanged_WhenNotInReviewMode_DoesNotNotifyLocalizedProperties()
    {
        _sut.IsInReviewMode = false;

        var changedProperties = new List<string>();
        _sut.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        _mockLocalizationService.Raise(l => l.LanguageChanged += null, this, EventArgs.Empty);

        changedProperties.Should().NotContain(nameof(_sut.ReviewModeTitleText));
    }

    // ── IDisposable ──

    [Fact]
    public void Dispose_UnsubscribesFromEvents()
    {
        var sut = new ReviewModeBarViewModel(
            _mockConfigReviewModeService.Object,
            _mockConfigReviewDiffService.Object,
            _mockConfigReviewBadgeService.Object,
            _mockConfigurationService.Object,
            _mockDispatcherService.Object,
            _mockLocalizationService.Object,
            _mockDialogService.Object,
            _mockLogService.Object);

        sut.Dispose();

        // After dispose, raising review mode changed should not update IsInReviewMode
        _mockConfigReviewModeService
            .Setup(s => s.IsInReviewMode)
            .Returns(true);
        _mockConfigReviewModeService.Raise(s => s.ReviewModeChanged += null, this, EventArgs.Empty);

        sut.IsInReviewMode.Should().BeFalse();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var sut = new ReviewModeBarViewModel(
            _mockConfigReviewModeService.Object,
            _mockConfigReviewDiffService.Object,
            _mockConfigReviewBadgeService.Object,
            _mockConfigurationService.Object,
            _mockDispatcherService.Object,
            _mockLocalizationService.Object,
            _mockDialogService.Object,
            _mockLogService.Object);

        var act = () =>
        {
            sut.Dispose();
            sut.Dispose();
        };

        act.Should().NotThrow();
    }
}
