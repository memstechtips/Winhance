using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Services;
using Xunit;
using Winhance.TestSupport;

namespace Winhance.UI.Tests.Services;

public class WindowsVersionFilterServiceTests
{
    private readonly Mock<IUserPreferencesService> _mockPreferencesService = new();
    private readonly Mock<IEventBus> _mockEventBus = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<ILogService> _mockLogService = new();

    private WindowsVersionFilterService CreateService()
    {
        // Mirrored here rather than in a single test: every test in this class that asserts on
        // dialog text goes through GetStringOrDefault, which reads TryGetString.
        _mockLocalizationService.MirrorTryGetString();

        return new WindowsVersionFilterService(
            _mockPreferencesService.Object,
            _mockEventBus.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockLogService.Object);
    }

    [Fact]
    public void IsFilterEnabled_DefaultsToTrue()
    {
        var service = CreateService();

        service.IsFilterEnabled.Should().BeTrue();
    }

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
                It.Is<string>(s => s.Contains("ON")), It.IsAny<Exception?>(), It.IsAny<string>()),
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
                It.Is<string>(s => s.Contains("OFF")), It.IsAny<Exception?>(), It.IsAny<string>()),
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
                It.Is<string>(s => s.Contains("Failed to load filter preference")), It.IsAny<Exception?>(), It.IsAny<string>()),
            Times.Once);
    }

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
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true, CheckboxChecked = false });

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
            .ReturnsAsync(true);

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
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false, CheckboxChecked = false });

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
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true, CheckboxChecked = true });

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
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true, CheckboxChecked = false });

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
            .ReturnsAsync(true);

        _mockPreferencesService
            .Setup(p => p.SetPreferenceAsync(UserPreferenceKeys.EnableWindowsVersionFilter, It.IsAny<bool>()))
            .ReturnsAsync(OperationResult.Succeeded());

        var service = CreateService();
        service.IsFilterEnabled.Should().BeTrue();

        var result = await service.ToggleFilterAsync(isInReviewMode: false);

        result.Should().BeTrue();
        service.IsFilterEnabled.Should().BeFalse();
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
                It.Is<string>(s => s.Contains("OFF")), It.IsAny<Exception?>(), It.IsAny<string>()),
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
                It.Is<string>(s => s.Contains("Failed to toggle")), It.IsAny<Exception?>(), It.IsAny<string>()),
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
            .Returns((string)null!);

        _mockDialogService
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false, CheckboxChecked = false });

        var service = CreateService();

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

    [Fact]
    public async Task ForceFilterOn_WhenFilterAlreadyEnabled_DoesNothing()
    {
        var service = CreateService();
        service.IsFilterEnabled.Should().BeTrue();

        bool eventFired = false;
        service.FilterStateChanged += (_, _) => eventFired = true;

        service.ForceFilterOn();

        eventFired.Should().BeFalse();
        _mockEventBus.Verify(e => e.Publish(It.IsAny<FilterStateChangedEvent>()), Times.Never);
    }

    [Fact]
    public async Task ForceFilterOn_WhenFilterDisabled_EnablesFilter()
    {
        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.DontShowFilterExplanation, false))
            .ReturnsAsync(true);
        _mockPreferencesService
            .Setup(p => p.SetPreferenceAsync(UserPreferenceKeys.EnableWindowsVersionFilter, It.IsAny<bool>()))
            .ReturnsAsync(OperationResult.Succeeded());

        var service = CreateService();
        await service.ToggleFilterAsync(isInReviewMode: false);
        service.IsFilterEnabled.Should().BeFalse();

        _mockEventBus.Invocations.Clear();

        service.ForceFilterOn();

        service.IsFilterEnabled.Should().BeTrue();
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
        await service.ToggleFilterAsync(isInReviewMode: false);

        bool? receivedState = null;
        service.FilterStateChanged += (_, state) => receivedState = state;

        service.ForceFilterOn();

        receivedState.Should().BeTrue();
    }

    [Fact]
    public async Task RestoreFilterPreferenceAsync_WhenSavedPreferenceDiffers_RestoresIt()
    {
        var service = CreateService();

        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.EnableWindowsVersionFilter, true))
            .ReturnsAsync(false);

        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.DontShowFilterExplanation, false))
            .ReturnsAsync(true);
        _mockPreferencesService
            .Setup(p => p.SetPreferenceAsync(UserPreferenceKeys.EnableWindowsVersionFilter, false))
            .ReturnsAsync(OperationResult.Succeeded());
        await service.ToggleFilterAsync(isInReviewMode: false);

        service.ForceFilterOn();
        service.IsFilterEnabled.Should().BeTrue();

        _mockEventBus.Invocations.Clear();

        await service.RestoreFilterPreferenceAsync();

        service.IsFilterEnabled.Should().BeFalse();
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
            .ReturnsAsync(true);

        await service.RestoreFilterPreferenceAsync();

        _mockEventBus.Verify(e => e.Publish(It.IsAny<FilterStateChangedEvent>()), Times.Never);
    }

    [Fact]
    public async Task RestoreFilterPreferenceAsync_FiresFilterStateChangedEvent_WhenStateChanges()
    {
        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.EnableWindowsVersionFilter, true))
            .ReturnsAsync(false);

        var service = CreateService();

        bool? receivedState = null;
        service.FilterStateChanged += (_, state) => receivedState = state;

        await service.RestoreFilterPreferenceAsync();

        receivedState.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleFilterAsync_TwiceReturnsToOriginalState()
    {
        _mockPreferencesService
            .Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.DontShowFilterExplanation, false))
            .ReturnsAsync(true);

        _mockPreferencesService
            .Setup(p => p.SetPreferenceAsync(UserPreferenceKeys.EnableWindowsVersionFilter, It.IsAny<bool>()))
            .ReturnsAsync(OperationResult.Succeeded());

        var service = CreateService();
        var originalState = service.IsFilterEnabled;

        await service.ToggleFilterAsync(isInReviewMode: false);
        await service.ToggleFilterAsync(isInReviewMode: false);

        service.IsFilterEnabled.Should().Be(originalState);
    }

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
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false, CheckboxChecked = true });

        _mockPreferencesService
            .Setup(p => p.SetPreferenceAsync(UserPreferenceKeys.DontShowFilterExplanation, true))
            .ReturnsAsync(OperationResult.Succeeded());

        var service = CreateService();
        var originalState = service.IsFilterEnabled;

        var result = await service.ToggleFilterAsync(isInReviewMode: false);

        result.Should().BeFalse();
        service.IsFilterEnabled.Should().Be(originalState);

        _mockPreferencesService.Verify(
            p => p.SetPreferenceAsync(UserPreferenceKeys.DontShowFilterExplanation, true),
            Times.Once);
    }
}
