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
        private readonly ILocalizationService _localizationService;

        public AdvancedToolsMenuViewModel(
            ILogService logService,
            INavigationService navigationService,
            IAutounattendXmlGeneratorService xmlGeneratorService,
            IDialogService dialogService,
            ILocalizationService localizationService)
        {
            _logService = logService;
            _navigationService = navigationService;
            _xmlGeneratorService = xmlGeneratorService;
            _dialogService = dialogService;
            _localizationService = localizationService;
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
            var confirmed = _dialogService.ShowLocalizedConfirmationDialog(
                "Dialog_GenerateXml",
                "Msg_GenerateXmlConfirm",
                DialogType.Question,
                "HelpCircle"
            );

            if (!confirmed)
            {
                _logService.Log(LogLevel.Info, "Autounattend.xml generation canceled by user");
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                FileName = "autounattend.xml",
                Filter = "Autounattend Files (autounattend.xml)|autounattend.xml|All Files|*.*",
                Title = _localizationService.GetString("AdvancedTools_FileDialog_SaveXml"),
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
                _dialogService.ShowLocalizedDialog(
                    "Dialog_InvalidFilename",
                    "AdvancedTools_Msg_InvalidFilename",
                    DialogType.Warning,
                    "AlertCircle"
                );
                return;
            }

            try
            {
                _logService.Log(LogLevel.Info, $"Generating autounattend.xml to: {saveFileDialog.FileName}");

                var outputPath = await _xmlGeneratorService.GenerateFromCurrentSelectionsAsync(
                    saveFileDialog.FileName
                );

                var useWimUtil = _dialogService.ShowLocalizedConfirmationDialog(
                    "Dialog_XmlGenSuccess",
                    "AdvancedTools_Msg_XmlGenSuccess",
                    DialogType.Success,
                    "CheckCircle",
                    outputPath
                );

                if (useWimUtil)
                {
                    _navigationService.NavigateTo("WimUtil");
                }

                _logService.Log(LogLevel.Info, "Autounattend.xml generated successfully");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error, $"Error generating autounattend.xml: {ex.Message}\n{ex.StackTrace}");

                _dialogService.ShowLocalizedDialog(
                    "Dialog_XmlGenError",
                    "AdvancedTools_Msg_XmlGenError",
                    DialogType.Error,
                    "CloseCircle",
                    ex.Message
                );
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
