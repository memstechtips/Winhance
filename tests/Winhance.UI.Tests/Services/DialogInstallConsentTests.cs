using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class DialogInstallConsentTests
{
    private readonly Mock<IDialogService> _dialog = new();
    private readonly Mock<ILocalizationService> _localization = new();
    private ConfirmationRequest? _shown;

    public DialogInstallConsentTests()
    {
        _localization.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);
        _localization.Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string key, object[] args) => $"{key}:{args[0]}");
        _dialog.Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .Callback<ConfirmationRequest>(r => _shown = r)
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });
    }

    private DialogInstallConsent CreateSut() => new(_dialog.Object, _localization.Object);

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
}
