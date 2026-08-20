namespace Winhance.UI.Features.Common.Interfaces;

// Must be initialized with the main Window after it is created (same pattern as DialogService).
public interface IFilePickerService
{
    // filters: pairs [name, pattern, ...], e.g. ["ISO Files", "*.iso"].
    string? PickFile(string[] filters, string? suggestedFileName = null);

    string? PickFolder(string? title = null);

    string? PickSaveFile(string[] filters, string? suggestedFileName = null, string? defaultExtension = null);
}
