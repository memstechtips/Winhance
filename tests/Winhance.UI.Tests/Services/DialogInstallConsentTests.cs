using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class DialogInstallConsentTests
{
    private const string PreferenceKey = "StoreDownloadFallback_DontShowAgain";

    private readonly Mock<IDialogService> _dialog = new();
    private readonly Mock<ILocalizationService> _localization = new();
    private readonly Mock<IUserPreferencesService> _prefs = new();
    private ConfirmationRequest? _shown;

    public DialogInstallConsentTests()
    {
        _localization.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);
        _localization.Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string key, object[] args) => $"{key}:{args[0]}");
        _dialog.Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .Callback<ConfirmationRequest>(r => _shown = r)
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });
        _prefs.Setup(p => p.GetPreferenceAsync(PreferenceKey, false)).ReturnsAsync(false);
        _prefs.Setup(p => p.SetPreferenceAsync(PreferenceKey, true)).ReturnsAsync(OperationResult.Succeeded());
    }

    private DialogInstallConsent CreateSut() => new(_dialog.Object, _localization.Object, _prefs.Object);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AllowUpdatePolicyChange_ReturnsWhatTheUserAnswered(bool confirmed)
    {
        _dialog.Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .Callback<ConfirmationRequest>(r => _shown = r)
            .ReturnsAsync(new ConfirmationResponse { Confirmed = confirmed });

        var result = await CreateSut().AllowUpdatePolicyChangeAsync("Test App");

        result.Should().Be(confirmed);
        _shown!.Title.Should().Be("Dialog_UpdatePolicyBlocking_Title");
        _shown.Message.Should().Be("Dialog_UpdatePolicyBlocking_Message:Test App");
        _shown.ConfirmButtonText.Should().Be("Button_Yes");
        _shown.CancelButtonText.Should().Be("Button_No");
    }

    [Fact]
    public async Task AllowFallbackDownload_ShowsTheResourceStringsAlone()
    {
        var result = await CreateSut().AllowFallbackDownloadAsync("Test App");

        result.Should().BeTrue();
        _shown!.Title.Should().Be("Dialog_FallbackDownload");
        _shown.Message.Should().Be("WindowsApps_Msg_FallbackDownload:Test App");
        _shown.CheckboxText.Should().Be("WindowsApps_Checkbox_DontAskAgain");
        _shown.ConfirmButtonText.Should().Be("Button_Download");
        _shown.CancelButtonText.Should().Be("Button_Cancel");
    }

    [Fact]
    public async Task AllowFallbackDownload_UserCancels_ReturnsFalse()
    {
        _dialog.Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

        var result = await CreateSut().AllowFallbackDownloadAsync("Test App");

        result.Should().BeFalse();
        _prefs.Verify(p => p.SetPreferenceAsync(PreferenceKey, true), Times.Never);
    }

    [Fact]
    public async Task AllowFallbackDownload_DontAskAgainChecked_StoresThePreference()
    {
        _dialog.Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true, CheckboxChecked = true });

        await CreateSut().AllowFallbackDownloadAsync("Test App");

        _prefs.Verify(p => p.SetPreferenceAsync(PreferenceKey, true), Times.Once);
    }

    [Fact]
    public async Task AllowFallbackDownload_PreferenceAlreadyStored_ConsentsWithoutAsking()
    {
        _prefs.Setup(p => p.GetPreferenceAsync(PreferenceKey, false)).ReturnsAsync(true);

        var result = await CreateSut().AllowFallbackDownloadAsync("Test App");

        result.Should().BeTrue();
        _dialog.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Never);
    }
}
