using System.Runtime.InteropServices;

// Compiled into Winhance AND embedded verbatim as the source PowerShell's Add-Type compiles on the new PC, so
// the app and the answer file run one implementation of this call. That second compiler is why the interop is
// hand-written rather than generated: CsWin32 emits into this project's compilation only, and nothing it emits
// can travel inside an answer file. It is also why the file stays C# 5 - Windows PowerShell hands Add-Type to
// CodeDom, which knows nothing newer - so: block-scoped namespace, no interpolation, no expression bodies, no
// out-var, no nullable annotations, and every System type spelled in full - Infrastructure has
// ImplicitUsings and Add-Type has nothing, so a `using System;` here is dead to one compiler and
// load-bearing for the other. The one using left is the one neither can supply.
// A ComImport interface dispatches on vtable position alone, so every member is declared, in the order CsWin32
// generates from the Win32 metadata, even though only four are ever called. The monitor-id parameters are
// IntPtr because the only value ever passed is the documented NULL meaning "every monitor".
namespace Winhance.Desktop
{
    public static class DesktopBackground
    {
        private const string DesktopWallpaperClsid = "C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD";
        private const string ShellItemIid = "43826D1E-E718-42EE-BC55-A1E261C37BFE";
        private const string ShellItemArrayIid = "B63EA76D-1F85-456F-A19C-48159EFA858B";

        private const int ShuffleImages = 1;
        private const uint FileSystemPath = 0x80058000;

        // SetSlideshow takes "a single item which is the container itself", not the pictures inside it.
        public static void Slideshow(string folder, int position, int intervalMs, bool shuffle)
        {
            IDesktopWallpaper desktop = Create();
            try
            {
                desktop.SetSlideshow(Album(folder));
                desktop.SetSlideshowOptions(shuffle ? ShuffleImages : 0, (uint)intervalMs);
                desktop.SetPosition(position);
            }
            finally
            {
                Marshal.ReleaseComObject(desktop);
            }
        }

        // The album the slideshow plays, or empty when none is set. Nullable annotations are out of reach here, so
        // empty stands in for null. Asking the shell is the only read that finds an album SetSlideshow set: the
        // Settings app records it in the registry as well, SetSlideshow does not.
        public static string SlideshowFolder()
        {
            IDesktopWallpaper desktop = Create();
            try
            {
                IShellItemArray items = desktop.GetSlideshow();
                if (items == null || items.GetCount() == 0)
                {
                    return string.Empty;
                }

                return items.GetItemAt(0).GetDisplayName(FileSystemPath);
            }
            finally
            {
                Marshal.ReleaseComObject(desktop);
            }
        }

        // SetWallpaper records the picture and repaints the desktop itself, so this needs no registry write
        // and no SystemParametersInfo broadcast beside it.
        public static void Picture(string path, int position)
        {
            IDesktopWallpaper desktop = Create();
            try
            {
                desktop.SetWallpaper(System.IntPtr.Zero, path);
                desktop.SetPosition(position);
            }
            finally
            {
                Marshal.ReleaseComObject(desktop);
            }
        }

        // Windows keeps the fit as the pair WallpaperStyle + TileWallpaper, and DESKTOP_WALLPAPER_POSITION
        // numbers the same six fits differently. Tile is the only one that moves the second value.
        public static int Position(string wallpaperStyle, string tileWallpaper)
        {
            if (tileWallpaper == "1")
            {
                return 1;
            }

            switch (wallpaperStyle)
            {
                case "0": return 0;
                case "2": return 2;
                case "6": return 3;
                case "22": return 5;
                default: return 4;
            }
        }

        // Activating by CLSID is the point, not a detour. A [ComImport] coclass declares no interfaces, so
        // casting an instance of it is a conversion the type system cannot make and PowerShell refuses
        // outright; activating by CLSID yields a plain runtime callable wrapper, and the cast below is then
        // the QueryInterface the runtime does know how to do.
        private static IDesktopWallpaper Create()
        {
            var coclass = System.Type.GetTypeFromCLSID(new System.Guid(DesktopWallpaperClsid));
            if (coclass == null)
            {
                throw new System.InvalidOperationException("The desktop wallpaper COM class is not registered on this PC.");
            }

            var instance = System.Activator.CreateInstance(coclass);
            if (instance == null)
            {
                throw new System.InvalidOperationException("The desktop wallpaper COM class could not be created.");
            }

            return (IDesktopWallpaper)instance;
        }

        private static IShellItemArray Album(string folder)
        {
            var itemId = new System.Guid(ShellItemIid);
            var arrayId = new System.Guid(ShellItemArrayIid);

            IShellItem folderItem;
            Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(folder, System.IntPtr.Zero, ref itemId, out folderItem));

            IShellItemArray items;
            Marshal.ThrowExceptionForHR(SHCreateShellItemArrayFromShellItem(folderItem, ref arrayId, out items));
            return items;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int SHCreateItemFromParsingName(string path, System.IntPtr bindContext, ref System.Guid riid, out IShellItem item);

        [DllImport("shell32.dll", ExactSpelling = true)]
        private static extern int SHCreateShellItemArrayFromShellItem(IShellItem item, ref System.Guid riid, out IShellItemArray items);
    }

    [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItem
    {
        void BindToHandler(System.IntPtr bindContext, ref System.Guid handler, ref System.Guid riid, out System.IntPtr result);

        void GetParent(out System.IntPtr parent);

        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetDisplayName(uint sigdnName);

        void GetAttributes(uint mask, out uint attributes);

        int Compare(System.IntPtr other, uint hint);
    }

    [ComImport, Guid("B63EA76D-1F85-456F-A19C-48159EFA858B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItemArray
    {
        void BindToHandler(System.IntPtr bindContext, ref System.Guid handler, ref System.Guid riid, out System.IntPtr result);

        void GetPropertyStore(int flags, ref System.Guid riid, out System.IntPtr result);

        void GetPropertyDescriptionList(System.IntPtr keyType, ref System.Guid riid, out System.IntPtr result);

        void GetAttributes(int attributeFlags, uint mask, out uint attributes);

        uint GetCount();

        IShellItem GetItemAt(uint index);

        void EnumItems(out System.IntPtr items);
    }

    [ComImport, Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDesktopWallpaper
    {
        void SetWallpaper(System.IntPtr monitorID, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);

        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetWallpaper(System.IntPtr monitorID);

        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetMonitorDevicePathAt(uint monitorIndex);

        uint GetMonitorDevicePathCount();

        void GetMonitorRECT(System.IntPtr monitorID, System.IntPtr displayRect);

        void SetBackgroundColor(uint color);

        uint GetBackgroundColor();

        void SetPosition(int position);

        int GetPosition();

        void SetSlideshow(IShellItemArray items);

        IShellItemArray GetSlideshow();

        void SetSlideshowOptions(int options, uint slideshowTick);

        void GetSlideshowOptions(out int options, out uint slideshowTick);

        void AdvanceSlideshow(System.IntPtr monitorID, int direction);

        int GetStatus();

        void Enable(int enable);
    }
}
