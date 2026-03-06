namespace Winhance.UI.Features.Common.Interfaces;

/// <summary>
/// Abstracts file and folder picker dialogs so ViewModels do not need
/// a direct Window reference. The service must be initialized with the
/// main Window after it is created (same pattern as DialogService).
/// </summary>
public interface IFilePickerService
{
    /// <summary>
    /// Shows an open-file picker.
    /// </summary>
    /// <param name="filters">Filter pairs: [filterName, filterPattern, ...].
    /// For example: ["ISO Files", "*.iso"].</param>
    /// <param name="suggestedFileName">Optional suggested file name.</param>
    /// <returns>The selected file path, or null if cancelled.</returns>
    string? PickFile(string[] filters, string? suggestedFileName = null);

    /// <summary>
    /// Shows a folder picker.
    /// </summary>
    /// <param name="title">The dialog title.</param>
    /// <returns>The selected folder path, or null if cancelled.</returns>
    string? PickFolder(string? title = null);

    /// <summary>
    /// Shows a save-file picker.
    /// </summary>
    /// <param name="filters">Filter pairs: [filterName, filterPattern, ...].
    /// For example: ["ISO Files", "*.iso"].</param>
    /// <param name="suggestedFileName">The default file name.</param>
    /// <param name="defaultExtension">The default extension (e.g., "iso").</param>
    /// <returns>The selected save path, or null if cancelled.</returns>
    string? PickSaveFile(string[] filters, string? suggestedFileName = null, string? defaultExtension = null);
}
