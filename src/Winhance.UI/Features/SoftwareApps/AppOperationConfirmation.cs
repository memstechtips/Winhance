using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.UI.Features.SoftwareApps;

/// <summary>
/// Builds the <see cref="ConfirmationRequest"/> for app install/remove operations.
/// Owns the install/remove domain knowledge (titles, headers) that used to live in
/// the generic DialogService. Pure factory — no UI types.
/// </summary>
public static class AppOperationConfirmation
{
    public static ConfirmationRequest Build(
        string operationType,
        IEnumerable<string> itemNames,
        string? checkboxText,
        ILocalizationService localization)
    {
        bool isInstall = operationType.Equals("install", StringComparison.OrdinalIgnoreCase);
        bool isRemove = operationType.Equals("remove", StringComparison.OrdinalIgnoreCase);

        string title = isInstall ? localization.GetString("Dialog_ConfirmInstallation") :
                       isRemove ? localization.GetString("Dialog_ConfirmRemoval") :
                       localization.GetString("Dialog_ConfirmOperation", operationType);

        string header = isInstall ? localization.GetString("Dialog_ItemsWillBeInstalled") :
                        isRemove ? localization.GetString("Dialog_ItemsWillBeRemoved") :
                        localization.GetString("Dialog_ItemsWillBeProcessed", operationType.ToLower());

        return new ConfirmationRequest
        {
            Title = title,
            Message = header,
            Items = itemNames.ToList(),
            CheckboxText = checkboxText,
            CheckboxInitiallyChecked = true,
            ConfirmButtonText = localization.GetString("Button_Continue"),
            CancelButtonText = localization.GetString("Button_Cancel")
        };
    }
}
