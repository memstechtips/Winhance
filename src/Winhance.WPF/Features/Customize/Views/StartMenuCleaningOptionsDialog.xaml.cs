using System.Windows;

namespace Winhance.WPF.Features.Customize.Views
{
    /// <summary>
    /// Dialog for Start Menu cleaning options.
    /// </summary>
    public partial class StartMenuCleaningOptionsDialog : Window
    {
        /// <summary>
        /// Gets a value indicating whether the user selected to apply cleaning to all existing user accounts.
        /// </summary>
        public bool ApplyToAllUsers { get; private set; }

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
            ApplyToAllUsers = ApplyToAllUsersCheckBox.IsChecked == true;
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
        /// <returns>A tuple containing whether the user confirmed the action and whether to apply to all users.</returns>
        public static (bool Confirmed, bool ApplyToAllUsers) ShowOptionsDialog()
        {
            var dialog = new StartMenuCleaningOptionsDialog();
            var result = dialog.ShowDialog();
            
            return (result == true, dialog.ApplyToAllUsers);
        }
    }
}
