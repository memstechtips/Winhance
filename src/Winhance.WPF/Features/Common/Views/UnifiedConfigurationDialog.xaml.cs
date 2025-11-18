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
using Winhance.Core.Features.Common.Models;

namespace Winhance.WPF.Features.Common.Views
{
    /// <summary>
    /// Interaction logic for UnifiedConfigurationDialog.xaml
    /// </summary>
    public partial class UnifiedConfigurationDialog : Window
    {
        private readonly UnifiedConfigurationDialogViewModel _viewModel;
        private readonly ILogService _logService;

        public UnifiedConfigurationDialog(
            string title,
            string description,
            Dictionary<string, (bool IsSelected, bool IsAvailable, int ItemCount)> sections,
            bool isSaveDialog
        )
        {
            try
            {
                InitializeComponent();

                try
                {
                    if (Application.Current is App appInstance)
                    {
                        var hostField = appInstance
                            .GetType()
                            .GetField(
                                "_host",
                                System.Reflection.BindingFlags.NonPublic
                                    | System.Reflection.BindingFlags.Instance
                            );
                        if (hostField != null)
                        {
                            var host = hostField.GetValue(appInstance);
                            var servicesProperty = host.GetType().GetProperty("Services");
                            if (servicesProperty != null)
                            {
                                var services = servicesProperty.GetValue(host);
                                var getServiceMethod = services
                                    .GetType()
                                    .GetMethod("GetService", new[] { typeof(Type) });
                                if (getServiceMethod != null)
                                {
                                    _logService =
                                        getServiceMethod.Invoke(
                                            services,
                                            new object[] { typeof(ILogService) }
                                        ) as ILogService;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                }

                LogInfo(
                    $"Creating {(isSaveDialog ? "save" : "import")} dialog with title: {title}"
                );
                LogInfo($"Sections: {string.Join(", ", sections.Keys)}");

                _viewModel = new UnifiedConfigurationDialogViewModel(
                    title,
                    description,
                    sections,
                    isSaveDialog
                );

                DataContext = _viewModel;
                this.Title = title;
                this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                this.ResizeMode = ResizeMode.NoResize;
                this.ShowInTaskbar = false;

                this.Loaded += (s, e) =>
                {
                    if (Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
                    {
                        mainViewModel.IsDialogOverlayVisible = true;
                    }
                };

                this.Closed += (s, e) =>
                {
                    if (Application.Current.MainWindow?.DataContext is MainViewModel mainViewModel)
                    {
                        mainViewModel.IsDialogOverlayVisible = false;
                    }
                };

                _viewModel.OkCommand = new RelayCommand(() =>
                {
                    bool hasSelectedSection = _viewModel.Sections.Any(s =>
                        s.IsSelected
                        || (s.HasSubSections && s.SubSections.Any(sub =>
                            sub.IsSelected
                            || (sub.HasActionOptions && sub.ActionOptions.Any(a => a.IsSelected))
                        ))
                    );

                    if (hasSelectedSection)
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
                            MessageBoxImage.Warning
                        );
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

                MessageBox.Show(
                    $"Error initializing dialog: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                DialogResult = false;
            }
        }

        public (Dictionary<string, bool> sections, ImportOptions options) GetResult()
        {
            try
            {
                var (sections, options) = _viewModel.GetResult();
                var selectedSections = sections.Where(r => r.Value).Select(r => r.Key).ToList();
                LogInfo(
                    $"GetResult called, returning {sections.Count} sections, selected: {string.Join(", ", selectedSections)}"
                );
                return (sections, options);
            }
            catch (Exception ex)
            {
                LogError($"Error getting result: {ex.Message}");
                return (new Dictionary<string, bool>(), new ImportOptions());
            }
        }

        private void LogInfo(string message)
        {
            _logService?.Log(LogLevel.Info, $"UnifiedConfigurationDialog: {message}");
        }

        private void LogError(string message)
        {
            _logService?.Log(LogLevel.Error, $"UnifiedConfigurationDialog: {message}");
        }

        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            LogInfo("Close button clicked");
            this.DialogResult = false;
        }
    }
}
