using System;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.AdvancedTools.Models;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.AdvancedTools.ViewModels;

/// <summary>
/// Sub-ViewModel for WIM Utility Step 2: XML generation, download, and selection.
/// </summary>
public partial class WimStep2XmlViewModel : ObservableObject, IDisposable
{
    private readonly IAutounattendXmlGeneratorService _xmlGeneratorService;
    private readonly IWimCustomizationService _wimCustomizationService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IFileSystemService _fileSystemService;
    private readonly IFilePickerService _filePickerService;
    private readonly ILogService _logService;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _disposed;

    /// <summary>
    /// The working directory, set by the parent when Step 1 completes.
    /// </summary>
    public string WorkingDirectory { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedXmlPath { get; set; }

    [ObservableProperty]
    public partial string XmlStatus { get; set; }

    [ObservableProperty]
    public partial bool IsXmlAdded { get; set; }

    public WizardActionCard GenerateWinhanceXmlCard { get; private set; } = new();
    public WizardActionCard DownloadXmlCard { get; private set; } = new();
    public WizardActionCard SelectXmlCard { get; private set; } = new();

    public WimStep2XmlViewModel(
        IAutounattendXmlGeneratorService xmlGeneratorService,
        IWimCustomizationService wimCustomizationService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IFileSystemService fileSystemService,
        IFilePickerService filePickerService,
        ILogService logService)
    {
        _xmlGeneratorService = xmlGeneratorService;
        _wimCustomizationService = wimCustomizationService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _fileSystemService = fileSystemService;
        _filePickerService = filePickerService;
        _logService = logService;

        SelectedXmlPath = string.Empty;
        XmlStatus = _localizationService.GetString("WIMUtil_Status_NoXmlAdded");

        CreateActionCards();
    }

    private void CreateActionCards()
    {
        GenerateWinhanceXmlCard = new WizardActionCard
        {
            Icon = "\uE710",
            Title = _localizationService.GetString("WIMUtil_Card_GenerateWinhanceXML_Title"),
            Description = _localizationService.GetString("WIMUtil_Card_GenerateWinhanceXML_Description"),
            ButtonText = _localizationService.GetString("WIMUtil_Card_GenerateWinhanceXML_Button"),
            ButtonCommand = GenerateWinhanceXmlCommand,
            IsEnabled = true
        };

        DownloadXmlCard = new WizardActionCard
        {
            IconPath = GetResourceIconPath("FileDownloadIconPath"),
            Title = _localizationService.GetString("WIMUtil_Card_DownloadXML_Title"),
            Description = _localizationService.GetString("WIMUtil_Card_DownloadXML_Description"),
            ButtonText = _localizationService.GetString("WIMUtil_Card_DownloadXML_Button"),
            ButtonCommand = DownloadUnattendedWinstallXmlCommand,
            IsEnabled = true
        };

        SelectXmlCard = new WizardActionCard
        {
            IconPath = GetResourceIconPath("AutounattendXmlIconPath"),
            Title = _localizationService.GetString("WIMUtil_Card_SelectXML_Title"),
            Description = _localizationService.GetString("WIMUtil_Card_SelectXML_Description"),
            ButtonText = _localizationService.GetString("WIMUtil_Card_SelectXML_Button"),
            ButtonCommand = SelectXmlFileCommand,
            IsEnabled = true
        };
    }

    [RelayCommand]
    private async Task GenerateWinhanceXml()
    {
        try
        {
            GenerateWinhanceXmlCard.IsComplete = false;
            GenerateWinhanceXmlCard.HasFailed = false;

            var confirmed = await _dialogService.ShowConfirmationAsync(
                _localizationService.GetString("WIMUtil_Card_GenerateWinhanceXML_Description"),
                _localizationService.GetString("WIMUtil_Card_GenerateWinhanceXML_Title"),
                "Yes", "No");
            if (!confirmed) return;

            XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlGenerating");
            var outputPath = _fileSystemService.CombinePath(WorkingDirectory, "autounattend.xml");
            var generatedPath = await _xmlGeneratorService.GenerateFromCurrentSelectionsAsync(outputPath);

            SelectedXmlPath = generatedPath;
            IsXmlAdded = true;
            XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlGenSuccess");
            ClearOtherXmlCardCompletions("generate");
            GenerateWinhanceXmlCard.IsComplete = true;
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error generating XML: {ex.Message}", ex);
            XmlStatus = string.Format(_localizationService.GetString("WIMUtil_Status_XmlGenFailed"), ex.Message);
            GenerateWinhanceXmlCard.HasFailed = true;
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_XmlGenError"), ex.Message),
                "Error");
        }
    }

    [RelayCommand]
    private async Task DownloadUnattendedWinstallXml()
    {
        try
        {
            DownloadXmlCard.IsComplete = false;
            DownloadXmlCard.HasFailed = false;

            var destinationPath = _fileSystemService.CombinePath(WorkingDirectory, "autounattend.xml");
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            var progress = new Progress<TaskProgressDetail>(detail => XmlStatus = detail.StatusText ?? _localizationService.GetString("WIMUtil_Status_XmlDownloading"));

            XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlDownloadStart");
            await _wimCustomizationService.DownloadUnattendedWinstallXmlAsync(destinationPath, progress, _cancellationTokenSource.Token);

            var addSuccess = await _wimCustomizationService.AddXmlToImageAsync(destinationPath, WorkingDirectory);
            if (addSuccess)
            {
                SelectedXmlPath = destinationPath;
                IsXmlAdded = true;
                XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlDownloadSuccess");
                ClearOtherXmlCardCompletions("download");
                DownloadXmlCard.IsComplete = true;
            }
            else
            {
                DownloadXmlCard.HasFailed = true;
                XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlAddFailed");
                await _dialogService.ShowErrorAsync(
                    _localizationService.GetString("WIMUtil_Msg_XmlAddFailed"),
                    "Error");
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error downloading XML: {ex.Message}", ex);
            XmlStatus = string.Format(_localizationService.GetString("WIMUtil_Status_XmlDownloadFailed"), ex.Message);
            DownloadXmlCard.HasFailed = true;
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_XmlDownloadError"), ex.Message),
                "Error");
        }
    }

    [RelayCommand]
    private async Task SelectXmlFile()
    {
        try
        {
            SelectXmlCard.IsComplete = false;
            SelectXmlCard.HasFailed = false;

            var selectedPath = _filePickerService.PickFile(
                ["XML Files", "*.xml"],
                _localizationService.GetString("WIMUtil_FileDialog_SelectXml"));
            if (string.IsNullOrEmpty(selectedPath)) return;

            XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlValidating");
            var isValidXml = await ValidateXmlFile(selectedPath);
            if (!isValidXml)
            {
                SelectXmlCard.HasFailed = true;
                XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlInvalid");
                await _dialogService.ShowErrorAsync(
                    _localizationService.GetString("WIMUtil_Msg_XmlInvalidError"),
                    "Error");
                return;
            }

            XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlAdding");
            var addSuccess = await _wimCustomizationService.AddXmlToImageAsync(selectedPath, WorkingDirectory);
            if (addSuccess)
            {
                SelectedXmlPath = selectedPath;
                IsXmlAdded = true;
                XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlSelectSuccess");
                ClearOtherXmlCardCompletions("select");
                SelectXmlCard.IsComplete = true;
            }
            else
            {
                SelectXmlCard.HasFailed = true;
                XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlValidAddFailed");
                await _dialogService.ShowErrorAsync(
                    _localizationService.GetString("WIMUtil_Msg_XmlValidAddFailed"),
                    "Error");
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error selecting XML: {ex.Message}", ex);
            XmlStatus = string.Format(_localizationService.GetString("WIMUtil_Status_ErrorPrefix"), ex.Message);
            SelectXmlCard.HasFailed = true;
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_XmlSelectError"), ex.Message),
                "Error");
        }
    }

    [RelayCommand]
    private async Task OpenSchneegansXmlGenerator()
    {
        try { await Windows.System.Launcher.LaunchUriAsync(new Uri("https://schneegans.de/windows/unattend-generator/")); }
        catch (Exception ex) { _logService.LogError($"Error opening Schneegans XML generator: {ex.Message}", ex); }
    }

    private async Task<bool> ValidateXmlFile(string xmlPath)
    {
        try
        {
            await Task.Run(() => XDocument.Load(xmlPath));
            return true;
        }
        catch (Exception ex) { _logService.LogDebug($"XML validation failed for '{xmlPath}': {ex.Message}"); return false; }
    }

    internal void ClearOtherXmlCardCompletions(string exceptCard)
    {
        if (exceptCard != "generate") GenerateWinhanceXmlCard.IsComplete = false;
        if (exceptCard != "download") DownloadXmlCard.IsComplete = false;
        if (exceptCard != "select") SelectXmlCard.IsComplete = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
    }

    private static string GetResourceIconPath(string resourceKey)
    {
        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(resourceKey, out var value) && value is string path)
            return path;
        return string.Empty;
    }
}
