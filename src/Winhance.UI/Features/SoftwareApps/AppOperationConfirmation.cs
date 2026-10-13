using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.UI.Features.SoftwareApps;

public enum AppOperation
{
    Install,
    Remove,
}

public static class AppOperationConfirmation
{
    public static ConfirmationRequest Build(
        AppOperation operation,
        IEnumerable<string> itemNames,
        string? checkboxText,
        ILocalizationService localization)
    {
        bool isInstall = operation == AppOperation.Install;
        return new ConfirmationRequest
        {
            Title = localization.GetString(isInstall ? "Dialog_ConfirmInstallation" : "Dialog_ConfirmRemoval"),
            Message = localization.GetString(isInstall ? "Dialog_ItemsWillBeInstalled" : "Dialog_ItemsWillBeRemoved"),
            Items = itemNames.ToList(),
            CheckboxText = checkboxText,
            CheckboxInitiallyChecked = true,
            ConfirmButtonText = localization.GetString("Button_Continue"),
            CancelButtonText = localization.GetString("Button_Cancel")
        };
    }
}
