using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IDialogService
{
    // An empty title or button text means the implementation supplies the localized default
    // (Information / Warning / Error, OK, Close) - never pass English literals here.
    void ShowMessage(string message, string title = "");

    Task ShowInformationAsync(string message, string title = "", string buttonText = "");

    Task ShowWarningAsync(string message, string title = "", string buttonText = "");

    Task ShowErrorAsync(string message, string title = "", string buttonText = "");

    // A Yes opens the store page from inside the dialog.
    Task ShowSponsorsDialogAsync(SponsorsDialogMode mode);

    Task<(ImportOption? Option, ImportOptions Options)> ShowConfigImportOptionsDialogAsync();

    Task<BuilderSeed?> ShowBuilderSeedDialogAsync();

    // Null when cancelled. appName null = the defaults dialog (no follow-defaults switch).
    Task<InstallOptionsChoice?> ShowInstallOptionsDialogAsync(string? appName, InstallOptionsChoice current);

    Task<ConfirmationResponse> ShowConfirmationAsync(ConfirmationRequest confirmationRequest);

    // True only when the request asks a question and the user confirmed it.
    Task<bool> ShowSummaryAsync(SummaryRequest request);

    Task<ConfirmationResponse> AskSummaryAsync(SummaryRequest request);

    // The trimmed text, or null when cancelled.
    Task<string?> AskTextAsync(TextInputRequest request);

    Task ShowTaskOutputDialogAsync(string title, IReadOnlyList<string> logMessages);

    Task ShowCustomContentDialogAsync(string title, object content, string closeButtonText = "");
}
