using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Common.Views
{
    /// <summary>
    /// Interaction logic for UnifiedConfigurationDialog.xaml
    /// </summary>
    public partial class UnifiedConfigurationDialog : Window
    {
        private readonly UnifiedConfigurationDialogViewModel _viewModel;
        private readonly ILogService _logService;

        /// <summary>
        /// Initializes a new instance of the <see cref="UnifiedConfigurationDialog"/> class.
        /// </summary>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="description">The description of the dialog.</param>
        /// <param name="sections">The dictionary of section names, their availability, and item counts.</param>
        /// <param name="isSaveDialog">Whether this is a save dialog (true) or an import dialog (false).</param>
        public UnifiedConfigurationDialog(
            string title,
            string description,
            Dictionary<string, (bool IsSelected, bool IsAvailable, int ItemCount)> sections,
            bool isSaveDialog)
        {
            try
            {
                InitializeComponent();

                // Try to get the log service from the application using reflection
                try
                {
                    if (Application.Current is App appInstance)
                    {
                        // Use reflection to access the _host.Services property
                        var hostField = appInstance.GetType().GetField("_host", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (hostField != null)
                        {
                            var host = hostField.GetValue(appInstance);
                            var servicesProperty = host.GetType().GetProperty("Services");
                            if (servicesProperty != null)
                            {
                                var services = servicesProperty.GetValue(host);
                                var getServiceMethod = services.GetType().GetMethod("GetService", new[] { typeof(Type) });
                                if (getServiceMethod != null)
                                {
                                    _logService = getServiceMethod.Invoke(services, new object[] { typeof(ILogService) }) as ILogService;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error getting log service: {ex.Message}");
                    // Continue without logging
                }
                
                LogInfo($"Creating {(isSaveDialog ? "save" : "import")} dialog with title: {title}");
                LogInfo($"Sections: {string.Join(", ", sections.Keys)}");

                // Create the view model
                _viewModel = new UnifiedConfigurationDialogViewModel(title, description, sections, isSaveDialog);
                
                // Set the data context
                DataContext = _viewModel;

                // Set the window title
                this.Title = title;
                
                // Ensure the dialog is shown as a modal dialog
                this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                this.ResizeMode = ResizeMode.NoResize;
                this.ShowInTaskbar = false;
                
                // Handle the OK and Cancel commands directly
                _viewModel.OkCommand = new RelayCommand(() =>
                {
                    // Validate that at least one section is selected
                    if (_viewModel.Sections.Any(s => s.IsSelected))
                    {
                        LogInfo("OK button clicked, at least one section selected");
                        this.DialogResult = true;
                    }
                    else
                    {
                        LogInfo("OK button clicked, but no sections selected");
                        MessageBox.Show(
                            "Please select at least one section to continue.",
                            "No Sections Selected",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                });
                
                _viewModel.CancelCommand = new RelayCommand(() =>
                {
                    LogInfo("Cancel button clicked");
                    this.DialogResult = false;
                });
                
                LogInfo("Dialog initialization completed");
            }
            catch (Exception ex)
            {
                LogError($"Error initializing dialog: {ex.Message}");
                Debug.WriteLine($"Error initializing dialog: {ex}");
                
                // Show error message
                MessageBox.Show($"Error initializing dialog: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // Set dialog result to false
                DialogResult = false;
            }
        }

        /// <summary>
        /// Gets the result of the dialog as a dictionary of section names and their selection state.
        /// </summary>
        /// <returns>A dictionary of section names and their selection state.</returns>
        public Dictionary<string, bool> GetResult()
        {
            try
            {
                var result = _viewModel.GetResult();
                LogInfo($"GetResult called, returning {result.Count} sections");
                return result;
            }
            catch (Exception ex)
            {
                LogError($"Error getting result: {ex.Message}");
                return new Dictionary<string, bool>();
            }
        }
        
        private void LogInfo(string message)
        {
            _logService?.Log(LogLevel.Info, $"UnifiedConfigurationDialog: {message}");
            Debug.WriteLine($"UnifiedConfigurationDialog: {message}");
        }
        
        private void LogError(string message)
        {
            _logService?.Log(LogLevel.Error, $"UnifiedConfigurationDialog: {message}");
            Debug.WriteLine($"UnifiedConfigurationDialog ERROR: {message}");
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
            LogInfo("Close button clicked");
            this.DialogResult = false;
        }
    }
}