using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class ChocolateyConsentServiceTests
{
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<IUserPreferencesService> _mockUserPreferencesService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<ILogService> _mockLogService = new();

    private readonly ChocolateyConsentService _sut;

    public ChocolateyConsentServiceTests()
    {
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        _mockUserPreferencesService
            .Setup(u => u.SetPreferenceAsync(It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(OperationResult.Succeeded());

        _sut = new ChocolateyConsentService(
            _mockDialogService.Object,
            _mockUserPreferencesService.Object,
            _mockLocalizationService.Object,
            _mockLogService.Object);
    }

    // ── RequestConsentAsync - previously consented ──

    [Fact]
    public async Task RequestConsentAsync_WhenAlreadyConsented_ReturnsTrueWithoutShowingDialog()
    {
        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync("ChocolateyFallbackConsented", false))
            .ReturnsAsync(true);

        var result = await _sut.RequestConsentAsync();

        result.Should().BeTrue();
        _mockDialogService.Verify(d => d.ShowConfirmationWithCheckboxAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // ── RequestConsentAsync - user confirms with remember ──

    [Fact]
    public async Task RequestConsentAsync_WhenUserConfirmsWithDontAskAgain_PersistsPreference()
    {
        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync("ChocolateyFallbackConsented", false))
            .ReturnsAsync(false);

        _mockDialogService
            .Setup(d => d.ShowConfirmationWithCheckboxAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((true, true));

        var result = await _sut.RequestConsentAsync();

        result.Should().BeTrue();
        _mockUserPreferencesService.Verify(u => u.SetPreferenceAsync("ChocolateyFallbackConsented", true), Times.Once);
        _mockLogService.Verify(l => l.LogInformation(It.Is<string>(s => s.Contains("remembered"))), Times.Once);
    }

    // ── RequestConsentAsync - user confirms without remember ──

    [Fact]
    public async Task RequestConsentAsync_WhenUserConfirmsWithoutDontAskAgain_DoesNotPersist()
    {
        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync("ChocolateyFallbackConsented", false))
            .ReturnsAsync(false);

        _mockDialogService
            .Setup(d => d.ShowConfirmationWithCheckboxAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((true, false));

        var result = await _sut.RequestConsentAsync();

        result.Should().BeTrue();
        _mockUserPreferencesService.Verify(u => u.SetPreferenceAsync(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        _mockLogService.Verify(l => l.LogInformation(It.Is<string>(s => s.Contains("one-time"))), Times.Once);
    }

    // ── RequestConsentAsync - user declines ──

    [Fact]
    public async Task RequestConsentAsync_WhenUserDeclines_ReturnsFalse()
    {
        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync("ChocolateyFallbackConsented", false))
            .ReturnsAsync(false);

        _mockDialogService
            .Setup(d => d.ShowConfirmationWithCheckboxAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((false, false));

        var result = await _sut.RequestConsentAsync();

        result.Should().BeFalse();
        _mockLogService.Verify(l => l.LogInformation(It.Is<string>(s => s.Contains("declined"))), Times.Once);
    }

    [Fact]
    public async Task RequestConsentAsync_WhenUserDeclinesWithCheckbox_ReturnsFalse()
    {
        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync("ChocolateyFallbackConsented", false))
            .ReturnsAsync(false);

        _mockDialogService
            .Setup(d => d.ShowConfirmationWithCheckboxAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((false, true));

        var result = await _sut.RequestConsentAsync();

        result.Should().BeFalse();
        // No preference saved when declined (even if checkbox was checked)
        _mockUserPreferencesService.Verify(u => u.SetPreferenceAsync(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    // ── RequestConsentAsync - exception handling ──

    [Fact]
    public async Task RequestConsentAsync_WhenExceptionThrown_ReturnsFalse()
    {
        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync("ChocolateyFallbackConsented", false))
            .ThrowsAsync(new Exception("Preferences error"));

        var result = await _sut.RequestConsentAsync();

        result.Should().BeFalse();
        _mockLogService.Verify(l => l.LogError(It.Is<string>(s => s.Contains("Preferences error"))), Times.Once);
    }

    [Fact]
    public async Task RequestConsentAsync_WhenDialogThrows_ReturnsFalse()
    {
        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync("ChocolateyFallbackConsented", false))
            .ReturnsAsync(false);

        _mockDialogService
            .Setup(d => d.ShowConfirmationWithCheckboxAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Dialog error"));

        var result = await _sut.RequestConsentAsync();

        result.Should().BeFalse();
    }

    // ── Localization ──

    [Fact]
    public async Task RequestConsentAsync_PassesCorrectLocalizationKeysToDialog()
    {
        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync("ChocolateyFallbackConsented", false))
            .ReturnsAsync(false);

        _mockDialogService
            .Setup(d => d.ShowConfirmationWithCheckboxAsync(
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((false, false));

        await _sut.RequestConsentAsync();

        _mockDialogService.Verify(d => d.ShowConfirmationWithCheckboxAsync(
            "Dialog_Choco_ConsentMessage",
            "Dialog_Choco_DontAskAgain",
            "Dialog_Choco_ConsentTitle",
            "Button_Yes",
            "Button_No"), Times.Once);
    }
}
