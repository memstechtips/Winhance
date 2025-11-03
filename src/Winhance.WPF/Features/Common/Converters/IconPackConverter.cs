using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MahApps.Metro.IconPacks;

namespace Winhance.WPF.Features.Common.Converters
{
    public class IconPackConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 ||
                values[0] == DependencyProperty.UnsetValue ||
                values[1] == DependencyProperty.UnsetValue)
            {
                return DependencyProperty.UnsetValue;
            }

            var iconPack = values[0]?.ToString() ?? "Material";
            var iconName = values[1]?.ToString();

            if (string.IsNullOrEmpty(iconName))
            {
                return DependencyProperty.UnsetValue;
            }

            try
            {
                return iconPack switch
                {
                    "MaterialDesign" => CreateMaterialDesignIcon(iconName),
                    "Lucide" => CreateLucideIcon(iconName),
                    "Material" or _ => CreateMaterialIcon(iconName)
                };
            }
            catch
            {
                return CreateMaterialIcon(iconName);
            }
        }

        private object CreateMaterialIcon(string iconName)
        {
            if (Enum.TryParse<PackIconMaterialKind>(iconName, out var kind))
            {
                return new PackIconMaterial
                {
                    Kind = kind,
                    Width = 20,
                    Height = 20
                };
            }
            return DependencyProperty.UnsetValue;
        }

        private object CreateMaterialDesignIcon(string iconName)
        {
            if (Enum.TryParse<PackIconMaterialDesignKind>(iconName, out var kind))
            {
                return new PackIconMaterialDesign
                {
                    Kind = kind,
                    Width = 20,
                    Height = 20
                };
            }
            return DependencyProperty.UnsetValue;
        }

        private object CreateLucideIcon(string iconName)
        {
            if (Enum.TryParse<PackIconLucideKind>(iconName, out var kind))
            {
                return new PackIconLucide
                {
                    Kind = kind,
                    Width = 20,
                    Height = 20
                };
            }
            return DependencyProperty.UnsetValue;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
