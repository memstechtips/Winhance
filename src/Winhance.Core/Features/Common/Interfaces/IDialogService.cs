using System.Threading.Tasks;
using System.Collections.Generic;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IDialogService
{
    void ShowMessage(string message, string title = "");

    Task ShowInformationAsync(string message, string title = "Information", string buttonText = "OK");

    Task ShowWarningAsync(string message, string title = "Warning", string buttonText = "OK");

    Task ShowErrorAsync(string message, string title = "Error", string buttonText = "OK");

    Task<(bool SupportClicked, bool DontShowAgain)> ShowSponsorsDialogAsync(SponsorsDialogMode mode);

    Task<(ImportOption? Option, ImportOptions Options)> ShowConfigImportOptionsDialogAsync();

    Task<ConfirmationResponse> ShowConfirmationAsync(ConfirmationRequest confirmationRequest);

    Task ShowTaskOutputDialogAsync(string title, IReadOnlyList<string> logMessages);

    Task ShowCustomContentDialogAsync(string title, object content, string closeButtonText = "Close");
}
