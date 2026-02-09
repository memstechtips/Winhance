using System.Threading.Tasks;
using System.Collections.Generic;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IDialogService
    {
        void ShowMessage(string message, string title = "");

        Task<bool> ShowConfirmationAsync(string message, string title = "", string okButtonText = "OK", string cancelButtonText = "Cancel");

        Task ShowInformationAsync(string message, string title = "Information", string buttonText = "OK");

        Task ShowWarningAsync(string message, string title = "Warning", string buttonText = "OK");

        Task ShowErrorAsync(string message, string title = "Error", string buttonText = "OK");

        Task<string?> ShowInputAsync(string message, string title = "", string defaultValue = "");

        Task<bool?> ShowYesNoCancelAsync(string message, string title = "");

        Task<Dictionary<string, bool>> ShowUnifiedConfigurationSaveDialogAsync(string title, string description, Dictionary<string, (bool IsSelected, bool IsAvailable, int ItemCount)> sections);

        Task<(Dictionary<string, bool> sections, ImportOptions options)?> ShowUnifiedConfigurationImportDialogAsync(string title, string description, Dictionary<string, (bool IsSelected, bool IsAvailable, int ItemCount)> sections);

        Task<(bool? Result, bool DontShowAgain)> ShowDonationDialogAsync(string title = null, string supportMessage = null);

        Task<(ImportOption? Option, bool SkipReview)> ShowConfigImportOptionsDialogAsync();

        Task<(bool Confirmed, bool CheckboxChecked)> ShowConfirmationWithCheckboxAsync(
            string message,
            string? checkboxText = null,
            string title = "Confirmation",
            string continueButtonText = "Continue",
            string cancelButtonText = "Cancel",
            string? titleBarIcon = null);

        void ShowOperationResult(
            string operationType,
            int successCount,
            int totalCount,
            IEnumerable<string> successItems,
            IEnumerable<string> failedItems = null,
            IEnumerable<string> skippedItems = null,
            bool hasConnectivityIssues = false,
            bool isUserCancelled = false);

        Task ShowInformationAsync(
            string title,
            string headerText,
            IEnumerable<string> apps,
            string footerText);

        Task<(bool Confirmed, bool CheckboxChecked)> ShowAppOperationConfirmationAsync(
            string operationType,
            IEnumerable<string> itemNames,
            int count,
            string? checkboxText = null);

        Task<ConfirmationResponse> ShowConfirmationAsync(
            ConfirmationRequest confirmationRequest,
            string continueButtonText = "Continue",
            string cancelButtonText = "Cancel");

        void ShowLocalizedDialog(
            string titleKey,
            string messageKey,
            DialogType dialogType,
            string iconName,
            params object[] messageParams);

        bool ShowLocalizedConfirmationDialog(
            string titleKey,
            string messageKey,
            DialogType dialogType,
            string iconName,
            params object[] messageParams);
    }
}