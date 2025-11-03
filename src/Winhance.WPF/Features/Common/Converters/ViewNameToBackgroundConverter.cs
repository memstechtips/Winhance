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

        public void NotifyThemeChanged()
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            }, DispatcherPriority.Background);
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
