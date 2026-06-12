using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class WindowsVersionFilterServiceTests
{
    private readonly Mock<IUserPreferencesService> _mockPreferencesService = new();
    private readonly Mock<ICompatibleSettingsRegistry> _mockCompatibleSettingsRegistry = new();
    private readonly Mock<IEventBus> _mockEventBus = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<ILogService> _mockLogService = new();

    private WindowsVersionFilterService CreateService()
    {
        return new WindowsVersionFilterService(
            _mockPreferencesService.Object,
            _mockCompatibleSettingsRegistry.Object,
            _mockEventBus.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockLogService.Object);
    }

    // -------------------------------------------------------
    // IsFilterEnabled default
    // -------------------------------------------------------

    [Fact]
    public void IsFilterEnabled_DefaultsToTrue()
    {
        var service = CreateService();

        service.IsFilterEnabled.Should().BeTrue();
    }

    // -------------------------------------------------------
    // LoadFilterPreferenceAsync
    // -------------------------------------------------------

    [Fact]
    public async Task LoadFilterPreferenceAsync_LoadsPreferenceFromStore()
    {
        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.EnableWindowsVersionFilter, true))
            .ReturnsAsync(false);

        var service = CreateService();

        await service.LoadFilterPreferenceAsync();

        service.IsFilterEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task LoadFilterPreferenceAsync_AppliesFilterToRegistry()
    {
        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.EnableWindowsVersionFilter, true))
            .ReturnsAsync(false);

        var service = CreateService();

        await service.LoadFilterPreferenceAsync();

        _mockCompatibleSettingsRegistry.Verify(
            r => r.SetFilterEnabled(false),
            Times.Once);
    }

    [Fact]
    public async Task LoadFilterPreferenceAsync_FiresFilterStateChangedEvent()
    {
        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.EnableWindowsVersionFilter, true))
            .ReturnsAsync(false);

        var service = CreateService();
        bool? receivedState = null;
        service.FilterStateChanged += (_, state) => receivedState = state;

        await service.LoadFilterPreferenceAsync();

        receivedState.Should().BeFalse();
    }

    [Fact]
    public async Task LoadFilterPreferenceAsync_WhenEnabled_LogsOnState()
    {
        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.EnableWindowsVersionFilter, true))
            .ReturnsAsync(true);

        var service = CreateService();

        await service.LoadFilterPreferenceAsync();

        _mockLogService.Verify(
            l => l.Log(
                Core.Features.Common.Enums.LogLevel.Info,
                It.Is<string>(s => s.Contains("ON"))),
            Times.Once);
    }

    [Fact]
    public async Task LoadFilterPreferenceAsync_WhenDisabled_LogsOffState()
    {
        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.EnableWindowsVersionFilter, true))
            .ReturnsAsync(false);

        var service = CreateService();

        await service.LoadFilterPreferenceAsync();

        _mockLogService.Verify(
            l => l.Log(
                Core.Features.Common.Enums.LogLevel.Info,
                It.Is<string>(s => s.Contains("OFF"))),
            Times.Once);
    }

    [Fact]
    public async Task LoadFilterPreferenceAsync_WhenPreferenceThrows_LogsError()
    {
        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.EnableWindowsVersionFilter, true))
            .ThrowsAsync(new Exception("Prefs unavailable"));

        var service = CreateService();

        await service.LoadFilterPreferenceAsync();

        _mockLogService.Verify(
            l => l.Log(
                Core.Features.Common.Enums.LogLevel.Error,
                It.Is<string>(s => s.Contains("Failed to load filter preference"))),
            Times.Once);
    }

    // -------------------------------------------------------
    // ToggleFilterAsync
    // -------------------------------------------------------

    [Fact]
    public async Task ToggleFilterAsync_WhenInReviewMode_ReturnsFalse()
    {
        var service = CreateService();

        var result = await service.ToggleFilterAsync(isInReviewMode: true);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleFilterAsync_WhenInReviewMode_DoesNotChangeFilter()
    {
        var service = CreateService();
        var originalState = service.IsFilterEnabled;

        await service.ToggleFilterAsync(isInReviewMode: true);

        service.IsFilterEnabled.Should().Be(originalState);
    }

    [Fact]
    public async Task ToggleFilterAsync_ShowsExplanationDialog_WhenDontShowAgainIsFalse()
    {
        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.DontShowFilterExplanation, false))
            .ReturnsAsync(false);

        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns("Localized text");

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true, CheckboxChecked = false }); // Confirmed, checkbox not checked

        _mockPreferencesService
            .Setup(p => p.SetPreferenceAsync(UserPreferenceKeys.EnableWindowsVersionFilter, It.IsAny<bool>()))
            .ReturnsAsync(OperationResult.Succeeded());

        var service = CreateService();

        await service.ToggleFilterAsync(isInReviewMode: false);

        _mockDialogService.Verify(
            d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()),
            Times.Once);
    }

    [Fact]
    public async Task ToggleFilterAsync_SkipsExplanationDialog_WhenDontShowAgainIsTrue()
    {
        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.DontShowFilterExplanation, false))
            .ReturnsAsync(true); // Don't show again

        _mockPreferencesService
            .Setup(p => p.SetPreferenceAsync(UserPreferenceKeys.EnableWindowsVersionFilter, It.IsAny<bool>()))
            .ReturnsAsync(OperationResult.Succeeded());

        var service = CreateService();

        await service.ToggleFilterAsync(isInReviewMode: false);

        _mockDialogService.Verify(
            d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()),
            Times.Never);
    }

    [Fact]
    public async Task ToggleFilterAsync_WhenUserCancelsDialog_ReturnsFalseAndDoesNotToggle()
    {
        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.DontShowFilterExplanation, false))
            .ReturnsAsync(false);

        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns("Localized text");

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false, CheckboxChecked = false }); // Cancelled

        var service = CreateService();
        var originalState = service.IsFilterEnabled;

        var result = await service.ToggleFilterAsync(isInReviewMode: false);

        result.Should().BeFalse();
        service.IsFilterEnabled.Should().Be(originalState);
    }

    [Fact]
    public async Task ToggleFilterAsync_WhenCheckboxChecked_SavesDontShowPreference()
    {
        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.DontShowFilterExplanation, false))
            .ReturnsAsync(false);

        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns("Localized text");

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true, CheckboxChecked = true }); // Confirmed and checkbox checked

        _mockPreferencesService
            .Setup(p => p.SetPreferenceAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(OperationResult.Succeeded());

        var service = CreateService();

        await service.ToggleFilterAsync(isInReviewMode: false);

        _mockPreferencesService.Verify(
            p => p.SetPreferenceAsync(UserPreferenceKeys.DontShowFilterExplanation, true),
            Times.Once);
    }

    [Fact]
    public async Task ToggleFilterAsync_WhenCheckboxNotChecked_DoesNotSaveDontShowPreference()
    {
        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.DontShowFilterExplanation, false))
            .ReturnsAsync(false);

        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns("Localized text");

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true, CheckboxChecked = false }); // Confirmed but checkbox not checked

        _mockPreferencesService
            .Setup(p => p.SetPreferenceAsync(UserPreferenceKeys.EnableWindowsVersionFilter, It.IsAny<bool>()))
            .ReturnsAsync(OperationResult.Succeeded());

        var service = CreateService();

        await service.ToggleFilterAsync(isInReviewMode: false);

        _mockPreferencesService.Verify(
            p => p.SetPreferenceAsync(UserPreferenceKeys.DontShowFilterExplanation, It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task ToggleFilterAsync_WhenConfirmed_TogglesFilterState()
    {
        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.DontShowFilterExplanation, false))
            .ReturnsAsync(true); // Skip dialog

        _mockPreferencesService
            .Setup(p => p.SetPreferenceAsync(UserPreferenceKeys.EnableWindowsVersionFilter, It.IsAny<bool>()))
            .ReturnsAsync(OperationResult.Succeeded());

        var service = CreateService();
        service.IsFilterEnabled.Should().BeTrue(); // Default is true

        var result = await service.ToggleFilterAsync(isInReviewMode: false);

        result.Should().BeTrue();
        service.IsFilterEnabled.Should().BeFalse(); // Toggled to false
    }

    [Fact]
    public async Task ToggleFilterAsync_PersistsNewState()
    {
        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.DontShowFilterExplanation, false))
            .ReturnsAsync(true);

        _mockPreferencesService
            .Setup(p => p.SetPreferenceAsync(UserPreferenceKeys.EnableWindowsVersionFilter, It.IsAny<bool>()))
            .ReturnsAsync(OperationResult.Succeeded());

        var service = CreateService();

        await service.ToggleFilterAsync(isInReviewMode: false);

        _mockPreferencesService.Verify(
            p => p.SetPreferenceAsync(UserPreferenceKeys.EnableWindowsVersionFilter, false),
            Times.Once);
    }

    [Fact]
    public async Task ToggleFilterAsync_UpdatesCompatibleSettingsRegistry()
    {
        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.DontShowFilterExplanation, false))
            .ReturnsAsync(true);

        _mockPreferencesService
            .Setup(p => p.SetPreferenceAsync(UserPreferenceKeys.EnableWindowsVersionFilter, It.IsAny<bool>()))
            .ReturnsAsync(OperationResult.Succeeded());

        var service = CreateService();

        await service.ToggleFilterAsync(isInReviewMode: false);

        _mockCompatibleSettingsRegistry.Verify(
            r => r.SetFilterEnabled(false),
            Times.Once);
    }

    [Fact]
    public async Task ToggleFilterAsync_PublishesFilterStateChangedEvent()
    {
        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.DontShowFilterExplanation, false))
            .ReturnsAsync(true);

        _mockPreferencesService
            .Setup(p => p.SetPreferenceAsync(UserPreferenceKeys.EnableWindowsVersionFilter, It.IsAny<bool>()))
            .ReturnsAsync(OperationResult.Succeeded());

        var service = CreateService();

        await service.ToggleFilterAsync(isInReviewMode: false);

        _mockEventBus.Verify(
            e => e.Publish(It.Is<FilterStateChangedEvent>(evt => evt.IsFilterEnabled == false)),
            Times.Once);
    }

    [Fact]
    public async Task ToggleFilterAsync_FiresFilterStateChangedClrEvent()
    {
        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.DontShowFilterExplanation, false))
            .ReturnsAsync(true);

        _mockPreferencesService
            .Setup(p => p.SetPreferenceAsync(UserPreferenceKeys.EnableWindowsVersionFilter, It.IsAny<bool>()))
            .ReturnsAsync(OperationResult.Succeeded());

        var service = CreateService();
        bool? receivedState = null;
        service.FilterStateChanged += (_, state) => receivedState = state;

        await service.ToggleFilterAsync(isInReviewMode: false);

        receivedState.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleFilterAsync_LogsNewState()
    {
        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.DontShowFilterExplanation, false))
            .ReturnsAsync(true);

        _mockPreferencesService
            .Setup(p => p.SetPreferenceAsync(UserPreferenceKeys.EnableWindowsVersionFilter, It.IsAny<bool>()))
            .ReturnsAsync(OperationResult.Succeeded());

        var service = CreateService();

        await service.ToggleFilterAsync(isInReviewMode: false);

        _mockLogService.Verify(
            l => l.Log(
                Core.Features.Common.Enums.LogLevel.Info,
                It.Is<string>(s => s.Contains("OFF"))),
            Times.Once);
    }

    [Fact]
    public async Task ToggleFilterAsync_WhenExceptionOccurs_ReturnsFalseAndLogsError()
    {
        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.DontShowFilterExplanation, false))
            .ThrowsAsync(new Exception("Prefs error"));

        var service = CreateService();

        var result = await service.ToggleFilterAsync(isInReviewMode: false);

        result.Should().BeFalse();
        _mockLogService.Verify(
            l => l.Log(
                Core.Features.Common.Enums.LogLevel.Error,
                It.Is<string>(s => s.Contains("Failed to toggle"))),
            Times.Once);
    }

    [Fact]
    public async Task ToggleFilterAsync_UsesLocalizationKeys()
    {
        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.DontShowFilterExplanation, false))
            .ReturnsAsync(false);

        _mockLocalizationService
            .Setup(l => l.GetString("Filter_Dialog_Message"))
            .Returns("Custom message");
        _mockLocalizationService
            .Setup(l => l.GetString("Filter_Dialog_Checkbox"))
            .Returns("Custom checkbox");
        _mockLocalizationService
            .Setup(l => l.GetString("Filter_Dialog_Title"))
            .Returns("Custom title");
        _mockLocalizationService
            .Setup(l => l.GetString("Filter_Dialog_Button_Toggle"))
            .Returns("Custom toggle");
        _mockLocalizationService
            .Setup(l => l.GetString("Button_Cancel"))
            .Returns("Custom cancel");

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.Is<ConfirmationRequest>(r =>
                r.Message == "Custom message" &&
                r.CheckboxText == "Custom checkbox" &&
                r.Title == "Custom title" &&
                r.ConfirmButtonText == "Custom toggle" &&
                r.CancelButtonText == "Custom cancel")))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false, CheckboxChecked = false });

        var service = CreateService();

        await service.ToggleFilterAsync(isInReviewMode: false);

        _mockDialogService.Verify(
            d => d.ShowConfirmationAsync(It.Is<ConfirmationRequest>(r =>
                r.Message == "Custom message" &&
                r.CheckboxText == "Custom checkbox" &&
                r.Title == "Custom title" &&
                r.ConfirmButtonText == "Custom toggle" &&
                r.CancelButtonText == "Custom cancel")),
            Times.Once);
    }

    [Fact]
    public async Task ToggleFilterAsync_WhenLocalizationReturnsNull_UsesFallbackStrings()
    {
        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.DontShowFilterExplanation, false))
            .ReturnsAsync(false);

        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string?)null);

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false, CheckboxChecked = false });

        var service = CreateService();

        // Should not throw; fallback strings are used
        await service.ToggleFilterAsync(isInReviewMode: false);

        _mockDialogService.Verify(
            d => d.ShowConfirmationAsync(It.Is<ConfirmationRequest>(r =>
                r.Message.Contains("Windows Version Filter") &&
                r.CheckboxText!.Contains("Don't show this message again") &&
                r.Title == "Windows Version Filter" &&
                r.ConfirmButtonText == "Toggle Filter" &&
                r.CancelButtonText == "Cancel")),
            Times.Once);
    }

    // -------------------------------------------------------
    // ForceFilterOn
    // -------------------------------------------------------

    [Fact]
    public async Task ForceFilterOn_WhenFilterAlreadyEnabled_DoesNothing()
    {
        var service = CreateService();
        service.IsFilterEnabled.Should().BeTrue(); // Already true

        bool eventFired = false;
        service.FilterStateChanged += (_, _) => eventFired = true;

        service.ForceFilterOn();

        eventFired.Should().BeFalse();
        _mockCompatibleSettingsRegistry.Verify(r => r.SetFilterEnabled(It.IsAny<bool>()), Times.Never);
        _mockEventBus.Verify(e => e.Publish(It.IsAny<FilterStateChangedEvent>()), Times.Never);
    }

    [Fact]
    public async Task ForceFilterOn_WhenFilterDisabled_EnablesFilter()
    {
        // First disable the filter via toggle
        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.DontShowFilterExplanation, false))
            .ReturnsAsync(true);
        _mockPreferencesService
            .Setup(p => p.SetPreferenceAsync(UserPreferenceKeys.EnableWindowsVersionFilter, It.IsAny<bool>()))
            .ReturnsAsync(OperationResult.Succeeded());

        var service = CreateService();
        await service.ToggleFilterAsync(isInReviewMode: false); // Now false
        service.IsFilterEnabled.Should().BeFalse();

        // Reset mock call tracking
        _mockCompatibleSettingsRegistry.Invocations.Clear();
        _mockEventBus.Invocations.Clear();

        service.ForceFilterOn();

        service.IsFilterEnabled.Should().BeTrue();
        _mockCompatibleSettingsRegistry.Verify(r => r.SetFilterEnabled(true), Times.Once);
        _mockEventBus.Verify(
            e => e.Publish(It.Is<FilterStateChangedEvent>(evt => evt.IsFilterEnabled == true)),
            Times.Once);
    }

    [Fact]
    public async Task ForceFilterOn_WhenFilterDisabled_FiresFilterStateChangedEvent()
    {
        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.DontShowFilterExplanation, false))
            .ReturnsAsync(true);
        _mockPreferencesService
            .Setup(p => p.SetPreferenceAsync(UserPreferenceKeys.EnableWindowsVersionFilter, It.IsAny<bool>()))
            .ReturnsAsync(OperationResult.Succeeded());

        var service = CreateService();
        await service.ToggleFilterAsync(isInReviewMode: false); // Now false

        bool? receivedState = null;
        service.FilterStateChanged += (_, state) => receivedState = state;

        service.ForceFilterOn();

        receivedState.Should().BeTrue();
    }

    // -------------------------------------------------------
    // RestoreFilterPreferenceAsync
    // -------------------------------------------------------

    [Fact]
    public async Task RestoreFilterPreferenceAsync_WhenSavedPreferenceDiffers_RestoresIt()
    {
        // Service starts with IsFilterEnabled = true
        var service = CreateService();

        // ForceFilterOn was called (no change since already true), but saved pref is false
        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.EnableWindowsVersionFilter, true))
            .ReturnsAsync(false);

        // First toggle to false so we can test restore back to the saved pref
        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.DontShowFilterExplanation, false))
            .ReturnsAsync(true);
        _mockPreferencesService
            .Setup(p => p.SetPreferenceAsync(UserPreferenceKeys.EnableWindowsVersionFilter, false))
            .ReturnsAsync(OperationResult.Succeeded());
        await service.ToggleFilterAsync(isInReviewMode: false); // Now false

        // Force back on
        service.ForceFilterOn(); // Now true
        service.IsFilterEnabled.Should().BeTrue();

        // Clear invocations for clean verification
        _mockCompatibleSettingsRegistry.Invocations.Clear();
        _mockEventBus.Invocations.Clear();

        // Restore should bring it back to saved value (false)
        await service.RestoreFilterPreferenceAsync();

        service.IsFilterEnabled.Should().BeFalse();
        _mockCompatibleSettingsRegistry.Verify(r => r.SetFilterEnabled(false), Times.Once);
        _mockEventBus.Verify(
            e => e.Publish(It.Is<FilterStateChangedEvent>(evt => evt.IsFilterEnabled == false)),
            Times.Once);
    }

    [Fact]
    public async Task RestoreFilterPreferenceAsync_WhenSavedPreferenceMatchesCurrent_DoesNothing()
    {
        var service = CreateService();
        service.IsFilterEnabled.Should().BeTrue();

        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.EnableWindowsVersionFilter, true))
            .ReturnsAsync(true); // Same as current

        await service.RestoreFilterPreferenceAsync();

        _mockCompatibleSettingsRegistry.Verify(r => r.SetFilterEnabled(It.IsAny<bool>()), Times.Never);
        _mockEventBus.Verify(e => e.Publish(It.IsAny<FilterStateChangedEvent>()), Times.Never);
    }

    [Fact]
    public async Task RestoreFilterPreferenceAsync_FiresFilterStateChangedEvent_WhenStateChanges()
    {
        // Start with filter enabled (default true), force the saved pref to be false
        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.EnableWindowsVersionFilter, true))
            .ReturnsAsync(false);

        // We need to make IsFilterEnabled different from saved. Force it to true first via LoadFilter with true
        var service = CreateService();
        // Default is true, saved is false, so they differ

        bool? receivedState = null;
        service.FilterStateChanged += (_, state) => receivedState = state;

        await service.RestoreFilterPreferenceAsync();

        receivedState.Should().BeFalse();
    }

    // -------------------------------------------------------
    // Double-toggle returns to original state
    // -------------------------------------------------------

    [Fact]
    public async Task ToggleFilterAsync_TwiceReturnsToOriginalState()
    {
        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.DontShowFilterExplanation, false))
            .ReturnsAsync(true); // Skip dialog

        _mockPreferencesService
            .Setup(p => p.SetPreferenceAsync(UserPreferenceKeys.EnableWindowsVersionFilter, It.IsAny<bool>()))
            .ReturnsAsync(OperationResult.Succeeded());

        var service = CreateService();
        var originalState = service.IsFilterEnabled;

        await service.ToggleFilterAsync(isInReviewMode: false);
        await service.ToggleFilterAsync(isInReviewMode: false);

        service.IsFilterEnabled.Should().Be(originalState);
    }

    // -------------------------------------------------------
    // User cancels dialog but checkbox was checked
    // -------------------------------------------------------

    [Fact]
    public async Task ToggleFilterAsync_UserCancelsButChecksBox_SavesDontShowButDoesNotToggle()
    {
        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.DontShowFilterExplanation, false))
            .ReturnsAsync(false);

        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns("text");

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false, CheckboxChecked = true }); // Cancelled but checkbox checked

        _mockPreferencesService
            .Setup(p => p.SetPreferenceAsync(UserPreferenceKeys.DontShowFilterExplanation, true))
            .ReturnsAsync(OperationResult.Succeeded());

        var service = CreateService();
        var originalState = service.IsFilterEnabled;

        var result = await service.ToggleFilterAsync(isInReviewMode: false);

        result.Should().BeFalse();
        service.IsFilterEnabled.Should().Be(originalState);

        // The "don't show" pref should still be saved even though toggle was cancelled
        _mockPreferencesService.Verify(
            p => p.SetPreferenceAsync(UserPreferenceKeys.DontShowFilterExplanation, true),
            Times.Once);
    }
}
