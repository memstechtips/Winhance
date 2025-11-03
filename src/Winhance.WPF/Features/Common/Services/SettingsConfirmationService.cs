using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.Interfaces;

namespace Winhance.WPF.Features.Common.Services
{
    public class SettingsConfirmationService(IDialogService dialogService) : ISettingsConfirmationService
    {

        public async Task<(bool confirmed, bool checkboxChecked)> HandleConfirmationAsync(
            string settingId,
            object? value,
            SettingDefinition setting)
        {
            if (setting == null) throw new ArgumentNullException(nameof(setting));

            if (!setting.RequiresConfirmation)
            {
                return (true, true);
            }

            string confirmationTitle = setting.ConfirmationTitle ?? "Confirmation";
            string confirmationMessage = setting.ConfirmationMessage ?? "";

            if (value is int selectedIndex &&
                setting.CustomProperties?.TryGetValue(CustomPropertyKeys.OptionConfirmations, out var confirmations) == true &&
                confirmations is Dictionary<int, (string Title, string Message)> confirmDict &&
                confirmDict.TryGetValue(selectedIndex, out var optionConfirmation))
            {
                confirmationTitle = optionConfirmation.Title;
                confirmationMessage = optionConfirmation.Message;
            }

            confirmationMessage = ReplacePlaceholders(confirmationMessage, settingId, value);

            var confirmationCheckboxText = ReplacePlaceholders(
                setting.ConfirmationCheckboxText ?? "",
                settingId,
                value
            );

            var (confirmed, checkboxChecked) = await dialogService.ShowConfirmationWithCheckboxAsync(
                confirmationMessage,
                string.IsNullOrEmpty(confirmationCheckboxText) ? null : confirmationCheckboxText,
                confirmationTitle,
                "Continue",
                "Cancel",
                setting.DialogTitleIcon
            );

            return (confirmed, checkboxChecked);
        }

        public string ReplacePlaceholders(string text, string settingId, object? value)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            if (settingId == "theme-mode-windows")
            {
                var isDarkMode = value is int comboBoxIndex ? comboBoxIndex == 1 : false;
                var themeMode = isDarkMode ? "Dark Mode" : "Light Mode";
                text = text.Replace("{themeMode}", themeMode);
            }

            return text;
        }
    }
}
