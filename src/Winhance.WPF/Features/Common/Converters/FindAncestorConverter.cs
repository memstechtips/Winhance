using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace Winhance.WPF.Features.Common.Converters
{
    /// <summary>
    /// Converter that finds an ancestor of a specified type in the visual tree.
    /// Used to detect parent control states for styling purposes.
    /// </summary>
    public class FindAncestorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // This converter is primarily used in bindings to find ancestors
            // The actual conversion is done through RelativeSource FindAncestor in XAML
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    /// <summary>
    /// Converter that checks if the parent is hovered, supporting both ListViewItem and GridViewColumnHeader parents.
    /// </summary>
    public class ParentHoverConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // Check if any of the parent controls are hovered
            foreach (var value in values)
            {
                if (value is bool isHovered && isHovered)
                {
                    return true;
                }
            }
            
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
