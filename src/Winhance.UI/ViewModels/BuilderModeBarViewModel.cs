using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.ViewModels;

/// <summary>
/// Child ViewModel for the Builder mode bar in the main window. Shown while the app is
/// authoring a config or autounattend.xml from the UI without applying to the system.
/// Hosts the Config/Autounattend target toggle plus Save and Cancel.
/// </summary>
public partial class BuilderModeBarViewModel : ObservableObject, IDisposable
{
    private bool _disposed;
    private readonly IApplicationModeService _applicationModeService;
    private readonly IConfigExportService _configExportService;
    private readonly IDispatcherService _dispatcherService;
    private readonly ILocalizationService _localizationService;
    private readonly IDialogService _dialogService;
    private readonly ILogService _logService;

    [ObservableProperty]
    public partial bool IsBuilderActive { get; set; }

    public bool IsConfigTarget => _applicationModeService.CurrentBuilderTarget == BuilderTarget.Config;
    public bool IsAutounattendTarget => _applicationModeService.CurrentBuilderTarget == BuilderTarget.Autounattend;

    public string BuilderModeTitleText =>
        _localizationService.GetString("Builder_Mode_Title") ?? "Builder Mode";

    public string BuilderModeDescriptionText =>
        _localizationService.GetString("Builder_Mode_Description")
            ?? "You're authoring a file from these settings — nothing here changes this PC. Choose a target, set your options, then Save.";

    public string BuilderConfigTargetText =>
        _localizationService.GetString("Builder_Mode_Target_Config") ?? "Config";

    public string BuilderAutounattendTargetText =>
        _localizationService.GetString("Builder_Mode_Target_Autounattend") ?? "Autounattend";

    public string BuilderSaveButtonText =>
        IsAutounattendTarget
            ? (_localizationService.GetString("Builder_Mode_Save_Autounattend") ?? "Save autounattend.xml")
            : (_localizationService.GetString("Builder_Mode_Save_Config") ?? "Save Config");

    public string BuilderCancelButtonText =>
        _localizationService.GetString("Builder_Mode_Cancel_Button") ?? "Cancel";

    public BuilderModeBarViewModel(
        IApplicationModeService applicationModeService,
        IConfigExportService configExportService,
        IDispatcherService dispatcherService,
        ILocalizationService localizationService,
        IDialogService dialogService,
        ILogService logService)
    {
        _applicationModeService = applicationModeService;
        _configExportService = configExportService;
        _dispatcherService = dispatcherService;
        _localizationService = localizationService;
        _dialogService = dialogService;
        _logService = logService;

        _applicationModeService.ModeChanged += OnModeChanged;
        _localizationService.LanguageChanged += OnLanguageChanged;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _applicationModeService.ModeChanged -= OnModeChanged;
        _localizationService.LanguageChanged -= OnLanguageChanged;
    }

    private void OnModeChanged(object? sender, EventArgs e)
    {
        _dispatcherService.RunOnUIThreadAsync(() =>
        {
            IsBuilderActive = _applicationModeService.CurrentMode == WinhanceMode.Builder;
            OnPropertyChanged(nameof(IsConfigTarget));
            OnPropertyChanged(nameof(IsAutounattendTarget));
            OnPropertyChanged(nameof(BuilderSaveButtonText));
            return Task.CompletedTask;
        }).FireAndForget(_logService);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(BuilderModeTitleText));
        OnPropertyChanged(nameof(BuilderModeDescriptionText));
        OnPropertyChanged(nameof(BuilderConfigTargetText));
        OnPropertyChanged(nameof(BuilderAutounattendTargetText));
        OnPropertyChanged(nameof(BuilderSaveButtonText));
        OnPropertyChanged(nameof(BuilderCancelButtonText));
    }

    /// <summary>Switch the Builder target to Config (preserves authored selections).</summary>
    public void SelectConfigTarget()
    {
        _applicationModeService.SetBuilderTarget(BuilderTarget.Config);
        OnPropertyChanged(nameof(IsConfigTarget));
        OnPropertyChanged(nameof(IsAutounattendTarget));
        OnPropertyChanged(nameof(BuilderSaveButtonText));
    }

    /// <summary>Switch the Builder target to Autounattend (preserves authored selections).</summary>
    public void SelectAutounattendTarget()
    {
        _applicationModeService.SetBuilderTarget(BuilderTarget.Autounattend);
        OnPropertyChanged(nameof(IsConfigTarget));
        OnPropertyChanged(nameof(IsAutounattendTarget));
        OnPropertyChanged(nameof(BuilderSaveButtonText));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            if (_applicationModeService.CurrentBuilderTarget == BuilderTarget.Autounattend)
            {
                await _configExportService.ExportBuilderAutounattendAsync();
            }
            else
            {
                await _configExportService.ExportBuilderConfigAsync();
            }
        }
        catch (Exception ex)
        {
            _logService.Log(LogLevel.Error, $"Builder Save failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        var title = _localizationService.GetString("Builder_Mode_Cancel_Confirmation_Title") ?? "Leave Builder Mode";
        var message = _localizationService.GetString("Builder_Mode_Cancel_Confirmation")
            ?? "Leave Builder mode? Your authored selections will be discarded (nothing was applied to this PC).";

        var confirmed = (await _dialogService.ShowConfirmationAsync(
            new ConfirmationRequest { Message = message, Title = title })).Confirmed;
        if (confirmed)
        {
            _applicationModeService.EnterNormalMode();
        }
    }
}
