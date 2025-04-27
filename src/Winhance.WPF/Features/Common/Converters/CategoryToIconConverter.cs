using System;
using System.Globalization;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;

namespace Winhance.WPF.Features.Common.Converters
{
    /// <summary>
    /// Converts a category name to an appropriate Material Design icon.
    /// </summary>
    public class CategoryToIconConverter : IValueConverter
    {
        public static CategoryToIconConverter Instance { get; } = new CategoryToIconConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string categoryName)
            {
                // Convert category name to lowercase for case-insensitive comparison
                string category = categoryName.ToLowerInvariant();

                // Map category names to appropriate Material Design icons
                return category switch
                {
                    // Browser related categories
                    var c when c.Contains("browser") => PackIconKind.Web,
                    
                    // Compression related categories
                    var c when c.Contains("compression") => PackIconKind.ZipBox,
                    var c when c.Contains("zip") => PackIconKind.ZipBox,
                    var c when c.Contains("archive") => PackIconKind.Archive,
                    
                    // Customization related categories
                    var c when c.Contains("customization") => PackIconKind.Palette,
                    var c when c.Contains("utilities") => PackIconKind.Tools,
                    var c when c.Contains("shell") => PackIconKind.Console,
                    
                    // Development related categories
                    var c when c.Contains("development") => PackIconKind.CodeBraces,
                    var c when c.Contains("programming") => PackIconKind.CodeBraces,
                    var c when c.Contains("code") => PackIconKind.CodeBraces,
                    
                    // Document related categories
                    var c when c.Contains("document") => PackIconKind.FileDocument,
                    var c when c.Contains("pdf") => PackIconKind.File,
                    var c when c.Contains("office") => PackIconKind.FileDocument,
                    var c when c.Contains("viewer") => PackIconKind.FileDocument,
                    
                    // Media related categories
                    var c when c.Contains("media") => PackIconKind.Play,
                    var c when c.Contains("video") => PackIconKind.Video,
                    var c when c.Contains("audio") => PackIconKind.Music,
                    var c when c.Contains("player") => PackIconKind.Play,
                    var c when c.Contains("multimedia") => PackIconKind.Play,
                    
                    // Communication related categories
                    var c when c.Contains("communication") => PackIconKind.Message,
                    var c when c.Contains("chat") => PackIconKind.Chat,
                    var c when c.Contains("email") => PackIconKind.Email,
                    var c when c.Contains("messaging") => PackIconKind.Message,
                    var c when c.Contains("calendar") => PackIconKind.Calendar,
                    
                    // Security related categories
                    var c when c.Contains("security") => PackIconKind.Shield,
                    var c when c.Contains("antivirus") => PackIconKind.ShieldOutline,
                    var c when c.Contains("firewall") => PackIconKind.Fire,
                    var c when c.Contains("privacy") => PackIconKind.Lock,
                    
                    // File & Disk Management
                    var c when c.Contains("file") => PackIconKind.Folder,
                    var c when c.Contains("disk") => PackIconKind.Database,
                    
                    // Gaming
                    var c when c.Contains("gaming") => PackIconKind.GamepadVariant,
                    var c when c.Contains("game") => PackIconKind.GamepadVariant,
                    
                    // Imaging
                    var c when c.Contains("imaging") => PackIconKind.Image,
                    var c when c.Contains("image") => PackIconKind.Image,
                    
                    // Online Storage
                    var c when c.Contains("storage") => PackIconKind.Cloud,
                    var c when c.Contains("cloud") => PackIconKind.Cloud,
                    
                    // Remote Access
                    var c when c.Contains("remote") => PackIconKind.Remote,
                    var c when c.Contains("access") => PackIconKind.Remote,
                    
                    // Optical Disc Utilities
                    var c when c.Contains("optical") => PackIconKind.Album,
                    var c when c.Contains("disc") => PackIconKind.Album,
                    var c when c.Contains("dvd") => PackIconKind.Album,
                    var c when c.Contains("cd") => PackIconKind.Album,
                    
                    // Default icon for unknown categories
                    _ => PackIconKind.Apps
                };
            }
            
            // Default to Apps icon if value is not a string
            return PackIconKind.Apps;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // This converter doesn't support converting back
            throw new NotImplementedException();
        }
    }
}
