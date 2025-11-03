using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace Winhance.WPF.Features.Common.Controls
{
    public partial class SearchBox : UserControl
    {
        public static readonly DependencyProperty SearchTextProperty =
            DependencyProperty.Register(
                nameof(SearchText),
                typeof(string),
                typeof(SearchBox),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSearchTextChanged));

        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register(
                nameof(Placeholder),
                typeof(string),
                typeof(SearchBox),
                new PropertyMetadata("Search..."));

        public static readonly DependencyProperty HasTextProperty =
            DependencyProperty.Register(
                nameof(HasText),
                typeof(bool),
                typeof(SearchBox),
                new PropertyMetadata(false));

        public SearchBox()
        {
            InitializeComponent();
            Loaded += SearchBox_Loaded;
            Unloaded += SearchBox_Unloaded;
        }

        private void SearchBox_Loaded(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.PreviewMouseDown += Window_PreviewMouseDown;
            }
        }

        private void SearchBox_Unloaded(object sender, RoutedEventArgs e)
        {
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.PreviewMouseDown -= Window_PreviewMouseDown;
            }
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (SearchTextBox.IsFocused)
            {
                var clickedElement = e.OriginalSource as DependencyObject;
                if (clickedElement != null && !IsDescendantOf(clickedElement, this))
                {
                    var window = Window.GetWindow(this);
                    if (window != null)
                    {
                        FocusManager.SetFocusedElement(window, null);
                    }
                    Keyboard.ClearFocus();
                }
            }
        }

        private bool IsDescendantOf(DependencyObject child, DependencyObject parent)
        {
            var current = child;
            while (current != null)
            {
                if (current == parent)
                    return true;
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return false;
        }

        public string SearchText
        {
            get => (string)GetValue(SearchTextProperty);
            set => SetValue(SearchTextProperty, value);
        }

        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        public bool HasText
        {
            get => (bool)GetValue(HasTextProperty);
            private set => SetValue(HasTextProperty, value);
        }

        private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SearchBox searchBox)
            {
                searchBox.HasText = !string.IsNullOrEmpty((string)e.NewValue);
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            SearchText = string.Empty;

            var window = Window.GetWindow(this);
            if (window != null)
            {
                FocusManager.SetFocusedElement(window, null);
            }

            Keyboard.ClearFocus();
            SearchTextBox.IsEnabled = false;
            SearchTextBox.IsEnabled = true;
        }

        private void SearchTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            Keyboard.ClearFocus();
        }
    }
}
