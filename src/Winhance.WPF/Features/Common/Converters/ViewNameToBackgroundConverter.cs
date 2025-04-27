using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Winhance.WPF.Features.Common.Resources.Theme;

namespace Winhance.WPF.Features.Common.Converters
{
    public class ViewNameToBackgroundConverter : IValueConverter, INotifyPropertyChanged
    {
        private static ViewNameToBackgroundConverter? _instance;
        
        public static ViewNameToBackgroundConverter Instance 
        {
            get 
            {
                if (_instance == null)
                {
                    _instance = new ViewNameToBackgroundConverter();
                }
                return _instance;
            }
        }
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        // This method will be called when the theme changes
        public void NotifyThemeChanged()
        {
            // Force a refresh of all bindings that use this converter
            Application.Current.Dispatcher.Invoke(() =>
            {
                // Notify all properties to force binding refresh
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
                
                // Also notify specific properties to ensure all binding scenarios are covered
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ThemeChanged"));
                
                // Force WPF to update all bindings
                if (Application.Current.MainWindow != null)
                {
                    Application.Current.MainWindow.UpdateLayout();
                }
            }, DispatcherPriority.Render);
        }
        
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var currentViewName = value as string;
                var buttonViewName = parameter as string;
                
                if (string.Equals(currentViewName, buttonViewName, StringComparison.OrdinalIgnoreCase))
                {
                    // Return the main content background color for selected buttons
                    var brush = Application.Current.Resources["MainContainerBorderBrush"] as SolidColorBrush;
                    return brush?.Color ?? Colors.Transparent;
                }
                
                // Return the default navigation button background color
                var defaultBrush = Application.Current.Resources["NavigationButtonBackground"] as SolidColorBrush;
                return defaultBrush?.Color ?? Colors.Transparent;
            }
            catch
            {
                var defaultBrush = Application.Current.Resources["NavigationButtonBackground"] as SolidColorBrush;
                return defaultBrush?.Color ?? Colors.Transparent;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
