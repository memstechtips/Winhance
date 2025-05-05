using System.Windows;
using System.Windows.Controls;

namespace Winhance.WPF.Features.Common.Behaviors
{
    /// <summary>
    /// Provides attached properties and behaviors for responsive layouts
    /// </summary>
    public static class ResponsiveLayoutBehavior
    {
        #region ItemWidth Attached Property

        /// <summary>
        /// Gets the ItemWidth value
        /// </summary>
        public static double GetItemWidth(DependencyObject obj)
        {
            return (double)obj.GetValue(ItemWidthProperty);
        }

        /// <summary>
        /// Sets the ItemWidth value
        /// </summary>
        public static void SetItemWidth(DependencyObject obj, double value)
        {
            obj.SetValue(ItemWidthProperty, value);
        }

        /// <summary>
        /// ItemWidth Attached Dependency Property
        /// </summary>
        public static readonly DependencyProperty ItemWidthProperty =
            DependencyProperty.RegisterAttached(
                "ItemWidth",
                typeof(double),
                typeof(ResponsiveLayoutBehavior),
                new PropertyMetadata(200.0, OnItemWidthChanged));

        /// <summary>
        /// Called when ItemWidth is changed
        /// </summary>
        private static void OnItemWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element)
            {
                element.Width = (double)e.NewValue;
            }
        }

        #endregion

        #region MinItemWidth Attached Property

        /// <summary>
        /// Gets the MinItemWidth value
        /// </summary>
        public static double GetMinItemWidth(DependencyObject obj)
        {
            return (double)obj.GetValue(MinItemWidthProperty);
        }

        /// <summary>
        /// Sets the MinItemWidth value
        /// </summary>
        public static void SetMinItemWidth(DependencyObject obj, double value)
        {
            obj.SetValue(MinItemWidthProperty, value);
        }

        /// <summary>
        /// MinItemWidth Attached Dependency Property
        /// </summary>
        public static readonly DependencyProperty MinItemWidthProperty =
            DependencyProperty.RegisterAttached(
                "MinItemWidth",
                typeof(double),
                typeof(ResponsiveLayoutBehavior),
                new PropertyMetadata(150.0, OnMinItemWidthChanged));

        /// <summary>
        /// Called when MinItemWidth is changed
        /// </summary>
        private static void OnMinItemWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element)
            {
                element.MinWidth = (double)e.NewValue;
            }
        }

        #endregion

        #region WrapPanelMaxWidth Attached Property

        /// <summary>
        /// Gets the WrapPanelMaxWidth value
        /// </summary>
        public static double GetWrapPanelMaxWidth(DependencyObject obj)
        {
            return (double)obj.GetValue(WrapPanelMaxWidthProperty);
        }

        /// <summary>
        /// Sets the WrapPanelMaxWidth value
        /// </summary>
        public static void SetWrapPanelMaxWidth(DependencyObject obj, double value)
        {
            obj.SetValue(WrapPanelMaxWidthProperty, value);
        }

        /// <summary>
        /// WrapPanelMaxWidth Attached Dependency Property
        /// </summary>
        public static readonly DependencyProperty WrapPanelMaxWidthProperty =
            DependencyProperty.RegisterAttached(
                "WrapPanelMaxWidth",
                typeof(double),
                typeof(ResponsiveLayoutBehavior),
                new PropertyMetadata(1000.0));

        #endregion

        #region MaxItemWidth Attached Property

        /// <summary>
        /// Gets the MaxItemWidth value
        /// </summary>
        public static double GetMaxItemWidth(DependencyObject obj)
        {
            return (double)obj.GetValue(MaxItemWidthProperty);
        }

        /// <summary>
        /// Sets the MaxItemWidth value
        /// </summary>
        public static void SetMaxItemWidth(DependencyObject obj, double value)
        {
            obj.SetValue(MaxItemWidthProperty, value);
        }

        /// <summary>
        /// MaxItemWidth Attached Dependency Property
        /// </summary>
        public static readonly DependencyProperty MaxItemWidthProperty =
            DependencyProperty.RegisterAttached(
                "MaxItemWidth",
                typeof(double),
                typeof(ResponsiveLayoutBehavior),
                new PropertyMetadata(400.0, OnMaxItemWidthChanged));

        /// <summary>
        /// Called when MaxItemWidth is changed
        /// </summary>
        private static void OnMaxItemWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is FrameworkElement element)
            {
                element.MaxWidth = (double)e.NewValue;
            }
        }

        #endregion

        #region UseWrapPanel Attached Property

        /// <summary>
        /// Gets the UseWrapPanel value
        /// </summary>
        public static bool GetUseWrapPanel(DependencyObject obj)
        {
            return (bool)obj.GetValue(UseWrapPanelProperty);
        }

        /// <summary>
        /// Sets the UseWrapPanel value
        /// </summary>
        public static void SetUseWrapPanel(DependencyObject obj, bool value)
        {
            obj.SetValue(UseWrapPanelProperty, value);
        }

        /// <summary>
        /// UseWrapPanel Attached Dependency Property
        /// </summary>
        public static readonly DependencyProperty UseWrapPanelProperty =
            DependencyProperty.RegisterAttached(
                "UseWrapPanel",
                typeof(bool),
                typeof(ResponsiveLayoutBehavior),
                new PropertyMetadata(false, OnUseWrapPanelChanged));

        /// <summary>
        /// Called when UseWrapPanel is changed
        /// </summary>
        private static void OnUseWrapPanelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ItemsControl itemsControl && (bool)e.NewValue)
            {
                // Get the WrapPanelMaxWidth value or use the default
                double maxWidth = GetWrapPanelMaxWidth(itemsControl);

                // Create a WrapPanel for the ItemsPanel
                var wrapPanel = new WrapPanel
                {
                    Orientation = Orientation.Horizontal,
                    MaxWidth = maxWidth,
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                
                // Add resources to the WrapPanel
                var wrapPanelStyle = new Style(typeof(ContentPresenter));
                wrapPanelStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 5, 10, 5)));
                wrapPanel.Resources.Add(typeof(ContentPresenter), wrapPanelStyle);

                // Set the ItemsPanel template
                var template = new ItemsPanelTemplate();
                template.VisualTree = new FrameworkElementFactory(typeof(WrapPanel));
                template.VisualTree.SetValue(WrapPanel.OrientationProperty, Orientation.Horizontal);
                template.VisualTree.SetValue(WrapPanel.MaxWidthProperty, maxWidth);
                template.VisualTree.SetValue(WrapPanel.HorizontalAlignmentProperty, HorizontalAlignment.Left);
                
                // Add resources to the template
                var resourceDictionary = new ResourceDictionary();
                var contentPresenterStyle = new Style(typeof(ContentPresenter));
                contentPresenterStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 5, 10, 5)));
                resourceDictionary.Add(typeof(ContentPresenter), contentPresenterStyle);
                template.Resources = resourceDictionary;
                
                // Set item spacing through the ItemContainerStyle
                var style = new Style(typeof(ContentPresenter));
                style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 5, 10, 5)));
                style.Setters.Add(new Setter(FrameworkElement.WidthProperty, 250.0));
                style.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, 250.0));
                style.Setters.Add(new Setter(FrameworkElement.MaxWidthProperty, 250.0));
                
                if (itemsControl.ItemContainerStyle == null)
                {
                    itemsControl.ItemContainerStyle = style;
                }

                itemsControl.ItemsPanel = template;
            }
        }

        #endregion
    }
}