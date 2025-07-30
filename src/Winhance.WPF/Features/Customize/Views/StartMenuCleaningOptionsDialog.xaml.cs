using System.Windows;

namespace Winhance.WPF.Features.Customize.Views
{
    /// <summary>
    /// Dialog for Start Menu cleaning options.
    /// </summary>
    public partial class StartMenuCleaningOptionsDialog : Window
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StartMenuCleaningOptionsDialog"/> class.
        /// </summary>
        public StartMenuCleaningOptionsDialog()
        {
            InitializeComponent();
            DataContext = this;
        }

        /// <summary>
        /// Handles the Continue button click event.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event arguments.</param>
        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// Handles the Cancel button click event.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The event arguments.</param>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Shows the Start Menu cleaning options dialog.
        /// </summary>
        /// <returns>True if the user confirmed the action, false otherwise.</returns>
        public static bool ShowOptionsDialog()
        {
            var dialog = new StartMenuCleaningOptionsDialog();
            var result = dialog.ShowDialog();
            
            return result == true;
        }
    }
}
