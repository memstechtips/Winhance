using System.Windows;
using System.Windows.Controls;

namespace Winhance.WPF.Features.Common.Utilities
{
    public static class NavigationButtonProperties
    {
        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.RegisterAttached(
                "IsSelected",
                typeof(bool),
                typeof(NavigationButtonProperties),
                new PropertyMetadata(false));

        public static void SetIsSelected(Button button, bool value)
        {
            button.SetValue(IsSelectedProperty, value);
        }

        public static bool GetIsSelected(Button button)
        {
            return (bool)button.GetValue(IsSelectedProperty);
        }

        public static readonly DependencyProperty IsLoadingProperty =
            DependencyProperty.RegisterAttached(
                "IsLoading",
                typeof(bool),
                typeof(NavigationButtonProperties),
                new PropertyMetadata(false));

        public static void SetIsLoading(Button button, bool value)
        {
            button.SetValue(IsLoadingProperty, value);
        }

        public static bool GetIsLoading(Button button)
        {
            return (bool)button.GetValue(IsLoadingProperty);
        }
    }
}
