using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.WimUtil.Models;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.WimUtil.ViewModels;

public partial class WimStep3DriversViewModel : ObservableObject, IMediaChoice
{
    private readonly IWimCustomizationService _wimCustomizationService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IFileSystemService _fileSystemService;
    private readonly IFilePickerService _filePickerService;
    private readonly ILogService _logService;

    // What the media already holds, so Create does not stage it twice.
    private bool _systemOnMedia;
    private string? _stagedFolder;

    public string WorkingDirectory
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;

            // Another folder is another image, so the choices start over.
            _systemOnMedia = false;
            _stagedFolder = null;
            AddSystemDrivers = false;
            AddFolderDrivers = false;
            CustomDriversFolder = string.Empty;
            PickUpMedia();
        }
    } = string.Empty;

    public bool IsBuiltIntoImage
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            RefreshText();
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDriverChoice))]
    public partial bool AddSystemDrivers { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDriverChoice))]
    public partial bool AddFolderDrivers { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FolderDriversDescription))]
    public partial string CustomDriversFolder { get; set; } = string.Empty;

    public bool HasDriverChoice => AddSystemDrivers || AddFolderDrivers;

    public string SystemDriversDescription => _localizationService.GetString(
        IsBuiltIntoImage ? "WIMUtil_Card_ExtractDrivers_Description_Image" : "WIMUtil_Card_ExtractDrivers_Description");

    public string FolderDriversDescription => !string.IsNullOrEmpty(CustomDriversFolder)
        ? $"{_localizationService.GetString("WIMUtil_Label_Selected")}: {CustomDriversFolder}"
        : _localizationService.GetString(
            IsBuiltIntoImage ? "WIMUtil_Card_CustomDrivers_Description_Image" : "WIMUtil_Card_CustomDrivers_Description");

    public string ApplyTitleKey => "WIMUtil_Build_PrepareDrivers";

    public FluentIcons.Common.Icon ApplyIcon => FluentIcons.Common.Icon.FolderArrowRight;

    public ServicingPhase? ServicedIn => ServicingPhase.Drivers;

    public bool NeedsApply =>
        (AddSystemDrivers && !_systemOnMedia)
        || (AddFolderDrivers && !string.IsNullOrEmpty(CustomDriversFolder) && CustomDriversFolder != _stagedFolder);

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
    }

    // Read from the media, so a reopened folder that already holds drivers shows them as chosen, and servicing moving
    // them into the image clears the choice.
    public void PickUpMedia()
    {
        var onMedia = !string.IsNullOrEmpty(WorkingDirectory) && _wimCustomizationService.AreDriversOnMedia(WorkingDirectory);
        if (onMedia)
        {
            if (!HasDriverChoice)
            {
                _systemOnMedia = true;
                AddSystemDrivers = true;
            }
            return;
        }

        if (!_systemOnMedia && _stagedFolder is null)
            return;

        _systemOnMedia = false;
        _stagedFolder = null;
        AddSystemDrivers = false;
        AddFolderDrivers = false;
        CustomDriversFolder = string.Empty;
        OnPropertyChanged(nameof(NeedsApply));
    }

    // The descriptions follow the mode and the language.
    public void RefreshText()
    {
        OnPropertyChanged(nameof(SystemDriversDescription));
        OnPropertyChanged(nameof(FolderDriversDescription));
    }

    partial void OnAddSystemDriversChanged(bool value) => OnPropertyChanged(nameof(NeedsApply));

    partial void OnAddFolderDriversChanged(bool value) => OnPropertyChanged(nameof(NeedsApply));

    partial void OnCustomDriversFolderChanged(string value) => OnPropertyChanged(nameof(NeedsApply));

    // Records the folder and nothing else. Create adds its drivers.
    [RelayCommand]
    private async Task ChooseDriversFolderAsync()
    {
        if (await PickFolderAsync())
            AddFolderDrivers = true;
    }

    [RelayCommand]
    private void ClearDriversFolder()
    {
        AddFolderDrivers = false;
        CustomDriversFolder = string.Empty;
    }

    private async Task<bool> PickFolderAsync()
    {
        var selectedPath = _filePickerService.PickFolder(_localizationService.GetString("WIMUtil_FolderDialog_SelectDrivers"));
        if (string.IsNullOrEmpty(selectedPath))
            return false;

        if (!_fileSystemService.DirectoryExists(selectedPath))
        {
            await _dialogService.ShowErrorAsync(_localizationService.GetString("WIMUtil_Msg_InvalidFolder"));
            return false;
        }

        if (_fileSystemService.GetFiles(selectedPath, "*.inf", SearchOption.AllDirectories).Length == 0)
        {
            await _dialogService.ShowWarningAsync(_localizationService.GetString("WIMUtil_Msg_NoCustomDrivers", selectedPath));
            return false;
        }

        CustomDriversFolder = selectedPath;
        return true;
    }

    public async Task<string?> ApplyAsync(IProgress<TaskProgressDetail> progress, CancellationToken cancellationToken)
    {
        if (AddSystemDrivers && !_systemOnMedia)
        {
            var failure = await StageAsync(null, progress, cancellationToken);
            if (failure is not null)
                return failure;
            _systemOnMedia = true;
        }

        if (AddFolderDrivers && !string.IsNullOrEmpty(CustomDriversFolder) && CustomDriversFolder != _stagedFolder)
        {
            var failure = await StageAsync(CustomDriversFolder, progress, cancellationToken);
            if (failure is not null)
                return failure;
            _stagedFolder = CustomDriversFolder;
        }

        OnPropertyChanged(nameof(NeedsApply));
        return null;
    }

    // A null folder is this PC's own drivers.
    private async Task<string?> StageAsync(string? folder, IProgress<TaskProgressDetail> progress, CancellationToken cancellationToken)
    {
        try
        {
            if (await _wimCustomizationService.AddDriversAsync(WorkingDirectory, folder, progress, cancellationToken))
                return null;

            return folder is null
                ? _localizationService.GetString("WIMUtil_Msg_NoDriversFound")
                : _localizationService.GetString("WIMUtil_Msg_NoCustomDrivers", folder);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logService.LogError($"Error adding drivers: {ex.Message}", ex);
            return _localizationService.GetString(
                folder is null ? "WIMUtil_Msg_DriverExtractionError" : "WIMUtil_Msg_DriverAdditionError", ex.Message);
        }
    }
}
