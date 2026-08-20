using Winhance.UI.Features.Common.Helpers;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

public class FilePickerService : IFilePickerService
{
    private readonly IMainWindowProvider _mainWindowProvider;

    public FilePickerService(IMainWindowProvider mainWindowProvider)
    {
        _mainWindowProvider = mainWindowProvider;
    }

    public string? PickFile(string[] filters, string? suggestedFileName = null)
    {
        var window = _mainWindowProvider.MainWindow;
        if (window == null) return null;

        // filters are pairs: [filterName, filterPattern, ...]
        var filterName = filters.Length > 0 ? filters[0] : "All Files";
        var filterPattern = filters.Length > 1 ? filters[1] : "*.*";

        return Win32FileDialogHelper.ShowOpenFilePicker(window, suggestedFileName ?? filterName, filterName, filterPattern);
    }

    public string? PickFolder(string? title = null)
    {
        var window = _mainWindowProvider.MainWindow;
        if (window == null) return null;

        return Win32FileDialogHelper.ShowFolderPicker(window, title ?? "Select Folder");
    }

    public string? PickSaveFile(string[] filters, string? suggestedFileName = null, string? defaultExtension = null)
    {
        var window = _mainWindowProvider.MainWindow;
        if (window == null) return null;

        var filterName = filters.Length > 0 ? filters[0] : "All Files";
        var filterPattern = filters.Length > 1 ? filters[1] : "*.*";
        var fileName = suggestedFileName ?? "";
        var ext = defaultExtension ?? "";

        return Win32FileDialogHelper.ShowSaveFilePicker(window, filterName, filterName, filterPattern, fileName, ext);
    }
}
