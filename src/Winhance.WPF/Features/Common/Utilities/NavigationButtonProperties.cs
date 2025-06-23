using System.Windows;
using System.Windows.Controls;

namespace Winhance.WPF.Features.Common.Utilities
{
    /// <summary>
    /// Provides attached properties for navigation buttons
    /// </summary>
    public static class NavigationButtonProperties
    {
        /// <summary>
        /// Attached property for IsSelected
        /// </summary>
        public static readonly DependencyProperty IsSelectedProperty =
            DependencyProperty.RegisterAttached(
                "IsSelected",
                typeof(bool),
                typeof(NavigationButtonProperties),
                new PropertyMetadata(false));

        /// <summary>
        /// Sets the IsSelected property
        /// </summary>
        public static void SetIsSelected(Button button, bool value)
        {
            button.SetValue(IsSelectedProperty, value);
        }

        /// <summary>
        /// Gets the IsSelected property
        /// </summary>
        public static bool GetIsSelected(Button button)
        {
            return (bool)button.GetValue(IsSelectedProperty);
        }
    }
}
