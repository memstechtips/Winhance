using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Winhance.WPF.Features.Common.Controls
{
    /// <summary>
    /// Interaction logic for SearchBox.xaml
    /// </summary>
    public partial class SearchBox : UserControl
    {
        /// <summary>
        /// Dependency property for the SearchText property.
        /// </summary>
        public static readonly DependencyProperty SearchTextProperty =
            DependencyProperty.Register(
                nameof(SearchText),
                typeof(string),
                typeof(SearchBox),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSearchTextChanged));

        /// <summary>
        /// Dependency property for the Placeholder property.
        /// </summary>
        public static readonly DependencyProperty PlaceholderProperty =
            DependencyProperty.Register(
                nameof(Placeholder),
                typeof(string),
                typeof(SearchBox),
                new PropertyMetadata("Search..."));

        /// <summary>
        /// Dependency property for the HasText property.
        /// </summary>
        public static readonly DependencyProperty HasTextProperty =
            DependencyProperty.Register(
                nameof(HasText),
                typeof(bool),
                typeof(SearchBox),
                new PropertyMetadata(false));

        /// <summary>
        /// Initializes a new instance of the <see cref="SearchBox"/> class.
        /// </summary>
        public SearchBox()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Gets or sets the search text.
        /// </summary>
        public string SearchText
        {
            get => (string)GetValue(SearchTextProperty);
            set => SetValue(SearchTextProperty, value);
        }

        /// <summary>
        /// Gets or sets the placeholder text.
        /// </summary>
        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        /// <summary>
        /// Gets a value indicating whether the search box has text.
        /// </summary>
        public bool HasText
        {
            get => (bool)GetValue(HasTextProperty);
            private set => SetValue(HasTextProperty, value);
        }

        /// <summary>
        /// Called when the search text changes.
        /// </summary>
        /// <param name="d">The dependency object.</param>
        /// <param name="e">The event arguments.</param>
        private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SearchBox searchBox)
            {
                searchBox.HasText = !string.IsNullOrEmpty((string)e.NewValue);
            }
        }

        /// <summary>
        /// Handles the click event of the clear button.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event arguments.</param>
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            SearchText = string.Empty;
            SearchTextBox.Focus();
        }
    }
}
