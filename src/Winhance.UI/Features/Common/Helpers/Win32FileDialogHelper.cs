using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.Common;
using WinRT.Interop;

namespace Winhance.UI.Features.Common.Helpers;

// WinRT file pickers fail under admin elevation, so this uses COM IFileDialog.
public static class Win32FileDialogHelper
{
    public static string? ShowFolderPicker(Window window, string title)
    {
        var dialog = (IFileDialog)new FileOpenDialog();
        dialog.SetOptions(FILEOPENDIALOGOPTIONS.FOS_PICKFOLDERS | FILEOPENDIALOGOPTIONS.FOS_FORCEFILESYSTEM | FILEOPENDIALOGOPTIONS.FOS_PATHMUSTEXIST);
        dialog.SetTitle(title);
        return Show(dialog, window);
    }

    public static string? ShowOpenFilePicker(Window window, string title, string filterName, string filterPattern)
        => ShowOpenFilePicker(window, title, filterName, filterPattern, initialFolderPath: "");

    public static string? ShowOpenFilePicker(Window window, string title, string filterName, string filterPattern, string initialFolderPath)
    {
        var dialog = (IFileDialog)new FileOpenDialog();
        SetFileTypes(dialog, filterName, filterPattern, includeAllFiles: true);
        dialog.SetOptions(FILEOPENDIALOGOPTIONS.FOS_FORCEFILESYSTEM | FILEOPENDIALOGOPTIONS.FOS_FILEMUSTEXIST | FILEOPENDIALOGOPTIONS.FOS_PATHMUSTEXIST);
        dialog.SetTitle(title);

        if (!string.IsNullOrEmpty(initialFolderPath) && Directory.Exists(initialFolderPath)
            && PInvoke.SHCreateItemFromParsingName(initialFolderPath, null, typeof(IShellItem).GUID, out var folder).Succeeded)
        {
            dialog.SetFolder((IShellItem)folder);
        }

        return Show(dialog, window);
    }

    public static string? ShowSaveFilePicker(Window window, string title, string filterName, string filterPattern, string defaultFileName, string defaultExtension)
    {
        var dialog = (IFileDialog)new FileSaveDialog();
        SetFileTypes(dialog, filterName, filterPattern, includeAllFiles: false);
        dialog.SetOptions(FILEOPENDIALOGOPTIONS.FOS_FORCEFILESYSTEM | FILEOPENDIALOGOPTIONS.FOS_OVERWRITEPROMPT | FILEOPENDIALOGOPTIONS.FOS_PATHMUSTEXIST);
        dialog.SetTitle(title);
        dialog.SetFileName(defaultFileName);
        dialog.SetDefaultExtension(defaultExtension);
        return Show(dialog, window);
    }

    // The dialog copies the filter strings, so they only need to stay pinned for the call.
    private static unsafe void SetFileTypes(IFileDialog dialog, string filterName, string filterPattern, bool includeAllFiles)
    {
        fixed (char* name = filterName, pattern = filterPattern, allName = "All Files", allPattern = "*.*")
        {
            ReadOnlySpan<COMDLG_FILTERSPEC> filters =
            [
                new() { pszName = name, pszSpec = pattern },
                new() { pszName = allName, pszSpec = allPattern },
            ];
            dialog.SetFileTypes(includeAllFiles ? filters : filters[..1]);
        }

        dialog.SetFileTypeIndex(1);
    }

    private static unsafe string? Show(IFileDialog dialog, Window window)
    {
        // Show fails with HRESULT_FROM_WIN32(ERROR_CANCELLED) when the user closes the dialog.
        try
        {
            dialog.Show(new HWND(WindowNative.GetWindowHandle(window)));
        }
        catch (COMException)
        {
            return null;
        }

        dialog.GetResult(out var item);
        item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out PWSTR path);
        try
        {
            return path.ToString();
        }
        finally
        {
            Marshal.FreeCoTaskMem((nint)path.Value);
        }
    }
}
