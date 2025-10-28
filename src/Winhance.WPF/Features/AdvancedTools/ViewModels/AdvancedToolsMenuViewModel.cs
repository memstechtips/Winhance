using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.WPF.Features.Common.Views;

namespace Winhance.WPF.Features.AdvancedTools.ViewModels
{
    public partial class AdvancedToolsMenuViewModel : ObservableObject
    {
        private readonly ILogService _logService;
        private readonly INavigationService _navigationService;
        private readonly IAutounattendXmlGeneratorService _xmlGeneratorService;
        private readonly IDialogService _dialogService;

        public AdvancedToolsMenuViewModel(
            ILogService logService,
            INavigationService navigationService,
            IAutounattendXmlGeneratorService xmlGeneratorService,
            IDialogService dialogService)
        {
            _logService = logService;
            _navigationService = navigationService;
            _xmlGeneratorService = xmlGeneratorService;
            _dialogService = dialogService;
        }

        [RelayCommand]
        private void NavigateToWimUtil()
        {
            try
            {
                _logService.LogInformation("Navigating to WIMUtil");
                CloseFlyout();
                _navigationService.NavigateTo("WimUtil");
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error navigating to WIMUtil: {ex.Message}", ex);
            }
        }

        [RelayCommand]
        private async Task NavigateToXmlGeneratorAsync()
        {
            try
            {
                _logService.LogInformation("Starting autounattend.xml generation");
                CloseFlyout();
                await GenerateAutounattendXmlAsync();
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error generating XML: {ex.Message}", ex);
            }
        }

        private async Task GenerateAutounattendXmlAsync()
        {
            var explanation =
                "You can generate an autounattend.xml file to add to a Windows ISO to customize Windows during installation based on your selections in Winhance.\n\n" +
                "How this works:\n" +
                "• Apps selected on the Windows Apps screen will be uninstalled automatically\n" +
                "• Settings in the Optimize and Customize screens will be added according to their current state in Winhance\n\n" +
                "If you're sure your selections are correct you can continue. If not, hit cancel, review your app and setting selections, and come back here.";

            var confirmDialog = CustomDialog.CreateConfirmationDialog(
                "Generate Autounattend XML",
                explanation,
                "",
                DialogType.Question,
                "HelpCircle"
            );
            confirmDialog.PrimaryButton.Content = "Continue";
            confirmDialog.SecondaryButton.Content = "Cancel";

            var continueResult = confirmDialog.ShowDialog();
            if (continueResult != true)
            {
                _logService.Log(LogLevel.Info, "Autounattend.xml generation canceled by user");
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                FileName = "autounattend.xml",
                Filter = "Autounattend Files (autounattend.xml)|autounattend.xml|All Files|*.*",
                Title = "Save Autounattend XML File",
                DefaultExt = ".xml",
                OverwritePrompt = true,
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            if (saveFileDialog.ShowDialog() != true)
            {
                _logService.Log(LogLevel.Info, "Save dialog canceled by user");
                return;
            }

            var fileName = Path.GetFileName(saveFileDialog.FileName);
            if (!fileName.Equals("autounattend.xml", StringComparison.OrdinalIgnoreCase))
            {
                var errorDialog = CustomDialog.CreateInformationDialog(
                    "Invalid Filename",
                    "The file must be named 'autounattend.xml' to work properly with Windows installation.",
                    "Please save the file with the exact name: autounattend.xml",
                    DialogType.Warning,
                    "AlertCircle"
                );
                errorDialog.ShowDialog();
                return;
            }

            try
            {
                _logService.Log(LogLevel.Info, $"Generating autounattend.xml to: {saveFileDialog.FileName}");

                var outputPath = await _xmlGeneratorService.GenerateFromCurrentSelectionsAsync(
                    saveFileDialog.FileName
                );

                var successMessage =
                    $"Your autounattend.xml file has been created!\n\n" +
                    $"Location: {outputPath}\n\n" +
                    $"Next steps:\n" +
                    $"1. Use the WIMUtil feature to add this file to a Windows ISO\n" +
                    $"2. Or manually copy it to the root of your Windows installation media\n" +
                    $"3. Install Windows using the modified media\n\n" +
                    $"Would you like to use the WIMUtil to create a Windows installation ISO with this file now?";

                var useWimUtilDialog = CustomDialog.CreateConfirmationDialog(
                    "XML Generated Successfully",
                    successMessage,
                    "",
                    DialogType.Success,
                    "CheckCircle"
                );
                useWimUtilDialog.PrimaryButton.Content = "Open WIMUtil";
                useWimUtilDialog.SecondaryButton.Content = "I'll Do It Later";

                var useWimUtilResult = useWimUtilDialog.ShowDialog();
                if (useWimUtilResult == true)
                {
                    _navigationService.NavigateTo("WimUtil");
                }

                _logService.Log(LogLevel.Info, "Autounattend.xml generated successfully");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error generating autounattend.xml: {ex.Message}\n{ex.StackTrace}");

                var errorDialog = CustomDialog.CreateInformationDialog(
                    "Generation Error",
                    $"Failed to generate autounattend.xml file.",
                    $"Error: {ex.Message}\n\nPlease check the log file for more details.",
                    DialogType.Error,
                    "CloseCircle"
                );
                errorDialog.ShowDialog();
            }
        }

        private void CloseFlyout()
        {
            try
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow?.DataContext is MainViewModel mainViewModel)
                {
                    mainViewModel.CloseAdvancedToolsFlyout();
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error closing flyout: {ex.Message}", ex);
            }
        }
    }
}
