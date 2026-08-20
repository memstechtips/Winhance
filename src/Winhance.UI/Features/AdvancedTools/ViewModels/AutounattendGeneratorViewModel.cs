using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Extensions;

namespace Winhance.UI.Features.AdvancedTools.ViewModels;

public partial class AutounattendGeneratorViewModel : ObservableObject
{
    private readonly ISelectionSaveService _saves;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogService _logService;
    private readonly ISelectionSetBuilder _selections;

    public string GenerateCardHeader => _localizationService.GetStringOrDefault("Dialog_GenerateXml", "Generate Autounattend XML");

    public string GenerateCardDescription => _localizationService.GetString("AdvancedTools_GenerateCard_Snapshot_Description");

    public string InfoBarTitle => _localizationService.GetStringOrDefault("AdvancedTools_InfoBar_MoreOptionsTitle", "More generation options coming soon");

    public string InfoBarMessage => _localizationService.GetStringOrDefault("AdvancedTools_InfoBar_MoreOptionsMessage", "Additional XML customization options will be available in future updates.");

    public string GenerateButtonText => _localizationService.GetStringOrDefault("WIMUtil_ButtonGenerate", "Generate");

    [ObservableProperty]
    public partial bool IsGenerating { get; set; }

    public event EventHandler? NavigateToWimUtilRequested;

    public AutounattendGeneratorViewModel(
        ISelectionSaveService saves,
        IDialogService dialogService,
        ILocalizationService localizationService,
        ILogService logService,
        ISelectionSetBuilder selections)
    {
        _saves = saves;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _logService = logService;
        _selections = selections;
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

            SelectionSet set;
            IsGenerating = true;
            try
            {
                set = await _selections.FromMachineAsync();
            }
            finally
            {
                IsGenerating = false;
            }

            SaveOutcome outcome = await _saves.SaveAsync(
                BuilderTarget.Autounattend,
                set,
                new SelectionSaveOptions { OfferWimUtil = true });

            if (outcome.WimUtilRequested)
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
