using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.AdvancedTools.Models;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.AdvancedTools.ViewModels;

/// <summary>
/// Sub-ViewModel for WIM Utility Step 3: driver extraction and injection.
/// </summary>
public partial class WimStep3DriversViewModel : ObservableObject
{
    private readonly IWimCustomizationService _wimCustomizationService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IFileSystemService _fileSystemService;
    private readonly IFilePickerService _filePickerService;
    private readonly ILogService _logService;
    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    /// The working directory, set by the parent when Step 1 completes.
    /// </summary>
    public string WorkingDirectory { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool AreDriversAdded { get; set; }

    public WizardActionCard ExtractSystemDriversCard { get; private set; } = new();
    public WizardActionCard SelectCustomDriversCard { get; private set; } = new();

    public WimStep3DriversViewModel(
        IWimCustomizationService wimCustomizationService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IFileSystemService fileSystemService,
        IFilePickerService filePickerService,
        ILogService logService)
    {
        _wimCustomizationService = wimCustomizationService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _fileSystemService = fileSystemService;
        _filePickerService = filePickerService;
        _logService = logService;

        CreateActionCards();
    }

    private void CreateActionCards()
    {
        ExtractSystemDriversCard = new WizardActionCard
        {
            IconPath = GetResourceIconPath("MemoryArrowDownIconPath"),
            Title = _localizationService.GetString("WIMUtil_Card_ExtractDrivers_Title"),
            Description = _localizationService.GetString("WIMUtil_Card_ExtractDrivers_Description"),
            ButtonText = _localizationService.GetString("WIMUtil_Card_ExtractDrivers_Button"),
            ButtonCommand = ExtractAndAddSystemDriversCommand,
            IsEnabled = true
        };

        SelectCustomDriversCard = new WizardActionCard
        {
            IconPath = GetResourceIconPath("ExplorerIconPath"),
            Title = _localizationService.GetString("WIMUtil_Card_CustomDrivers_Title"),
            Description = _localizationService.GetString("WIMUtil_Card_CustomDrivers_Description"),
            ButtonText = _localizationService.GetString("WIMUtil_Card_CustomDrivers_Button"),
            ButtonCommand = SelectAndAddCustomDriversCommand,
            IsEnabled = true
        };
    }

    [RelayCommand]
    private async Task ExtractAndAddSystemDrivers()
    {
        try
        {
            ExtractSystemDriversCard.IsComplete = false;
            ExtractSystemDriversCard.HasFailed = false;

            var confirmed = await _dialogService.ShowConfirmationAsync(
                _localizationService.GetString("WIMUtil_Msg_ExtractDriversConfirm"),
                _localizationService.GetString("WIMUtil_Card_ExtractDrivers_Title"),
                "Yes", "No");
            if (!confirmed) return;

            ExtractSystemDriversCard.IsProcessing = true;
            ExtractSystemDriversCard.IsEnabled = false;

            _cancellationTokenSource = new CancellationTokenSource();
            var progress = new Progress<TaskProgressDetail>(detail => { });

            var success = await _wimCustomizationService.AddDriversAsync(WorkingDirectory, null, progress, _cancellationTokenSource.Token);

            ExtractSystemDriversCard.IsProcessing = false;
            ExtractSystemDriversCard.IsEnabled = true;

            if (success)
            {
                AreDriversAdded = true;
                ExtractSystemDriversCard.IsComplete = true;
                await _dialogService.ShowInformationAsync(
                    _localizationService.GetString("WIMUtil_Msg_DriversSuccess"),
                    "Success");
            }
            else
            {
                ExtractSystemDriversCard.HasFailed = true;
                await _dialogService.ShowWarningAsync(
                    _localizationService.GetString("WIMUtil_Msg_NoDriversFound"),
                    "Warning");
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error extracting system drivers: {ex.Message}", ex);
            ExtractSystemDriversCard.IsProcessing = false;
            ExtractSystemDriversCard.IsEnabled = true;
            ExtractSystemDriversCard.HasFailed = true;
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_DriverExtractionError"), ex.Message),
                "Error");
        }
    }

    [RelayCommand]
    private async Task SelectAndAddCustomDrivers()
    {
        try
        {
            SelectCustomDriversCard.IsComplete = false;
            SelectCustomDriversCard.HasFailed = false;

            var selectedPath = _filePickerService.PickFolder(_localizationService.GetString("WIMUtil_FolderDialog_SelectDrivers"));
            if (string.IsNullOrEmpty(selectedPath)) return;

            if (!_fileSystemService.DirectoryExists(selectedPath))
            {
                SelectCustomDriversCard.HasFailed = true;
                await _dialogService.ShowErrorAsync(
                    _localizationService.GetString("WIMUtil_Msg_InvalidFolder"),
                    "Error");
                return;
            }

            var hasFiles = _fileSystemService.GetFiles(selectedPath, "*", SearchOption.AllDirectories).Length > 0 || _fileSystemService.GetDirectories(selectedPath, "*", SearchOption.AllDirectories).Length > 0;
            if (!hasFiles)
            {
                SelectCustomDriversCard.HasFailed = true;
                await _dialogService.ShowWarningAsync(
                    _localizationService.GetString("WIMUtil_Msg_EmptyFolder"),
                    "Warning");
                return;
            }

            SelectCustomDriversCard.IsProcessing = true;
            SelectCustomDriversCard.IsEnabled = false;
            SelectCustomDriversCard.Description = $"{_localizationService.GetString("WIMUtil_Label_Selected")}: {selectedPath}";

            _cancellationTokenSource = new CancellationTokenSource();
            var progress = new Progress<TaskProgressDetail>(detail => { });

            var success = await _wimCustomizationService.AddDriversAsync(WorkingDirectory, selectedPath, progress, _cancellationTokenSource.Token);

            SelectCustomDriversCard.IsProcessing = false;
            SelectCustomDriversCard.IsEnabled = true;

            if (success)
            {
                AreDriversAdded = true;
                SelectCustomDriversCard.IsComplete = true;
                await _dialogService.ShowInformationAsync(
                    _localizationService.GetString("WIMUtil_Msg_DriverFilesAdded"),
                    "Success");
            }
            else
            {
                SelectCustomDriversCard.HasFailed = true;
                await _dialogService.ShowWarningAsync(
                    string.Format(_localizationService.GetString("WIMUtil_Msg_NoCustomDrivers"), selectedPath),
                    "Warning");
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error adding custom drivers: {ex.Message}", ex);
            SelectCustomDriversCard.IsProcessing = false;
            SelectCustomDriversCard.IsEnabled = true;
            SelectCustomDriversCard.HasFailed = true;
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_DriverAdditionError"), ex.Message),
                "Error");
        }
    }

    private static string GetResourceIconPath(string resourceKey)
    {
        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(resourceKey, out var value) && value is string path)
            return path;
        return string.Empty;
    }
}
