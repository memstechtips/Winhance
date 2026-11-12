using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Helpers;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

public class FilePickerService(
    IMainWindowProvider mainWindowProvider,
    IDialogService dialogService,
    ILocalizationService localizationService,
    ILogService logService) : IFilePickerService
{
    public string? PickFile(string[] filters, string? suggestedFileName = null)
    {
        var window = mainWindowProvider.MainWindow;
        if (window == null) return null;

        // filters are pairs: [filterName, filterPattern, ...]
        var filterName = filters.Length > 0 ? filters[0] : "All Files";
        var filterPattern = filters.Length > 1 ? filters[1] : "*.*";

        return Win32FileDialogHelper.ShowOpenFilePicker(window, suggestedFileName ?? filterName, filterName, filterPattern);
    }

    public string? PickFolder(string? title = null)
    {
        var window = mainWindowProvider.MainWindow;
        if (window == null) return null;

        return Win32FileDialogHelper.ShowFolderPicker(window, title ?? "Select Folder");
    }

    public string? PickSavePath(string title, string filterName, string filterPattern, string defaultFileName, string defaultExtension)
    {
        var window = mainWindowProvider.MainWindow;
        if (window == null)
        {
            // The COM dialog needs an owner hwnd. The error dialog is left to run on its own: the caller has nothing
            // further to do once this returns null, the same as a cancel.
            logService.Log(LogLevel.Error, "Cannot show file dialog - no main window");
            dialogService.ShowErrorAsync(localizationService.GetString("Dialog_FileDialogUnavailable")).FireAndForget(logService);
            return null;
        }

        return Win32FileDialogHelper.ShowSaveFilePicker(window, title, filterName, filterPattern, defaultFileName, defaultExtension);
    }
}
