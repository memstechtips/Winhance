using System;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.Interfaces;

namespace Winhance.WPF.Features.Common.Services
{
    /// <summary>
    /// Service responsible for handling setting confirmation dialogs.
    /// Follows SRP by handling only confirmation UI logic.
    /// </summary>
    public class SettingsConfirmationService : ISettingsConfirmationService
    {
        private readonly IDialogService _dialogService;

        public SettingsConfirmationService(IDialogService dialogService)
        {
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        }

        public async Task<(bool confirmed, bool checkboxChecked)> HandleConfirmationAsync(
            string settingId, 
            object? value, 
            ApplicationSetting setting)
        {
            if (setting == null) throw new ArgumentNullException(nameof(setting));

            // If no confirmation required, return success
            if (!setting.RequiresConfirmation)
            {
                return (true, true);
            }

            // Build confirmation dialog from model metadata with placeholder replacement
            var confirmationMessage = ReplacePlaceholders(
                setting.ConfirmationMessage ?? "",
                settingId,
                value
            );
            
            var confirmationCheckboxText = ReplacePlaceholders(
                setting.ConfirmationCheckboxText ?? "",
                settingId,
                value
            );

            var (confirmed, checkboxChecked) = await _dialogService.ShowConfirmationWithCheckboxAsync(
                confirmationMessage,
                string.IsNullOrEmpty(confirmationCheckboxText) ? null : confirmationCheckboxText,
                setting.ConfirmationTitle ?? "Confirmation"
            );

            return (confirmed, checkboxChecked);
        }

        public string ReplacePlaceholders(string text, string settingId, object? value)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Replace {themeMode} placeholder for theme settings
            if (settingId == "theme-mode-windows")
            {
                var isDarkMode = value is int comboBoxIndex ? comboBoxIndex == 0 : true;
                var themeMode = isDarkMode ? "Dark Mode" : "Light Mode";
                text = text.Replace("{themeMode}", themeMode);
            }

            // Add more placeholder replacements here for other settings as needed

            return text;
        }
    }
}
