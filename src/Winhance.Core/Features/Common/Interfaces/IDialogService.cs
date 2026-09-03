using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IDialogService
{
    // An empty title or button text means the implementation supplies the localized default
    // (Information / Warning / Error, OK, Close) - never pass English literals here.
    void ShowMessage(string message, string title = "");

    Task ShowInformationAsync(string message, string title = "", string buttonText = "");

    Task ShowWarningAsync(string message, string title = "", string buttonText = "");

    Task ShowErrorAsync(string message, string title = "", string buttonText = "");

    Task<(bool SupportClicked, bool DontShowAgain)> ShowSponsorsDialogAsync(SponsorsDialogMode mode);

    Task<(ImportOption? Option, ImportOptions Options)> ShowConfigImportOptionsDialogAsync();

    Task<BuilderSeed?> ShowBuilderSeedDialogAsync();

    Task<ConfirmationResponse> ShowConfirmationAsync(ConfirmationRequest confirmationRequest);

    Task ShowTaskOutputDialogAsync(string title, IReadOnlyList<string> logMessages);

    Task<bool> ShowTaskOutputConfirmationAsync(string title, IReadOnlyList<string> logMessages, string confirmButtonText, string cancelButtonText);

    Task ShowCustomContentDialogAsync(string title, object content, string closeButtonText = "");
}
