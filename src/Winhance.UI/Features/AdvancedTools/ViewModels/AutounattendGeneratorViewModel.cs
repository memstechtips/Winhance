using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Helpers;

namespace Winhance.UI.Features.AdvancedTools.ViewModels;

/// <summary>
/// ViewModel for the Autounattend XML Generator page.
/// </summary>
public partial class AutounattendGeneratorViewModel : ObservableObject
{
    private readonly IAutounattendXmlGeneratorService _xmlGeneratorService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogService _logService;
    private Window? _mainWindow;

    /// <summary>
    /// Gets the localized card header text.
    /// </summary>
    public string GenerateCardHeader => _localizationService.GetString("Dialog_GenerateXml") ?? "Generate Autounattend XML";

    public string GenerateCardDescription => _localizationService.GetString("AdvancedTools_GenerateCard_Description") ?? "Generate an autounattend.xml file based on your current Winhance selections to customize Windows during installation.";

    public string InfoBarTitle => _localizationService.GetString("AdvancedTools_InfoBar_MoreOptionsTitle") ?? "More generation options coming soon";

    public string InfoBarMessage => _localizationService.GetString("AdvancedTools_InfoBar_MoreOptionsMessage") ?? "Additional XML customization options will be available in future updates.";

    public string GenerateButtonText => _localizationService.GetString("WIMUtil_ButtonGenerate") ?? "Generate";

    [ObservableProperty]
    private bool _isGenerating;

    /// <summary>
    /// Raised when the user wants to navigate to WimUtil after successful generation.
    /// </summary>
    public event EventHandler? NavigateToWimUtilRequested;

    public AutounattendGeneratorViewModel(
        IAutounattendXmlGeneratorService xmlGeneratorService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        ILogService logService)
    {
        _xmlGeneratorService = xmlGeneratorService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _logService = logService;
    }

    public void SetMainWindow(Window window)
    {
        _mainWindow = window;
    }

    [RelayCommand]
    private async Task GenerateAutounattendXmlAsync()
    {
        try
        {
            // Show confirmation dialog
            var confirmMessage = _localizationService.GetString("Msg_GenerateXmlConfirm");
            var confirmTitle = _localizationService.GetString("Dialog_GenerateXml") ?? "Generate Autounattend XML";
            var confirmed = await _dialogService.ShowConfirmationAsync(confirmMessage, confirmTitle);
            if (!confirmed)
                return;

            // Show save file picker
            if (_mainWindow == null)
                return;

            var saveTitle = _localizationService.GetString("AdvancedTools_FileDialog_SaveXml") ?? "Save Autounattend XML File";
            var outputPath = Win32FileDialogHelper.ShowSaveFilePicker(
                _mainWindow,
                saveTitle,
                "XML Files",
                "*.xml",
                "autounattend.xml",
                "xml");

            if (string.IsNullOrEmpty(outputPath))
                return;

            // Validate filename is autounattend.xml
            var fileName = Path.GetFileName(outputPath);
            if (!string.Equals(fileName, "autounattend.xml", StringComparison.OrdinalIgnoreCase))
            {
                var invalidMsg = _localizationService.GetString("AdvancedTools_Msg_InvalidFilename");
                await _dialogService.ShowInformationAsync(invalidMsg, "Warning");
                return;
            }

            // Generate the XML
            IsGenerating = true;
            try
            {
                await _xmlGeneratorService.GenerateFromCurrentSelectionsAsync(outputPath);
            }
            finally
            {
                IsGenerating = false;
            }

            // Show success dialog with WimUtil option
            var successMsg = string.Format(
                _localizationService.GetString("AdvancedTools_Msg_XmlGenSuccess") ?? "XML generated at {0}",
                outputPath);
            var successTitle = _localizationService.GetString("Dialog_Success") ?? "Success";
            var yesText = _localizationService.GetString("Button_Yes") ?? "Yes";
            var noText = _localizationService.GetString("Button_No") ?? "No";
            var openWimUtil = await _dialogService.ShowConfirmationAsync(successMsg, successTitle, yesText, noText);

            if (openWimUtil)
            {
                NavigateToWimUtilRequested?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error generating autounattend.xml: {ex.Message}");
            var errorMsg = string.Format(
                _localizationService.GetString("AdvancedTools_Msg_XmlGenError") ?? "Failed to generate: {0}",
                ex.Message);
            var errorTitle = _localizationService.GetString("Dialog_XmlGenError") ?? "Generation Error";
            await _dialogService.ShowErrorAsync(errorMsg, errorTitle);
        }
    }
}
