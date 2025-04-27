using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace Winhance.WPF.Theme
{
    public static class FontLoader
    {
        [DllImport("gdi32.dll")]
        private static extern int AddFontResource(string lpFilename);

        [DllImport("gdi32.dll")]
        private static extern int RemoveFontResource(string lpFilename);

        private static string? _tempFontPath;

        public static bool LoadFont(string resourcePath)
        {
            try
            {
                // Extract the font to a temporary file
                var fontUri = new Uri(resourcePath);
                var streamResourceInfo = Application.GetResourceStream(fontUri);

                if (streamResourceInfo == null)
                {
                    return false;
                }

                // Create a temporary file for the font
                _tempFontPath = Path.Combine(
                    Path.GetTempPath(),
                    $"MaterialSymbols_{Guid.NewGuid()}.ttf"
                );

                using (var fileStream = File.Create(_tempFontPath))
                using (var resourceStream = streamResourceInfo.Stream)
                {
                    resourceStream.CopyTo(fileStream);
                }

                // Load the font using the Win32 API
                int result = AddFontResource(_tempFontPath);

                // Force a redraw of all text elements
                foreach (Window window in Application.Current.Windows)
                {
                    window.InvalidateVisual();
                }

                return result > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void UnloadFont()
        {
            if (!string.IsNullOrEmpty(_tempFontPath) && File.Exists(_tempFontPath))
            {
                RemoveFontResource(_tempFontPath);
                try
                {
                    File.Delete(_tempFontPath);
                }
                catch
                {
                    // Ignore errors when deleting the temporary file
                }
            }
        }
    }
}
