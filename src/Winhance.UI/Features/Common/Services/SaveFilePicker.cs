using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Helpers;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

public sealed class SaveFilePicker : ISaveFilePicker
{
    private readonly IMainWindowProvider _mainWindow;
    private readonly ILogService _log;
    private readonly IDialogService _dialogs;
    private readonly ILocalizationService _loc;

    public SaveFilePicker(IMainWindowProvider mainWindow, ILogService log, IDialogService dialogs, ILocalizationService loc)
    {
        _mainWindow = mainWindow;
        _log = log;
        _dialogs = dialogs;
        _loc = loc;
    }

    public string? PickSavePath(string title, string filterName, string filterPattern, string defaultFileName, string defaultExtension)
    {
        var window = _mainWindow.MainWindow;
        if (window == null)
        {
            // The COM dialog needs an owner hwnd, so there is nothing to await here - report and fall through as a cancel.
            _log.Log(LogLevel.Error, "Cannot show file dialog - no main window");
            _dialogs.ShowErrorAsync(_loc.GetString("Dialog_FileDialogUnavailable")).FireAndForget(_log);
            return null;
        }

        return Win32FileDialogHelper.ShowSaveFilePicker(window, title, filterName, filterPattern, defaultFileName, defaultExtension);
    }
}
