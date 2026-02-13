using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using WinRT.Interop;

namespace Winhance.UI.Features.Common.Helpers;

/// <summary>
/// Provides Win32 file dialog functionality that works when running as administrator.
/// WinRT file pickers fail with admin elevation, so we use COM IFileDialog instead.
/// </summary>
public static class Win32FileDialogHelper
{
    #region COM Interfaces and GUIDs

    private static readonly Guid CLSID_FileOpenDialog = new("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7");
    private static readonly Guid CLSID_FileSaveDialog = new("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B");

    [ComImport]
    [Guid("42f85136-db7e-439c-85f1-e4075d135fc8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileDialog
    {
        [PreserveSig] int Show(IntPtr parent);
        void SetFileTypes(uint cFileTypes, [MarshalAs(UnmanagedType.LPArray)] COMDLG_FILTERSPEC[] rgFilterSpec);
        void SetFileTypeIndex(uint iFileType);
        void GetFileTypeIndex(out uint piFileType);
        void Advise(IntPtr pfde, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOptions(FOS fos);
        void GetOptions(out FOS pfos);
        void SetDefaultFolder(IShellItem psi);
        void SetFolder(IShellItem psi);
        void GetFolder(out IShellItem ppsi);
        void GetCurrentSelection(out IShellItem ppsi);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        void GetResult(out IShellItem ppsi);
        void AddPlace(IShellItem psi, int fdap);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        void Close(int hr);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(IntPtr pFilter);
    }

    [ComImport]
    [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct COMDLG_FILTERSPEC
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pszName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pszSpec;
    }

    [Flags]
    private enum FOS : uint
    {
        FOS_OVERWRITEPROMPT = 0x2,
        FOS_STRICTFILETYPES = 0x4,
        FOS_NOCHANGEDIR = 0x8,
        FOS_PICKFOLDERS = 0x20,
        FOS_FORCEFILESYSTEM = 0x40,
        FOS_ALLNONSTORAGEITEMS = 0x80,
        FOS_NOVALIDATE = 0x100,
        FOS_ALLOWMULTISELECT = 0x200,
        FOS_PATHMUSTEXIST = 0x800,
        FOS_FILEMUSTEXIST = 0x1000,
        FOS_CREATEPROMPT = 0x2000,
        FOS_SHAREAWARE = 0x4000,
        FOS_NOREADONLYRETURN = 0x8000,
        FOS_NOTESTFILECREATE = 0x10000,
        FOS_HIDEMRUPLACES = 0x20000,
        FOS_HIDEPINNEDPLACES = 0x40000,
        FOS_NODEREFERENCELINKS = 0x100000,
        FOS_DONTADDTORECENT = 0x2000000,
        FOS_FORCESHOWHIDDEN = 0x10000000,
        FOS_DEFAULTNOMINIMODE = 0x20000000,
        FOS_FORCEPREVIEWPANEON = 0x40000000
    }

    private enum SIGDN : uint
    {
        SIGDN_NORMALDISPLAY = 0x00000000,
        SIGDN_PARENTRELATIVEPARSING = 0x80018001,
        SIGDN_DESKTOPABSOLUTEPARSING = 0x80028000,
        SIGDN_PARENTRELATIVEEDITING = 0x80031001,
        SIGDN_DESKTOPABSOLUTEEDITING = 0x8004c000,
        SIGDN_FILESYSPATH = 0x80058000,
        SIGDN_URL = 0x80068000,
        SIGDN_PARENTRELATIVEFORADDRESSBAR = 0x8007c001,
        SIGDN_PARENTRELATIVE = 0x80080001
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        ref Guid riid,
        out IShellItem ppv);

    #endregion

    /// <summary>
    /// Shows a folder picker dialog.
    /// </summary>
    /// <param name="window">The parent window.</param>
    /// <param name="title">The dialog title.</param>
    /// <returns>The selected folder path, or null if cancelled.</returns>
    public static string? ShowFolderPicker(Window window, string title)
    {
        var dialog = (IFileDialog)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_FileOpenDialog)!)!;

        dialog.SetOptions(FOS.FOS_PICKFOLDERS | FOS.FOS_FORCEFILESYSTEM | FOS.FOS_PATHMUSTEXIST);
        dialog.SetTitle(title);

        var hwnd = WindowNative.GetWindowHandle(window);
        if (dialog.Show(hwnd) != 0)
            return null;

        dialog.GetResult(out var item);
        item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out var path);
        return path;
    }

    /// <summary>
    /// Shows a file open dialog.
    /// </summary>
    /// <param name="window">The parent window.</param>
    /// <param name="title">The dialog title.</param>
    /// <param name="filterName">The filter display name (e.g., "ISO Files").</param>
    /// <param name="filterPattern">The filter pattern (e.g., "*.iso").</param>
    /// <returns>The selected file path, or null if cancelled.</returns>
    public static string? ShowOpenFilePicker(Window window, string title, string filterName, string filterPattern)
    {
        var dialog = (IFileDialog)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_FileOpenDialog)!)!;

        var filters = new COMDLG_FILTERSPEC[]
        {
            new() { pszName = filterName, pszSpec = filterPattern },
            new() { pszName = "All Files", pszSpec = "*.*" }
        };
        dialog.SetFileTypes((uint)filters.Length, filters);
        dialog.SetFileTypeIndex(1);
        dialog.SetOptions(FOS.FOS_FORCEFILESYSTEM | FOS.FOS_FILEMUSTEXIST | FOS.FOS_PATHMUSTEXIST);
        dialog.SetTitle(title);

        var hwnd = WindowNative.GetWindowHandle(window);
        if (dialog.Show(hwnd) != 0)
            return null;

        dialog.GetResult(out var item);
        item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out var path);
        return path;
    }

    /// <summary>
    /// Shows a file open dialog with an initial folder.
    /// </summary>
    /// <param name="window">The parent window.</param>
    /// <param name="title">The dialog title.</param>
    /// <param name="filterName">The filter display name (e.g., "Winhance Configuration Files").</param>
    /// <param name="filterPattern">The filter pattern (e.g., "*.winhance").</param>
    /// <param name="initialFolderPath">The initial folder to open the dialog in.</param>
    /// <returns>The selected file path, or null if cancelled.</returns>
    public static string? ShowOpenFilePicker(Window window, string title, string filterName, string filterPattern, string initialFolderPath)
    {
        var dialog = (IFileDialog)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_FileOpenDialog)!)!;

        var filters = new COMDLG_FILTERSPEC[]
        {
            new() { pszName = filterName, pszSpec = filterPattern },
            new() { pszName = "All Files", pszSpec = "*.*" }
        };
        dialog.SetFileTypes((uint)filters.Length, filters);
        dialog.SetFileTypeIndex(1);
        dialog.SetOptions(FOS.FOS_FORCEFILESYSTEM | FOS.FOS_FILEMUSTEXIST | FOS.FOS_PATHMUSTEXIST);
        dialog.SetTitle(title);

        if (!string.IsNullOrEmpty(initialFolderPath) && Directory.Exists(initialFolderPath))
        {
            SetInitialFolder(dialog, initialFolderPath);
        }

        var hwnd = WindowNative.GetWindowHandle(window);
        if (dialog.Show(hwnd) != 0)
            return null;

        dialog.GetResult(out var item);
        item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out var path);
        return path;
    }

    /// <summary>
    /// Shows a file save dialog.
    /// </summary>
    /// <param name="window">The parent window.</param>
    /// <param name="title">The dialog title.</param>
    /// <param name="filterName">The filter display name (e.g., "ISO Files").</param>
    /// <param name="filterPattern">The filter pattern (e.g., "*.iso").</param>
    /// <param name="defaultFileName">The default file name.</param>
    /// <param name="defaultExtension">The default extension (e.g., "iso").</param>
    /// <returns>The selected file path, or null if cancelled.</returns>
    public static string? ShowSaveFilePicker(Window window, string title, string filterName, string filterPattern, string defaultFileName, string defaultExtension)
    {
        var dialog = (IFileDialog)Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_FileSaveDialog)!)!;

        var filters = new COMDLG_FILTERSPEC[]
        {
            new() { pszName = filterName, pszSpec = filterPattern }
        };
        dialog.SetFileTypes((uint)filters.Length, filters);
        dialog.SetFileTypeIndex(1);
        dialog.SetOptions(FOS.FOS_FORCEFILESYSTEM | FOS.FOS_OVERWRITEPROMPT | FOS.FOS_PATHMUSTEXIST);
        dialog.SetTitle(title);
        dialog.SetFileName(defaultFileName);
        dialog.SetDefaultExtension(defaultExtension);

        var hwnd = WindowNative.GetWindowHandle(window);
        if (dialog.Show(hwnd) != 0)
            return null;

        dialog.GetResult(out var item);
        item.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out var path);
        return path;
    }

    /// <summary>
    /// Sets the initial folder for a dialog.
    /// </summary>
    private static void SetInitialFolder(IFileDialog dialog, string folderPath)
    {
        var guid = typeof(IShellItem).GUID;
        if (SHCreateItemFromParsingName(folderPath, IntPtr.Zero, ref guid, out var item) == 0)
        {
            dialog.SetFolder(item);
        }
    }
}
