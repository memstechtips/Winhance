using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.Views;

namespace Winhance.WPF.Features.Common.Services
{
    /// <summary>
    /// Service for handling UI-related operations for configuration management.
    /// </summary>
    public class ConfigurationUIService
    {
        private readonly ILogService _logService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationUIService"/> class.
        /// </summary>
        /// <param name="logService">The log service.</param>
        public ConfigurationUIService(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
        }

        /// <summary>
        /// Shows the unified configuration dialog to let the user select which sections to include.
        /// </summary>
        /// <param name="config">The unified configuration file.</param>
        /// <param name="isSaveDialog">Whether this is a save dialog (true) or an import dialog (false).</param>
        /// <returns>A dictionary of section names and their selection state.</returns>
        public async Task<Dictionary<string, bool>> ShowUnifiedConfigurationDialogAsync(UnifiedConfigurationFile config, bool isSaveDialog)
        {
            try
            {
                _logService.Log(LogLevel.Info, $"Showing unified configuration dialog (isSaveDialog: {isSaveDialog})");

                // Create a dictionary of sections with their availability and item counts
                var sectionInfo = new Dictionary<string, (bool IsSelected, bool IsAvailable, int ItemCount)>
                {
                    { "WindowsApps", (true, config.WindowsApps.Items.Count > 0, config.WindowsApps.Items.Count) },
                    { "ExternalApps", (true, config.ExternalApps.Items.Count > 0, config.ExternalApps.Items.Count) },
                    { "Customize", (true, config.Customize.Items.Count > 0, config.Customize.Items.Count) },
                    { "Optimize", (true, config.Optimize.Items.Count > 0, config.Optimize.Items.Count) }
                };

                // Create and show the dialog
                var dialog = new UnifiedConfigurationDialog(
                    isSaveDialog ? "Save Configuration" : "Select Configuration Sections",
                    isSaveDialog ? "Select which sections you want to save to the unified configuration." : "Select which sections you want to import from the unified configuration.",
                    sectionInfo,
                    isSaveDialog);

                dialog.Owner = Application.Current.MainWindow;
                bool? dialogResult = dialog.ShowDialog();

                if (dialogResult != true)
                {
                    _logService.Log(LogLevel.Info, "User canceled unified configuration dialog");
                    return new Dictionary<string, bool>();
                }

                // Get the selected sections from the dialog
                var result = dialog.GetResult();

                _logService.Log(LogLevel.Info, $"Selected sections: {string.Join(", ", result.Where(kvp => kvp.Value).Select(kvp => kvp.Key))}");

                return result;
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error showing unified configuration dialog: {ex.Message}");
                return new Dictionary<string, bool>();
            }
        }

        /// <summary>
        /// Shows a success message for configuration operations.
        /// </summary>
        /// <param name="title">The title of the message.</param>
        /// <param name="message">The message to show.</param>
        /// <param name="sections">The sections that were affected.</param>
        /// <param name="additionalInfo">Additional information to show.</param>
        public void ShowSuccessMessage(string title, string message, List<string> sections, string additionalInfo)
        {
            CustomDialog.ShowInformation(title, message, sections, additionalInfo);
        }

        /// <summary>
        /// Shows an error message for configuration operations.
        /// </summary>
        /// <param name="title">The title of the message.</param>
        /// <param name="message">The message to show.</param>
        public void ShowErrorMessage(string title, string message)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// Shows a confirmation dialog for configuration operations.
        /// </summary>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="message">The message to show.</param>
        /// <returns>True if the user confirmed, false otherwise.</returns>
        public bool ShowConfirmationDialog(string title, string message)
        {
            var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }
    }
}