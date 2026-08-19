using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.UI.Features.Common.Helpers;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Extensions;

namespace Winhance.UI.Features.AdvancedTools.ViewModels;

public partial class AutounattendGeneratorViewModel : ObservableObject
{
    private readonly IAutounattendWriter _autounattend;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogService _logService;
    private readonly ISelectionSetBuilder _selections;
    private Window? _mainWindow;

    public string GenerateCardHeader => _localizationService.GetStringOrDefault("Dialog_GenerateXml", "Generate Autounattend XML");

    public string GenerateCardDescription => _localizationService.GetString("AdvancedTools_GenerateCard_Snapshot_Description");

    public string InfoBarTitle => _localizationService.GetStringOrDefault("AdvancedTools_InfoBar_MoreOptionsTitle", "More generation options coming soon");

    public string InfoBarMessage => _localizationService.GetStringOrDefault("AdvancedTools_InfoBar_MoreOptionsMessage", "Additional XML customization options will be available in future updates.");

    public string GenerateButtonText => _localizationService.GetStringOrDefault("WIMUtil_ButtonGenerate", "Generate");

    [ObservableProperty]
    public partial bool IsGenerating { get; set; }

    public event EventHandler? NavigateToWimUtilRequested;

    public AutounattendGeneratorViewModel(
        IAutounattendWriter autounattend,
        IDialogService dialogService,
        ILocalizationService localizationService,
        ILogService logService,
        ISelectionSetBuilder selections)
    {
        _autounattend = autounattend;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _logService = logService;
        _selections = selections;
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
            var confirmMessage = _localizationService.GetString("Msg_GenerateXmlSnapshotConfirm");
            var confirmTitle = _localizationService.GetStringOrDefault("Dialog_GenerateXml", "Generate Autounattend XML");
            var confirmed = (await _dialogService.ShowConfirmationAsync(new ConfirmationRequest { Message = confirmMessage, Title = confirmTitle })).Confirmed;
            if (!confirmed)
                return;

            if (_mainWindow == null)
                return;

            var saveTitle = _localizationService.GetStringOrDefault("AdvancedTools_FileDialog_SaveXml", "Save Autounattend XML File");
            var outputPath = Win32FileDialogHelper.ShowSaveFilePicker(
                _mainWindow,
                saveTitle,
                "XML Files",
                "*.xml",
                "autounattend.xml",
                "xml");

            if (string.IsNullOrEmpty(outputPath))
                return;

            var fileName = Path.GetFileName(outputPath);
            if (!string.Equals(fileName, "autounattend.xml", StringComparison.OrdinalIgnoreCase))
            {
                var invalidMsg = _localizationService.GetString("AdvancedTools_Msg_InvalidFilename");
                await _dialogService.ShowInformationAsync(invalidMsg, _localizationService.GetStringOrDefault("Dialog_Warning", "Warning"));
                return;
            }

            IsGenerating = true;
            try
            {
                var set = await _selections.FromMachineAsync();

                if (set.WindowsApps.Count == 0)
                {
                    var continueAnyway = (await _dialogService.ShowConfirmationAsync(new ConfirmationRequest
                    {
                        Message = _localizationService.GetString("Dialog_NoAppsSelected_Xml_Message"),
                        Title = _localizationService.GetString("Dialog_NoAppsSelected_Title"),
                        ConfirmButtonText = _localizationService.GetStringOrDefault("Button_Yes", "Yes"),
                        CancelButtonText = _localizationService.GetStringOrDefault("Button_No", "No"),
                    })).Confirmed;
                    if (!continueAnyway)
                        return;
                }

                await _autounattend.WriteAsync(set, _selections.CurrentScope, outputPath);
            }
            finally
            {
                IsGenerating = false;
            }

            var successMsg = string.Format(
                _localizationService.GetStringOrDefault("AdvancedTools_Msg_XmlGenSuccess", "XML generated at {0}"),
                outputPath);
            var successTitle = _localizationService.GetStringOrDefault("Dialog_Success", "Success");
            var yesText = _localizationService.GetStringOrDefault("Button_Yes", "Yes");
            var noText = _localizationService.GetStringOrDefault("Button_No", "No");
            var openWimUtil = (await _dialogService.ShowConfirmationAsync(new ConfirmationRequest
            {
                Message = successMsg,
                Title = successTitle,
                ConfirmButtonText = yesText,
                CancelButtonText = noText,
            })).Confirmed;

            if (openWimUtil)
            {
                NavigateToWimUtilRequested?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Error generating autounattend.xml: {ex.Message}");
            var errorMsg = string.Format(
                _localizationService.GetStringOrDefault("AdvancedTools_Msg_XmlGenError", "Failed to generate: {0}"),
                ex.Message);
            var errorTitle = _localizationService.GetStringOrDefault("Dialog_XmlGenError", "Generation Error");
            await _dialogService.ShowErrorAsync(errorMsg, errorTitle);
        }
    }
}
