namespace Winhance.UI.Features.Common.Interfaces;

// null = the user cancelled, or there is no main window to parent the dialog to (already logged, and reported to
// the user where a dialog can show at all).
public interface ISaveFilePicker
{
    string? PickSavePath(string title, string filterName, string filterPattern, string defaultFileName, string defaultExtension);
}
