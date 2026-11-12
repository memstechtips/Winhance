namespace Winhance.UI.Features.Common.Interfaces;

// null = the user cancelled, or there is no main window to parent the dialog to.
public interface IFilePickerService
{
    // filters: pairs [name, pattern, ...], e.g. ["ISO Files", "*.iso"].
    string? PickFile(string[] filters, string? suggestedFileName = null);

    string? PickFolder(string? title = null);

    string? PickSavePath(string title, string filterName, string filterPattern, string defaultFileName, string defaultExtension);
}
