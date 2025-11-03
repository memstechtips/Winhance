using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace Winhance.WPF.Converters
{
    public class WindowStateToCommandConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is WindowState windowState)
            {
                return windowState == WindowState.Maximized
                    ? SystemCommands.RestoreWindowCommand
                    : SystemCommands.MaximizeWindowCommand;
            }

            return SystemCommands.MaximizeWindowCommand;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}