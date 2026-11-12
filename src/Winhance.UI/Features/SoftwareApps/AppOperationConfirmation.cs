using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;

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
        ILocalizationService localization,
        string? checkboxDescription = null) =>
        Build(operation, itemNames.Select(name => new ConfirmationItem(name)), checkboxText, localization, checkboxDescription);

    public static ConfirmationRequest Build(
        AppOperation operation,
        IEnumerable<ItemDefinition> items,
        string? checkboxText,
        ILocalizationService localization,
        string? checkboxDescription = null) =>
        Build(
            operation,
            items.Select(i => new ConfirmationItem(i.Name, i.HasInstabilityWarning, !i.CanBeReinstalled)),
            checkboxText,
            localization,
            checkboxDescription);

    public static ConfirmationRequest Build(
        AppOperation operation,
        IEnumerable<ConfirmationItem> items,
        string? checkboxText,
        ILocalizationService localization,
        string? checkboxDescription = null)
    {
        bool isInstall = operation == AppOperation.Install;
        // The icons describe what removal does, so an install list never carries them.
        var rows = items
            .Select(i => isInstall ? i with { HasInstabilityWarning = false, CannotBeReinstalled = false } : i)
            .ToList();
        return new ConfirmationRequest
        {
            Title = localization.GetString(isInstall ? "Dialog_ConfirmInstallation" : "Dialog_ConfirmRemoval"),
            Message = localization.GetString(isInstall ? "Dialog_ItemsWillBeInstalled" : "Dialog_ItemsWillBeRemoved"),
            Items = rows.Select(i => i.Name).ToList(),
            ItemDetails = rows,
            InstabilityLegend = rows.Any(i => i.HasInstabilityWarning)
                ? localization.GetString("Dialog_RemovalInstabilityLegend")
                : null,
            NoReinstallLegend = rows.Any(i => i.CannotBeReinstalled)
                ? localization.GetString("Dialog_RemovalNoReinstallLegend")
                : null,
            CheckboxText = checkboxText,
            CheckboxDescription = checkboxDescription,
            CheckboxInitiallyChecked = true,
            ConfirmButtonText = localization.GetString("Button_Continue"),
            CancelButtonText = localization.GetString("Button_Cancel")
        };
    }
}
