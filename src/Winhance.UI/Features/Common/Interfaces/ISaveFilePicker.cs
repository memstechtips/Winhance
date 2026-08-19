namespace Winhance.UI.Features.Common.Interfaces;

// null = the user cancelled, or there is no main window to parent the dialog to (already reported to the user).
public interface ISaveFilePicker
{
    string? PickSavePath(string title, string filterName, string filterPattern, string defaultFileName, string defaultExtension);
}
