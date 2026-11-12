using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentIcons.Common;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Exceptions;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Winhance.UI.Features.WimUtil.Models;

namespace Winhance.UI.Features.WimUtil.ViewModels;

public partial class WimStep5IsoViewModel : ObservableObject
{
    private const string EditionId = "autounattend-edition";
    private const string MediaEditionsId = "autounattend-media-editions";
    private const string MediaEditionsListId = "autounattend-media-editions-list";
    private const string Netfx3Id = "autounattend-netfx3";
    private const string Netfx3RemoveSourceId = "autounattend-netfx3-remove-source";

    private readonly IWimCustomizationService _wimCustomizationService;
    private readonly IIsoService _isoService;
    private readonly IUsbMediaWriter _usbMediaWriter;
    private readonly IBootManagerService _bootManagerService;
    private readonly ITaskProgressService _taskProgressService;
    private readonly IProcessExecutor _processExecutor;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IFileSystemService _fileSystemService;
    private readonly IFilePickerService _filePickerService;
    private readonly ILogService _logService;
    private readonly IAnswerFileValidator _answerFileValidator;
    private readonly AnswerFileCheckState _checkState;
    private readonly IOfflineServicingService _offlineServicingService;
    private readonly ISelectionSaveService _saves;
    private readonly ISelectionSetBuilder _selections;
    private readonly WimUtilSession _session;
    private readonly IWimImageService _wimImageService;
    private readonly Dictionary<ServicingPhase, WizardActionCard> _phaseCards = [];
    private WizardActionCard _answerFileCard = new();
    private WizardActionCard _mediaCard = new();
    private WizardActionCard _formatCard = new();
    private readonly IReadOnlyList<IMediaChoice> _choices;
    private ImageFormat? _mediaFormat;
    private bool _formatChosen;

    public string WorkingDirectory
    {
        get;
        set
        {
            if (field != value)
                _formatChosen = false;
            field = value;
            OnPropertyChanged(nameof(CanDeleteWorkingFolder));
        }
    } = string.Empty;

    public bool IsBuiltIntoImage
    {
        get;
        set
        {
            field = value;
            EditionNeedChanged();
        }
    }

    // The facade hands these over before Create, the way it hands over the working folder and the mode.
    public ImageCleanupOptions Cleanup { get; set; } = ImageCleanupOptions.None;

    public int? EditionIndex
    {
        get;
        set
        {
            field = value;
            EditionNeedChanged();
        }
    }

    // Without an edition to keep, servicing leaves a multi-edition image alone and nothing gets built into it.
    public bool NeedsEdition => IsBuiltIntoImage && EditionIndex is null && _session.Editions.Count > 1;

    // True while a Winhance answer file is the one being built, which Create writes just before the media.
    public bool WritesAnswerFile { get; set; }

    // Raised once servicing has moved staged items out of the media folder and into the image.
    public event EventHandler<OfflineServicingResult>? ImageServiced;

    // Raised once Create has changed the image file's format, so the page reads the media again.
    public event EventHandler? ImageFormatChanged;

    public event EventHandler? WorkingFilesDeleted;

    [ObservableProperty]
    public partial bool IsCreatingMedia { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDeleteWorkingFolder))]
    public partial string OutputIsoPath { get; set; }

    [ObservableProperty]
    public partial bool IsIsoCreated { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIsoDestination))]
    [NotifyPropertyChangedFor(nameof(IsUsbDestination))]
    [NotifyPropertyChangedFor(nameof(CanDeleteWorkingFolder))]
    [NotifyCanExecuteChangedFor(nameof(CreateMediaCommand))]
    public partial WimOutputDestination Destination { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateMediaCommand))]
    public partial RemovableDrive? SelectedUsbTarget { get; set; }

    [ObservableProperty]
    public partial bool ShowUsbHardDrives { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWimFormat))]
    [NotifyPropertyChangedFor(nameof(IsEsdFormat))]
    public partial ImageFormat OutputFormat { get; set; }

    // False while the media's format is unknown or both files sit in the folder, where there is nothing to convert.
    [ObservableProperty]
    public partial bool CanChooseFormat { get; set; }

    public bool IsWimFormat => OutputFormat == ImageFormat.Wim;

    public bool IsEsdFormat => OutputFormat == ImageFormat.Esd;

    private bool NeedsFormatConversion => CanChooseFormat && _mediaFormat != OutputFormat;

    private ImageFormat EffectiveFormat => CanChooseFormat ? OutputFormat : _mediaFormat ?? ImageFormat.Wim;

    public bool IsIsoDestination => Destination == WimOutputDestination.Iso;

    public bool IsUsbDestination => Destination == WimOutputDestination.Usb;

    public ObservableCollection<RemovableDrive> UsbTargets { get; } = [];

    public ObservableCollection<BootManagerChoice> BootManagerChoices { get; } = [];

    [ObservableProperty]
    public partial BootManagerChoice? SelectedBootManager { get; set; }

    [ObservableProperty]
    public partial bool ShowBootManagerChoice { get; set; }

    [ObservableProperty]
    public partial string BootManagerDescription { get; set; }

    [ObservableProperty]
    public partial bool IsBootManagerOutdated { get; set; }

    public WizardActionCard SelectOutputCard { get; private set; } = new();

    public WizardActionCard SelectUsbCard { get; private set; } = new();

    // One card per thing Create is about to do, in order; rebuilt on every Create.
    public ObservableCollection<WizardActionCard> BuildCards { get; } = [];

    [ObservableProperty]
    public partial bool HasBuildCards { get; set; }

    [ObservableProperty]
    public partial bool DeleteWorkingFolderAfterBuild { get; set; }

    public bool CanDeleteWorkingFolder => CanOfferCleanup(WorkingDirectory, Destination, OutputIsoPath);

    public WimStep5IsoViewModel(
        IWimCustomizationService wimCustomizationService,
        IIsoService isoService,
        IUsbMediaWriter usbMediaWriter,
        IBootManagerService bootManagerService,
        ITaskProgressService taskProgressService,
        IProcessExecutor processExecutor,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IFileSystemService fileSystemService,
        IFilePickerService filePickerService,
        ILogService logService,
        IAnswerFileValidator answerFileValidator,
        AnswerFileCheckState checkState,
        IOfflineServicingService offlineServicingService,
        ISelectionSaveService saves,
        ISelectionSetBuilder selections,
        WimUtilSession session,
        IWimImageService wimImageService,
        IReadOnlyList<IMediaChoice> choices)
    {
        _wimCustomizationService = wimCustomizationService;
        _isoService = isoService;
        _usbMediaWriter = usbMediaWriter;
        _bootManagerService = bootManagerService;
        _taskProgressService = taskProgressService;
        _processExecutor = processExecutor;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _fileSystemService = fileSystemService;
        _filePickerService = filePickerService;
        _logService = logService;
        _answerFileValidator = answerFileValidator;
        _checkState = checkState;
        _offlineServicingService = offlineServicingService;
        _saves = saves;
        _selections = selections;
        _session = session;
        _wimImageService = wimImageService;
        _choices = choices;
        _session.EditionsChanged += (_, _) => EditionNeedChanged();

        OutputIsoPath = string.Empty;
        BootManagerDescription = string.Empty;

        CreateActionCards();
    }

    private void CreateActionCards()
    {
        SelectOutputCard = new WizardActionCard
        {
            Title = _localizationService.GetString("WIMUtil_Card_SelectOutput_Title"),
            Description = _localizationService.GetString("WIMUtil_Label_NoLocation"),
            ButtonText = _localizationService.GetString("WIMUtil_Card_SelectOutput_Button"),
            ButtonCommand = SelectIsoOutputLocationCommand,
            IsEnabled = true,
            BusyReason = _localizationService.GetString("WIMUtil_Disabled_Busy")
        };

        SelectUsbCard = new WizardActionCard
        {
            Title = _localizationService.GetString("WIMUtil_Card_SelectUsb_Title"),
            Description = _localizationService.GetString("WIMUtil_Card_SelectUsb_Description"),
            ButtonText = _localizationService.GetString("WIMUtil_Card_SelectUsb_Button"),
            ButtonCommand = RefreshUsbTargetsCommand,
            IsEnabled = true,
            BusyReason = _localizationService.GetString("WIMUtil_Disabled_Busy")
        };
    }

    // Run once the ISO is extracted. On failure the card stays hidden and the media keeps its own boot
    // manager.
    public async Task InspectBootManagerAsync()
    {
        try
        {
            var options = await _bootManagerService.InspectAsync(WorkingDirectory, CancellationToken.None);

            BootManagerChoices.Clear();
            if (options.Offers2023)
            {
                BootManagerChoices.Add(new BootManagerChoice(BootManager.Pca2011, _localizationService.GetString("WIMUtil_BootManager_Pca2011")));
                BootManagerChoices.Add(new BootManagerChoice(BootManager.Ca2023, _localizationService.GetString("WIMUtil_BootManager_Ca2023")));
            }

            SelectedBootManager = BootManagerChoices.FirstOrDefault(choice => choice.Value == options.Default);
            BootManagerDescription = _localizationService.GetString(options.Default == BootManager.Ca2023
                ? "WIMUtil_Card_BootManager_DescriptionRevoked"
                : "WIMUtil_Card_BootManager_Description");
            ShowBootManagerChoice = options.Offers2023;
            IsBootManagerOutdated = options.IsOutdated;
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Could not read the media's boot manager: {ex.Message}");
            BootManagerChoices.Clear();
            SelectedBootManager = null;
            ShowBootManagerChoice = false;
            IsBootManagerOutdated = false;
        }
    }

    private Task<BootFileReplacements> PrepareBootFilesAsync(CancellationToken cancellationToken) =>
        _bootManagerService.PrepareAsync(WorkingDirectory, SelectedBootManager?.Value ?? BootManager.Pca2011, cancellationToken);

    // The output follows the media's own format until the user picks one, so an ESD source is not converted by default.
    public void ShowMediaFormat(ImageFormat? format, bool bothExist)
    {
        _mediaFormat = format;
        CanChooseFormat = format is not null && !bothExist;
        if (format is { } current && !_formatChosen)
            OutputFormat = current;
    }

    [RelayCommand]
    private void SelectWimFormat()
    {
        _formatChosen = true;
        OutputFormat = ImageFormat.Wim;
    }

    [RelayCommand]
    private void SelectEsdFormat()
    {
        _formatChosen = true;
        OutputFormat = ImageFormat.Esd;
    }

    [RelayCommand]
    private void SelectIsoDestination() => Destination = WimOutputDestination.Iso;

    [RelayCommand]
    private async Task SelectUsbDestinationAsync()
    {
        Destination = WimOutputDestination.Usb;
        await RefreshUsbTargetsAsync();
    }

    partial void OnShowUsbHardDrivesChanged(bool value)
    {
        if (IsUsbDestination)
            _ = RefreshUsbTargetsCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private async Task RefreshUsbTargetsAsync()
    {
        try
        {
            SelectUsbCard.IsProcessing = true;
            var previous = SelectedUsbTarget?.DiskNumber;
            var includeHardDrives = ShowUsbHardDrives;
            var targets = await Task.Run(() => _usbMediaWriter.GetCandidateTargets(includeHardDrives));

            UsbTargets.Clear();
            foreach (var target in targets)
            {
                UsbTargets.Add(target);
            }

            // Never a default on a USB hard drive: it is the likelier one to hold someone's backups.
            SelectedUsbTarget = UsbTargets.FirstOrDefault(t => t.DiskNumber == previous) ?? UsbTargets.FirstOrDefault(t => t.HasRemovableMedia);
            SelectUsbCard.Description = UsbTargets.Count == 0
                ? _localizationService.GetString("WIMUtil_Label_NoUsbFound")
                : _localizationService.GetString("WIMUtil_Card_SelectUsb_Description");
        }
        catch (Exception ex)
        {
            _logService.LogError($"Could not list USB drives: {ex.Message}", ex);
            SelectUsbCard.Description = string.Format(_localizationService.GetString("WIMUtil_Status_ErrorPrefix"), ex.Message);
        }
        finally
        {
            SelectUsbCard.IsProcessing = false;
        }
    }

    [RelayCommand]
    private void SelectIsoOutputLocation()
    {
        var path = _filePickerService.PickSavePath(
            _localizationService.GetString("WIMUtil_Card_SelectOutput_Title"),
            "ISO Files",
            "*.iso",
            "Winhance_Windows.iso",
            "iso");
        if (!string.IsNullOrEmpty(path))
        {
            OutputIsoPath = path;
            SelectOutputCard.Description = $"{_localizationService.GetString("WIMUtil_Label_Output")}: {OutputIsoPath}";
        }
    }

    private void EditionNeedChanged()
    {
        OnPropertyChanged(nameof(NeedsEdition));
        CreateMediaCommand.NotifyCanExecuteChanged();
    }

    private bool CanCreateMedia() => (!IsUsbDestination || SelectedUsbTarget is not null) && !NeedsEdition;

    [RelayCommand(CanExecute = nameof(CanCreateMedia))]
    private async Task CreateMediaAsync()
    {
        IsCreatingMedia = true;
        bool created;
        try
        {
            created = await CreateMediaCoreAsync();
        }
        finally
        {
            IsCreatingMedia = false;
        }

        if (created && DeleteWorkingFolderAfterBuild)
            await DeleteWorkingFolderAsync();
    }

    [RelayCommand]
    private Task ShowBuildDetailsAsync() =>
        _dialogService.ShowTaskOutputDialogAsync(
            _localizationService.GetString("Dialog_TerminalOutput_Title"),
            _taskProgressService.GetTerminalOutputLines());

    // Only Winhance's own folder is offered, never one the user pointed at, and never when the ISO was saved inside it.
    internal static bool CanOfferCleanup(string workingDirectory, WimOutputDestination destination, string outputIsoPath)
    {
        if (!IsWinhanceFolder(workingDirectory))
            return false;
        return destination == WimOutputDestination.Usb
            || !outputIsoPath.StartsWith(workingDirectory.TrimEnd('\\', '/') + "\\", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsWinhanceFolder(string workingDirectory) =>
        Path.GetFileName(workingDirectory.TrimEnd('\\', '/')).Equals(WimStep1ViewModel.WorkingFolderName, StringComparison.OrdinalIgnoreCase);

    // The extracted media is several GB that the finished ISO or USB no longer needs.
    private async Task DeleteWorkingFolderAsync()
    {
        var folder = WorkingDirectory;
        if (!CanDeleteWorkingFolder || !_fileSystemService.DirectoryExists(folder))
            return;

        try
        {
            await Task.Run(() => _fileSystemService.DeleteDirectory(folder, recursive: true));
            WorkingFilesDeleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logService.LogError($"Could not delete the working folder: {ex.Message}", ex);
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_CleanupFailed"), folder, ex.Message));
        }
    }

    private async Task<bool> CreateMediaCoreAsync()
    {
        if (string.IsNullOrEmpty(WorkingDirectory))
        {
            await _dialogService.ShowWarningAsync(
                _localizationService.GetString("WIMUtil_Msg_WorkingDirectoryRequired"));
            return false;
        }

        if (Destination == WimOutputDestination.Usb)
        {
            return await WriteUsbMediaAsync();
        }

        if (string.IsNullOrEmpty(OutputIsoPath))
        {
            await _dialogService.ShowWarningAsync(
                _localizationService.GetString("WIMUtil_Msg_OutputRequired"));
            return false;
        }

        var plan = await PrepareBuildAsync("WIMUtil_Status_CreatingIso", usb: null);
        if (plan is null)
            return false;

        var answerFileChanged = false;
        var created = false;
        var cancelled = false;
        try
        {
            SelectOutputCard.IsEnabled = false;

            _taskProgressService.StartTask(_localizationService.GetString("WIMUtil_Status_CreatingIso"), true);
            var progress = _taskProgressService.CreateDetailedProgress();
            var token = _taskProgressService.CurrentTaskCancellationSource!.Token;
            answerFileChanged = await RunImageBuildAsync(plan, progress, token);
            _mediaCard.IsProcessing = true;
            var replacements = await PrepareBootFilesAsync(token);

            var success = await _isoService.CreateIsoAsync(WorkingDirectory, OutputIsoPath, replacements, progress, token);
            _taskProgressService.CompleteTask();

            SelectOutputCard.IsEnabled = true;

            if (success)
            {
                IsIsoCreated = true;
                created = true;
                _mediaCard.IsComplete = true;
                SelectOutputCard.Description = _localizationService.GetString("WIMUtil_Desc_IsoCreatedSuccess");

                var openFolder = (await _dialogService.ShowConfirmationAsync(new ConfirmationRequest
                {
                    Message = string.Format(_localizationService.GetString("WIMUtil_Msg_IsoCreatedSuccess"), OutputIsoPath),
                    Title = _localizationService.GetString("WIMUtil_Desc_IsoCreatedSuccess"),
                    ConfirmButtonText = _localizationService.GetString("WIMUtil_Button_OpenFolder"),
                    CancelButtonText = _localizationService.GetString("Button_Close"),
                })).Confirmed;
                if (openFolder)
                {
                    _processExecutor.ShellExecuteAsync("explorer.exe", $"/select,\"{OutputIsoPath}\"").FireAndForget(_logService);
                }
            }
            else
            {
                _taskProgressService.FailTask();
                SelectOutputCard.Description = _localizationService.GetString("WIMUtil_Desc_IsoCreateFailed");
                await _dialogService.ShowErrorAsync(
                    _localizationService.GetString("WIMUtil_Msg_IsoCreationFailed"));
            }
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
            _taskProgressService.CancelTask();
            SelectOutputCard.IsEnabled = true;
            try { if (_fileSystemService.FileExists(OutputIsoPath)) _fileSystemService.DeleteFile(OutputIsoPath); } catch (Exception ex) { _logService.LogDebug($"Best-effort incomplete ISO cleanup failed: {ex.Message}"); }
            SelectOutputCard.Description = _localizationService.GetString("WIMUtil_Desc_IsoCreateCancelled");
        }
        catch (InsufficientDiskSpaceException spaceEx)
        {
            _taskProgressService.FailTask();
            SelectOutputCard.IsEnabled = true;
            SelectOutputCard.Description = string.Format(_localizationService.GetString("WIMUtil_Status_InsufficientDiskSpace"), spaceEx.DriveName);
            await _dialogService.ShowWarningAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_InsufficientSpace_Create"), spaceEx.DriveName, spaceEx.RequiredGB.ToString("F2"), spaceEx.AvailableGB.ToString("F2"), (spaceEx.RequiredGB - spaceEx.AvailableGB).ToString("F2")),
                string.Format(_localizationService.GetString("WIMUtil_Status_InsufficientDiskSpace"), spaceEx.DriveName));
        }
        catch (Exception ex)
        {
            _taskProgressService.FailTask();
            SelectOutputCard.IsEnabled = true;
            SelectOutputCard.Description = string.Format(_localizationService.GetString("WIMUtil_Status_ErrorPrefix"), ex.Message);
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_IsoCreationError"), ex.Message));
        }
        finally
        {
            SettleBuildCards(failed: !created && !cancelled);
            _taskProgressService.CompleteTask();
        }

        if (answerFileChanged)
            await RecheckServicedAnswerFileAsync();
        return created;
    }

    private sealed record BuildPlan(
        SelectionSet? Set, OfflineBuildRequest? Request, EditionPlan? Editions, bool RemoveSource)
    {
        public IReadOnlyList<StagedApp> Apps { get; set; } = [];
    }

    private sealed record EditionPlan(
        IReadOnlyList<ImageEdition> Kept, IReadOnlyList<ImageEdition> Removed, bool NoneMatched, bool RemovesChosen,
        ImageEdition? ChosenNotOnMedia);

    // Everything before the media itself: the cards, the answer file, the drivers and update the user ticked, and the
    // one dialog that can stop the run. Null means nothing more happens; the cards stay up so the user sees how far it got.
    private async Task<BuildPlan?> PrepareBuildAsync(string mediaTitleKey, RemovableDrive? usb)
    {
        BuildPlan plan;
        try
        {
            plan = await PlanBuildAsync();
            var pending = _choices.Where(choice => choice.NeedsApply).ToList();
            var choiceCards = ShowStageCards(plan, pending);
            ShowServicingCards(plan, mediaTitleKey);
            if (!await WriteAnswerFileAsync(plan))
            {
                SettleBuildCards(failed: true);
                return null;
            }

            if (!await ApplyChoicesAsync(pending, choiceCards))
            {
                SettleBuildCards(failed: false);
                return null;
            }
        }
        catch (OperationCanceledException)
        {
            SettleBuildCards(failed: false);
            return null;
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error generating XML: {ex.Message}", ex);
            SettleBuildCards(failed: true);
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_XmlGenError"), ex.Message));
            return null;
        }

        await EnsureInstallStepsAsync();

        if (await GoAheadAsync(plan, usb))
            return plan;

        SettleBuildCards(failed: false);
        return null;
    }

    private async Task<BuildPlan> PlanBuildAsync()
    {
        var set = WritesAnswerFile ? await _selections.FromBuilderSessionAsync() : null;
        var editions = set is null ? null : PlanEditions(set);

        if (IsBuiltIntoImage)
        {
            var request = new OfflineBuildRequest(WorkingDirectory)
            {
                EditionIndex = EditionIndex,
                OutputFormat = EffectiveFormat,
                StagesDrivers = Stages(ServicingPhase.Drivers),
                StagesUpdate = Stages(ServicingPhase.Update),
                EnableDotNet35 = IsEnabled(set, Netfx3Id),
                RemoveDotNetSource = IsEnabled(set, Netfx3RemoveSourceId),
                Cleanup = Cleanup,
                Selections = set,
            };
            return new BuildPlan(set, request, editions, RemoveSource: false);
        }

        // Setup installs .NET from the media's sources\sxs folder when the answer file turns it on.
        var removeSource = IsEnabled(set, Netfx3RemoveSourceId) && !IsEnabled(set, Netfx3Id);
        return new BuildPlan(set, null, editions, removeSource);
    }

    private bool Stages(ServicingPhase phase) => _choices.Any(choice => choice.NeedsApply && choice.ServicedIn == phase);

    private static bool IsEnabled(SelectionSet? set, string settingId) =>
        set?.StateOf(settingId)?.Label == LocKey.Common.Enabled;

    private List<WizardActionCard> ShowStageCards(BuildPlan plan, IReadOnlyList<IMediaChoice> pending)
    {
        BuildCards.Clear();
        _phaseCards.Clear();

        _answerFileCard = new WizardActionCard { Icon = Icon.DocumentText };
        if (plan.Set is not null)
        {
            _answerFileCard.Title = _localizationService.GetString("WIMUtil_Build_WriteAnswerFile");
            BuildCards.Add(_answerFileCard);
        }

        var cards = new List<WizardActionCard>();
        foreach (var choice in pending)
        {
            var card = new WizardActionCard { Title = _localizationService.GetString(choice.ApplyTitleKey), Icon = choice.ApplyIcon };
            cards.Add(card);
            BuildCards.Add(card);
        }

        HasBuildCards = true;
        return cards;
    }

    private void ShowServicingCards(BuildPlan plan, string mediaTitleKey)
    {
        if (plan.Request is { } request)
        {
            foreach (var phase in _offlineServicingService.PhasesFor(request))
                AddPhaseCard(phase);
        }
        else
        {
            if (plan.Editions is { Removed.Count: > 0 })
                AddPhaseCard(ServicingPhase.Export);
            if (plan.RemoveSource)
                AddPhaseCard(ServicingPhase.RemoveDotNetSource);
        }

        // Servicing exports in the chosen format when it already rewrites the image; otherwise it is a step of its own.
        _formatCard = new WizardActionCard { Icon = Icon.ArrowSwap };
        if (NeedsFormatConversion && !_phaseCards.ContainsKey(ServicingPhase.Export))
        {
            _formatCard.Title = _localizationService.GetString("WIMUtil_Build_ConvertFormat", FormatName(OutputFormat));
            BuildCards.Add(_formatCard);
        }

        _mediaCard = new WizardActionCard
        {
            Title = _localizationService.GetString(mediaTitleKey),
            Icon = Destination == WimOutputDestination.Usb ? Icon.UsbStick : Icon.Storage,
        };
        BuildCards.Add(_mediaCard);
    }

    internal static string FormatName(ImageFormat format) => format == ImageFormat.Esd ? "ESD" : "WIM";

    // Each choice is one step of the run. A failure stops it with the choice's own message, and Cancel stops it quietly.
    private async Task<bool> ApplyChoicesAsync(IReadOnlyList<IMediaChoice> pending, IReadOnlyList<WizardActionCard> cards)
    {
        for (var i = 0; i < pending.Count; i++)
        {
            var card = cards[i];
            card.IsProcessing = true;
            _taskProgressService.StartTask(_localizationService.GetString(pending[i].ApplyTitleKey), true);
            string? failure;
            try
            {
                var progress = _taskProgressService.CreateDetailedProgress();
                failure = await pending[i].ApplyAsync(progress, _taskProgressService.CurrentTaskCancellationSource!.Token);
            }
            catch (OperationCanceledException)
            {
                _taskProgressService.CancelTask();
                card.IsProcessing = false;
                return false;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Could not prepare {pending[i].ApplyTitleKey}: {ex.Message}", ex);
                failure = ex.Message;
            }
            finally
            {
                _taskProgressService.CompleteTask();
            }

            if (failure is not null)
            {
                card.IsProcessing = false;
                card.HasFailed = true;
                await _dialogService.ShowErrorAsync(failure);
                return false;
            }

            card.IsProcessing = false;
            card.IsComplete = true;
        }

        return true;
    }

    private void AddPhaseCard(ServicingPhase phase)
    {
        var card = new WizardActionCard { Title = _localizationService.GetString(PhaseTitleKey(phase)), Icon = PhaseIcon(phase) };
        _phaseCards[phase] = card;
        BuildCards.Add(card);
    }

    private static string PhaseTitleKey(ServicingPhase phase) => phase switch
    {
        ServicingPhase.PrepareImage => "WIMUtil_Progress_PreparingImage",
        ServicingPhase.Mount => "WIMUtil_Progress_Mounting",
        ServicingPhase.Update => "WIMUtil_Progress_AddingUpdate",
        ServicingPhase.Drivers => "WIMUtil_Progress_AddingDrivers",
        ServicingPhase.RemoveApps => "WIMUtil_Progress_RemovingApps",
        ServicingPhase.ApplySettings => "WIMUtil_Progress_ApplyingSettings",
        ServicingPhase.Cleanup => "WIMUtil_Progress_CleaningUp",
        ServicingPhase.DotNet => "WIMUtil_Progress_EnablingDotNet",
        ServicingPhase.Save => "WIMUtil_Progress_Saving",
        ServicingPhase.Export => "WIMUtil_Progress_Exporting",
        ServicingPhase.RemoveDotNetSource => "WIMUtil_Progress_RemovingDotNetSource",
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
    };

    private static Icon PhaseIcon(ServicingPhase phase) => phase switch
    {
        ServicingPhase.PrepareImage => Icon.WrenchScrewdriver,
        ServicingPhase.Mount => Icon.HardDrive,
        ServicingPhase.Update => Icon.ArrowSync,
        ServicingPhase.Drivers => Icon.DeveloperBoard,
        ServicingPhase.RemoveApps => Icon.Delete,
        ServicingPhase.ApplySettings => Icon.Settings,
        ServicingPhase.Cleanup => Icon.Broom,
        ServicingPhase.DotNet => Icon.Code,
        ServicingPhase.Save => Icon.Save,
        ServicingPhase.Export => Icon.Layer,
        ServicingPhase.RemoveDotNetSource => Icon.FolderProhibited,
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
    };

    private void SetPhase(ServicingPhase phase, ServicingPhaseState state)
    {
        if (!_phaseCards.TryGetValue(phase, out var card))
            return;

        switch (state)
        {
            case ServicingPhaseState.Running:
                card.IsProcessing = true;
                break;
            case ServicingPhaseState.Done:
                card.IsComplete = true;
                break;
            case ServicingPhaseState.Failed:
                card.HasFailed = true;
                break;
            case ServicingPhaseState.Skipped:
                card.IsProcessing = false;
                card.IsComplete = false;
                card.HasFailed = false;
                card.Description = _localizationService.GetString("WIMUtil_Build_Skipped");
                break;
        }
    }

    // A card still spinning when the run ends was the step that stopped it.
    private void SettleBuildCards(bool failed)
    {
        foreach (var card in BuildCards.Where(card => card.IsProcessing))
        {
            if (failed)
                card.HasFailed = true;
            else
                card.IsProcessing = false;
        }
    }

    // The file is written after steps 2 and 3, so the driver and update steps they added to the old one are
    // laid down again here. Image mode skips it: servicing removes these steps, or re-ensures the ones it
    // could not build in. It only self-heals and must never block output.
    private async Task EnsureInstallStepsAsync()
    {
        if (IsBuiltIntoImage)
            return;

        try
        {
            var driverStep = await _wimCustomizationService.EnsureDriverInstallStepAsync(WorkingDirectory);
            if (driverStep is DriverInstallStepResult.Added or DriverInstallStepResult.CreatedXml)
                _logService.LogWarning($"Driver install step was missing at media-creation time; repaired as {driverStep}");
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Could not verify the driver install step: {ex.Message}");
        }

        try
        {
            var updateStep = await _wimCustomizationService.EnsureUpdateInstallStepAsync(WorkingDirectory);
            if (updateStep is UpdateInstallStepResult.Added or UpdateInstallStepResult.CreatedXml)
                _logService.LogWarning($"Update install step was missing at media-creation time; repaired as {updateStep}");
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Could not verify the update install step: {ex.Message}");
        }
    }

    // False when the save produced no file, which already told the user why.
    private async Task<bool> WriteAnswerFileAsync(BuildPlan plan)
    {
        if (plan.Set is not { } set)
            return true;

        _answerFileCard.IsProcessing = true;
        var saved = await _saves.SaveAsync(BuilderTarget.Autounattend, set, new SelectionSaveOptions
        {
            FixedPath = _fileSystemService.CombinePath(WorkingDirectory, "autounattend.xml"),
            MediaFolder = WorkingDirectory,
            ReportSuccessInDialog = false,
        });
        if (saved.Path is null)
            return false;
        if (saved.StagingCancelled)
            throw new OperationCanceledException();

        plan.Apps = saved.Apps;
        _answerFileCard.IsComplete = true;
        return true;
    }

    // Kept is every edition when the choice keeps none, so a slip in the checklist never empties the media.
    private EditionPlan PlanEditions(SelectionSet set)
    {
        var editions = _session.Editions;
        int? chosen = set.Value(EditionId) is ChoiceValue.Option { Index: var option } ? option : null;

        // An edition outside the table has no option either, and must not pass for a choice that names none.
        bool IsChosen(ImageEdition e) => chosen is { } picked && MediaEditions.OptionFor(e.EditionId) == picked;

        // Servicing exports the edition at EditionIndex out of the image, so that one is what the media will hold.
        var held = IsBuiltIntoImage
            ? editions.FirstOrDefault(e => e.Index == EditionIndex) ?? (editions is [var only] ? only : null)
            : null;
        var chosenNotOnMedia = held is not null && MediaEditions.PicksAnother(chosen, held.EditionId) ? held : null;

        var keep = set.StateOf(MediaEditionsId)?.Label;
        var kept =
            keep == LocKey.Setting.AutounattendMediaEditions.Option1
                ? editions.Where(IsChosen).ToList()
            : keep == LocKey.Setting.AutounattendMediaEditions.Option2 && set.Value(MediaEditionsListId) is ChoiceValue.Keys ticked
                ? editions.Where(e => ticked.Values.Contains(e.Name)).ToList()
            : editions.ToList();

        if (kept.Count == 0)
            return new EditionPlan(editions, [], NoneMatched: editions.Count > 0, RemovesChosen: false, ChosenNotOnMedia: chosenNotOnMedia);

        var removed = editions.Except(kept).ToList();
        return new EditionPlan(kept, removed, NoneMatched: false,
            RemovesChosen: removed.Any(IsChosen) && !kept.Any(IsChosen), ChosenNotOnMedia: chosenNotOnMedia);
    }

    // Image mode builds everything through servicing. During setup the image is only trimmed: the editions the
    // answer file does not keep are exported out, and the .NET source goes when Setup will not need it.
    // True when servicing rewrote autounattend.xml (built-in items drop their setup steps), so the file
    // GoAheadAsync checked is no longer the one on the media.
    private async Task<bool> RunImageBuildAsync(BuildPlan plan, IProgress<TaskProgressDetail> progress, CancellationToken token)
    {
        var answerFileChanged = false;
        if (plan.Request is { } request)
        {
            answerFileChanged = await ServiceImageAsync(request, progress, token);
        }
        else
        {
            await KeepEditionsAsync(plan, progress, token);
            RemoveDotNetSourceFolder(plan);
        }

        await ConvertFormatAsync(progress, token);
        return answerFileChanged;
    }

    // Image mode has already exported in this format, which makes the call a no-op there; during setup it does the work.
    private async Task ConvertFormatAsync(IProgress<TaskProgressDetail> progress, CancellationToken token)
    {
        if (!CanChooseFormat)
            return;

        var converting = NeedsFormatConversion;
        _formatCard.IsProcessing = converting;
        if (!await _wimImageService.ConvertImageAsync(WorkingDirectory, OutputFormat, progress, token))
            throw new InvalidOperationException(
                string.Format(_localizationService.GetString("WIMUtil_Msg_ConversionFailed"), FormatName(OutputFormat)));

        _formatCard.IsProcessing = false;
        _formatCard.IsComplete = converting;
        _mediaFormat = OutputFormat;
        if (converting)
            ImageFormatChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task<bool> ServiceImageAsync(OfflineBuildRequest request, IProgress<TaskProgressDetail> progress, CancellationToken token)
    {
        if (_phaseCards.Count == 0)
            return false;

        progress.Report(new TaskProgressDetail { StatusText = _localizationService.GetString("WIMUtil_Status_Servicing") });
        var phases = new Progress<ServicingPhaseUpdate>(update => SetPhase(update.Phase, update.State));
        var result = await _offlineServicingService.ServiceAsync(request, progress, phases, token);
        _session.Editions = (await _wimImageService.DetectImageFormatAsync(WorkingDirectory))?.Editions ?? _session.Editions;
        ImageServiced?.Invoke(this, result);
        if (result.AnyFellBack)
        {
            await _dialogService.ShowWarningAsync(
                _localizationService.GetString(FellBackMessageKey(result)));
        }
        return result.Drivers != ServicedItemOutcome.NotStaged || result.Update != ServicedItemOutcome.NotStaged;
    }

    // The answer file is already on the media, so a failed removal still leaves media that installs, with every
    // edition on it. Cancelling is the user stopping the whole run, so it is not caught here.
    private async Task KeepEditionsAsync(BuildPlan plan, IProgress<TaskProgressDetail> progress, CancellationToken token)
    {
        if (plan.Editions is not { Removed.Count: > 0 } editions)
            return;

        SetPhase(ServicingPhase.Export, ServicingPhaseState.Running);
        try
        {
            var result = await _wimImageService.KeepEditionsAsync(
                WorkingDirectory, [.. editions.Kept.Select(e => e.Name)], progress, token);

            if (!result.Removed)
            {
                _phaseCards[ServicingPhase.Export].Description = _localizationService.GetString("WIMUtil_Msg_RemoveEditionsFailed");
                SetPhase(ServicingPhase.Export, ServicingPhaseState.Failed);
                return;
            }

            _session.Editions = (await _wimImageService.DetectImageFormatAsync(WorkingDirectory))?.Editions ?? editions.Kept;
            if (result.LeftoverImage is { } leftover)
                _phaseCards[ServicingPhase.Export].Description = _localizationService.GetString("WIMUtil_Generate_Summary_OldImageLeft", leftover);
            SetPhase(ServicingPhase.Export, ServicingPhaseState.Done);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logService.LogError($"Error removing editions: {ex.Message}", ex);
            _phaseCards[ServicingPhase.Export].Description = _localizationService.GetString("WIMUtil_Msg_RemoveEditionsFailed");
            SetPhase(ServicingPhase.Export, ServicingPhaseState.Failed);
        }
    }

    private void RemoveDotNetSourceFolder(BuildPlan plan)
    {
        if (!plan.RemoveSource)
            return;

        var sxs = _fileSystemService.CombinePath(WorkingDirectory, "sources", "sxs");
        if (!_fileSystemService.DirectoryExists(sxs))
        {
            SetPhase(ServicingPhase.RemoveDotNetSource, ServicingPhaseState.Skipped);
            return;
        }

        SetPhase(ServicingPhase.RemoveDotNetSource, ServicingPhaseState.Running);
        try
        {
            _fileSystemService.DeleteDirectory(sxs, recursive: true);
            SetPhase(ServicingPhase.RemoveDotNetSource, ServicingPhaseState.Done);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logService.LogWarning($"Could not delete {sxs}: {ex.Message}");
            SetPhase(ServicingPhase.RemoveDotNetSource, ServicingPhaseState.Failed);
        }
    }

    private Task RecheckServicedAnswerFileAsync() =>
        _checkState.RecheckAfterChangeAsync(
            _fileSystemService.CombinePath(WorkingDirectory, "autounattend.xml"),
            _answerFileValidator, _dialogService, _localizationService, _logService);

    private static string FellBackMessageKey(OfflineServicingResult result) => (result.Drivers, result.Update) switch
    {
        (ServicedItemOutcome.FellBackToSetup, ServicedItemOutcome.FellBackToSetup) => "WIMUtil_Msg_ServicingFellBack_Both",
        (ServicedItemOutcome.FellBackToSetup, _) => "WIMUtil_Msg_ServicingFellBack_Drivers",
        _ => "WIMUtil_Msg_ServicingFellBack_Update",
    };

    // The one dialog Create can show before it starts. For a stick it always asks, and names the device and its size,
    // because "are you sure" next to a drive picker is how people wipe the wrong stick.
    private async Task<bool> GoAheadAsync(BuildPlan plan, RemovableDrive? usb)
    {
        var review = await AnswerFileReviewAsync(plan);
        if (usb is null)
            return review is null || await _dialogService.ShowSummaryAsync(review);

        var erase = string.Format(
            _localizationService.GetString("WIMUtil_Msg_UsbEraseConfirm"),
            usb.Model,
            usb.SizeGigabytes.ToString("F1"),
            usb.DiskNumber.ToString());
        if (review is not null)
        {
            return await _dialogService.ShowSummaryAsync(review with
            {
                Message = erase,
                ConfirmButtonText = _localizationService.GetString("WIMUtil_Button_EraseAndWrite"),
            });
        }

        return (await _dialogService.ShowConfirmationAsync(new ConfirmationRequest
        {
            Message = erase,
            Title = _localizationService.GetString("WIMUtil_Msg_UsbEraseTitle"),
            ConfirmButtonText = _localizationService.GetString("WIMUtil_Button_EraseAndWrite"),
            CancelButtonText = _localizationService.GetString("Button_Close"),
        })).Confirmed;
    }

    // The last look at the file Setup will read, or null when there is nothing to decide: no finding, every installer
    // on the media, and an edition choice that does what it says. A check that cannot run is logged and never stops the media.
    private async Task<SummaryRequest?> AnswerFileReviewAsync(BuildPlan plan)
    {
        var answerFile = _fileSystemService.CombinePath(WorkingDirectory, "autounattend.xml");
        if (!_fileSystemService.FileExists(answerFile))
            return null;

        try
        {
            var report = await _answerFileValidator.ValidateAsync(answerFile);
            _checkState.Publish(answerFile, report);

            var sections = new List<SummarySection>();
            if (plan.Apps.Count > 0)
            {
                sections.Add(new(_localizationService.GetString("WIMUtil_Generate_Result_AppsHeading"),
                    plan.Apps
                        .Select(app => new SummaryRow(
                            SummaryIcon.Status,
                            app.Name,
                            _localizationService.GetString(SelectionSaveService.ReasonKey(app.Outcome)),
                            Tone: app.Staged ? SummaryTone.Success : SummaryTone.Warning))
                        .ToList()));
            }

            var editionRows = EditionWarningRows(plan.Editions);
            if (editionRows.Count > 0)
                sections.Add(new(_localizationService.GetString("WIMUtil_Generate_Result_EditionsHeading"), editionRows));

            if (report.Findings.Count == 0 && editionRows.Count == 0 && plan.Apps.All(app => app.Staged))
                return null;

            if (report.Findings.Count > 0)
                sections.Add(AnswerFileReportDialog.FindingsSection(report, _localizationService));

            return AnswerFileReportDialog.Request(report, _localizationService) with
            {
                Sections = sections,
                ConfirmButtonText = _localizationService.GetString("WIMUtil_Button_ContinueAnyway"),
            };
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Could not check autounattend.xml: {ex.Message}");
            return null;
        }
    }

    // Removing editions only happens during setup, so the other two warnings have nothing to say in image mode.
    private List<SummaryRow> EditionWarningRows(EditionPlan? editions)
    {
        var rows = new List<SummaryRow>();
        if (editions is null)
            return rows;

        if (editions.RemovesChosen && !IsBuiltIntoImage)
        {
            rows.Add(new(SummaryIcon.Status,
                _localizationService.GetString("WIMUtil_Generate_Plan_ChosenEditionRemoved"),
                _localizationService.GetString("WIMUtil_Generate_Plan_ChosenEditionRemoved_Detail"),
                Tone: SummaryTone.Warning));
        }

        if (editions.NoneMatched && !IsBuiltIntoImage)
        {
            rows.Add(new(SummaryIcon.Status,
                _localizationService.GetString("WIMUtil_Generate_Plan_NoEditionLeft"),
                _localizationService.GetString("WIMUtil_Generate_Plan_NoEditionLeft_Detail"),
                Tone: SummaryTone.Warning));
        }

        if (editions.ChosenNotOnMedia is { } held)
        {
            rows.Add(new(SummaryIcon.Status,
                _localizationService.GetString("WIMUtil_Generate_Plan_ChosenNotOnMedia"),
                _localizationService.GetString("WIMUtil_Generate_Plan_ChosenNotOnMedia_Detail", held.Name),
                Tone: SummaryTone.Warning));
        }

        return rows;
    }

    private async Task<bool> WriteUsbMediaAsync()
    {
        var target = SelectedUsbTarget;
        if (target is null)
        {
            await _dialogService.ShowWarningAsync(
                _localizationService.GetString("WIMUtil_Msg_UsbRequired"));
            return false;
        }

        var plan = await PrepareBuildAsync("WIMUtil_Status_WritingUsb", target);
        if (plan is null)
            return false;

        var answerFileChanged = false;
        var written = false;
        var cancelled = false;
        try
        {
            SelectUsbCard.IsEnabled = false;

            _taskProgressService.StartTask(_localizationService.GetString("WIMUtil_Status_WritingUsb"), true);
            var progress = _taskProgressService.CreateDetailedProgress();
            var token = _taskProgressService.CurrentTaskCancellationSource!.Token;

            // Before the writer erases the stick, so a servicing failure leaves the drive as it was.
            answerFileChanged = await RunImageBuildAsync(plan, progress, token);
            _mediaCard.IsProcessing = true;
            var replacements = await PrepareBootFilesAsync(token);

            await Task.Run(() => _usbMediaWriter.Write(target, WorkingDirectory, replacements, progress, token), token);
            _taskProgressService.CompleteTask();

            IsIsoCreated = true;
            written = true;
            _mediaCard.IsComplete = true;
            SelectUsbCard.IsComplete = true;
            SelectUsbCard.Description = _localizationService.GetString("WIMUtil_Desc_UsbWriteSuccess");

            await _dialogService.ShowInformationAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_UsbWriteSuccess"), target.Model),
                _localizationService.GetStringOrDefault("Dialog_Success", "Success"));
        }
        catch (InsufficientDiskSpaceException spaceEx)
        {
            _taskProgressService.FailTask();
            SelectUsbCard.HasFailed = true;
            SelectUsbCard.Description = string.Format(_localizationService.GetString("WIMUtil_Status_InsufficientDiskSpace"), spaceEx.DriveName);
            await _dialogService.ShowWarningAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_InsufficientSpace_Create"), spaceEx.DriveName, spaceEx.RequiredGB.ToString("F2"), spaceEx.AvailableGB.ToString("F2"), (spaceEx.RequiredGB - spaceEx.AvailableGB).ToString("F2")),
                string.Format(_localizationService.GetString("WIMUtil_Status_InsufficientDiskSpace"), spaceEx.DriveName));
        }
        catch (UsbMediaErasedException erased) when (erased.WasCancelled)
        {
            cancelled = true;
            _taskProgressService.CancelTask();
            SelectUsbCard.Description = _localizationService.GetString("WIMUtil_Desc_UsbWriteCancelled");
            await _dialogService.ShowWarningAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_UsbDriveErased"), erased.Target?.Model),
                _localizationService.GetString("WIMUtil_Desc_UsbWriteCancelled"));
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
            _taskProgressService.CancelTask();
            SelectUsbCard.Description = _localizationService.GetString("WIMUtil_Desc_UsbWriteCancelled");
        }
        catch (Exception ex)
        {
            _taskProgressService.FailTask();
            _logService.LogError($"Could not write the USB drive: {ex.Message}", ex);
            SelectUsbCard.HasFailed = true;
            SelectUsbCard.Description = string.Format(_localizationService.GetString("WIMUtil_Status_ErrorPrefix"), ex.Message);

            // Once the drive has been wiped, "could not write" on its own reads as "nothing
            // happened", and the user unplugs a blank stick.
            var message = string.Format(_localizationService.GetString("WIMUtil_Msg_UsbWriteFailed"), ex.Message);
            if (ex is UsbMediaErasedException failedAfterErase)
            {
                message += Environment.NewLine + Environment.NewLine
                    + string.Format(_localizationService.GetString("WIMUtil_Msg_UsbDriveErased"), failedAfterErase.Target?.Model);
            }

            await _dialogService.ShowErrorAsync(message);
        }
        finally
        {
            SelectUsbCard.IsEnabled = true;
            SettleBuildCards(failed: !written && !cancelled);
            _taskProgressService.CompleteTask();
        }

        if (answerFileChanged)
            await RecheckServicedAnswerFileAsync();
        return written;
    }
}

public enum WimOutputDestination
{
    Iso,
    Usb,
}
