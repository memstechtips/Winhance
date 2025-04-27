using Microsoft.Win32;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Winhance.WPF.Features.Common.Converters
{
    /// <summary>
    /// Converts a RegistryHive enum to its full string representation (HKEY_CURRENT_USER, HKEY_LOCAL_MACHINE, etc.)
    /// </summary>
    public class RegistryHiveToFullNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is RegistryHive hive)
            {
                return hive switch
                {
                    RegistryHive.ClassesRoot => "HKEY_CLASSES_ROOT",
                    RegistryHive.CurrentUser => "HKEY_CURRENT_USER",
                    RegistryHive.LocalMachine => "HKEY_LOCAL_MACHINE",
                    RegistryHive.Users => "HKEY_USERS",
                    RegistryHive.CurrentConfig => "HKEY_CURRENT_CONFIG",
                    _ => value.ToString()
                };
            }
            
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
