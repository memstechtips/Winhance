using System;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Winhance.WPF.Features.Common.Resources.Theme;

namespace Winhance.WPF.Features.Common.Services
{
    public class WindowIconService
    {
        private readonly IThemeManager _themeManager;

        public WindowIconService(IThemeManager themeManager)
        {
            _themeManager = themeManager;
        }

        public void UpdateTitleBarIcon(Image iconImage)
        {
            if (iconImage == null) return;

            try
            {
                string iconPath = _themeManager.IsDarkTheme
                    ? "pack://application:,,,/Resources/AppIcons/winhance-rocket-white-transparent-bg.ico"
                    : "pack://application:,,,/Resources/AppIcons/winhance-rocket-black-transparent-bg.ico";

                var image = new BitmapImage(new Uri(iconPath, UriKind.Absolute));
                image.Freeze();
                iconImage.Source = image;
            }
            catch
            {
                try
                {
                    var defaultImage = new BitmapImage(new Uri(
                        "pack://application:,,,/Resources/AppIcons/winhance-rocket.ico",
                        UriKind.Absolute));
                    defaultImage.Freeze();
                    iconImage.Source = defaultImage;
                }
                catch
                {
                    // Silently fail
                }
            }
        }
    }
}
