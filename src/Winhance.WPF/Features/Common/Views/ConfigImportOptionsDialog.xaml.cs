using System;
using System.Windows;
using System.Windows.Input;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.WPF.Features.Common.Views
{
    /// <summary>
    /// Interaction logic for ConfigImportOptionsDialog.xaml
    /// </summary>
    public partial class ConfigImportOptionsDialog : Window
    {
        /// <summary>
        /// Gets the selected import option.
        /// </summary>
        public ImportOption SelectedOption { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigImportOptionsDialog"/> class.
        /// </summary>
        public ConfigImportOptionsDialog()
        {
            InitializeComponent();

            // Set the default selected option to None
            SelectedOption = ImportOption.None;
        }

        /// <summary>
        /// Handles the mouse left button down event on the title bar to enable window dragging.
        /// </summary>
        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        /// <summary>
        /// Handles the close button click event.
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        /// <summary>
        /// Handles the import own config button click event.
        /// </summary>
        private void ImportOwnConfig_Click(object sender, RoutedEventArgs e)
        {
            SelectedOption = ImportOption.ImportOwn;
            this.DialogResult = true;
            this.Close();
        }

        /// <summary>
        /// Handles the import recommended config button click event.
        /// </summary>
        private void ImportRecommendedConfig_Click(object sender, RoutedEventArgs e)
        {
            SelectedOption = ImportOption.ImportRecommended;
            this.DialogResult = true;
            this.Close();
        }
    }
}
