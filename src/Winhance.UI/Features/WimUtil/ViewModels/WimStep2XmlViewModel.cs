using System.Globalization;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.WimUtil.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.Common.Extensions;

namespace Winhance.UI.Features.WimUtil.ViewModels;

public partial class WimStep2XmlViewModel : ObservableObject, IMediaChoice, IDisposable
{
    private const string SetupLanguageId = "autounattend-setup-language";
    private const string EditionId = "autounattend-edition";

    private readonly IWimCustomizationService _wimCustomizationService;
    private readonly IDialogService _dialogService;
    private readonly ILocalizationService _localizationService;
    private readonly IFileSystemService _fileSystemService;
    private readonly IFilePickerService _filePickerService;
    private readonly ILogService _logService;
    private readonly IAnswerFileValidator _answerFileValidator;
    private readonly IAutounattendReader _autounattendReader;
    private readonly IApplicationModeService _applicationModeService;
    private readonly IBuilderModeEntry _builderModeEntry;
    private readonly IDispatcherService _dispatcherService;
    private readonly IEventBus _eventBus;
    private readonly AnswerFileCheckState _checkState;
    private readonly WimUtilSession _session;
    private readonly IWimImageService _wimImageService;
    private readonly ITargetWindowsService _targetWindows;
    private bool _disposed;
    private bool _joined;
    private bool _picked;
    private bool _declinedBuilder;
    private bool _ownXmlOnMedia;
    private string? _seededFolder;
    private string _workingDirectory = string.Empty;

    // Step 1 can move the folder while the session is open, and the next save must target the media being built now.
    // Other media is a fresh start, so what was picked for the old folder no longer stops its own file being picked up.
    public string WorkingDirectory
    {
        get => _workingDirectory;
        set
        {
            if (_workingDirectory == value) return;
            _workingDirectory = value;
            _picked = false;
            _seededFolder = null;
            ShareFolder();
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBuilderChosen))]
    [NotifyPropertyChangedFor(nameof(IsOwnXmlChosen))]
    [NotifyPropertyChangedFor(nameof(UsesOwnAnswerFile))]
    [NotifyPropertyChangedFor(nameof(OwnAnswerFileName))]
    public partial AnswerFileChoice Choice { get; set; }

    public bool IsBuilderChosen => Choice == AnswerFileChoice.Builder;
    public bool IsOwnXmlChosen => Choice == AnswerFileChoice.OwnXml;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsWinhanceFileOnMedia))]
    [NotifyPropertyChangedFor(nameof(ShowsOwnFileOnMedia))]
    public partial bool HasMediaAnswerFile { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowsWinhanceFileOnMedia))]
    [NotifyPropertyChangedFor(nameof(ShowsOwnFileOnMedia))]
    public partial bool MediaAnswerFileIsWinhance { get; set; }

    public bool ShowsWinhanceFileOnMedia => HasMediaAnswerFile && MediaAnswerFileIsWinhance;
    public bool ShowsOwnFileOnMedia => HasMediaAnswerFile && !MediaAnswerFileIsWinhance;

    [ObservableProperty]
    public partial string SelectedXmlPath { get; set; }

    [ObservableProperty]
    public partial string XmlStatus { get; set; }

    // True when the answer file comes from outside Winhance's builder.
    public bool UsesOwnAnswerFile => Choice == AnswerFileChoice.OwnXml;

    public string OwnAnswerFileName => Path.GetFileName(SelectedXmlPath);

    public bool IsAutounattendSession =>
        _applicationModeService.CurrentMode == WinhanceMode.Builder
        && _applicationModeService.CurrentBuilderTarget == BuilderTarget.Autounattend;

    public string ApplyTitleKey => "WIMUtil_Build_AddAnswerFile";

    public FluentIcons.Common.Icon ApplyIcon => FluentIcons.Common.Icon.DocumentAdd;

    public ServicingPhase? ServicedIn => null;

    // The file that is already on the media counts as applied, and nothing is copied until Create.
    public bool NeedsApply => Choice == AnswerFileChoice.OwnXml && !string.IsNullOrEmpty(SelectedXmlPath) && !_ownXmlOnMedia;

    // None is the unpicked state, which Create leaves alone. Setup still runs a file already on the media, so leaving
    // a Winhance XML session falls back to that file rather than to a state that hides it.
    private AnswerFileChoice Unpicked => _ownXmlOnMedia ? AnswerFileChoice.OwnXml : AnswerFileChoice.None;

    public WimStep2XmlViewModel(
        IWimCustomizationService wimCustomizationService,
        IDialogService dialogService,
        ILocalizationService localizationService,
        IFileSystemService fileSystemService,
        IFilePickerService filePickerService,
        ILogService logService,
        IAnswerFileValidator answerFileValidator,
        IAutounattendReader autounattendReader,
        IApplicationModeService applicationModeService,
        IBuilderModeEntry builderModeEntry,
        IDispatcherService dispatcherService,
        IEventBus eventBus,
        AnswerFileCheckState checkState,
        WimUtilSession session,
        IWimImageService wimImageService,
        ITargetWindowsService targetWindows)
    {
        _wimCustomizationService = wimCustomizationService;
        _dialogService = dialogService;
        _localizationService = localizationService;
        _fileSystemService = fileSystemService;
        _filePickerService = filePickerService;
        _logService = logService;
        _answerFileValidator = answerFileValidator;
        _autounattendReader = autounattendReader;
        _applicationModeService = applicationModeService;
        _builderModeEntry = builderModeEntry;
        _dispatcherService = dispatcherService;
        _eventBus = eventBus;
        _checkState = checkState;
        _session = session;
        _wimImageService = wimImageService;
        _targetWindows = targetWindows;

        SelectedXmlPath = string.Empty;
        XmlStatus = _localizationService.GetString("WIMUtil_Status_NoXmlAdded");
        Choice = IsAutounattendSession ? AnswerFileChoice.Builder : AnswerFileChoice.None;

        _applicationModeService.ModeChanged += OnModeChanged;
    }

    // An Autounattend session can run without ever opening this page, and a save from there must not pick up a stale
    // WIMUtil folder.
    private bool RecordsFolder => IsAutounattendSession && _joined;

    private void ShareFolder() => _session.WorkingDirectory = RecordsFolder ? WorkingDirectory : null;

    // Entering Builder again clears the session's choices, so the media is read again on the next visit.
    private void OnModeChanged(object? sender, EventArgs e)
    {
        _seededFolder = null;
        if (!IsAutounattendSession)
        {
            _joined = false;
            _session.Editions = [];
        }

        ShareFolder();

        _dispatcherService.RunOnUIThreadAsync(() =>
        {
            if (!IsAutounattendSession && Choice == AnswerFileChoice.Builder)
                Choice = Unpicked;
            else if (IsAutounattendSession && Choice == AnswerFileChoice.None && !_declinedBuilder)
                Choice = AnswerFileChoice.Builder;

            OnPropertyChanged(nameof(IsAutounattendSession));
            return Task.CompletedTask;
        }).FireAndForget(_logService);
    }

    [RelayCommand]
    private void ChooseBuilder()
    {
        _declinedBuilder = false;
        _picked = true;
        Choice = AnswerFileChoice.Builder;
    }

    [RelayCommand]
    private async Task ChooseOwnXmlAsync()
    {
        _declinedBuilder = true;
        if (string.IsNullOrEmpty(SelectedXmlPath) && !await PickOwnXmlAsync())
        {
            RefreshChoiceFlags();
            return;
        }

        _picked = true;
        Choice = AnswerFileChoice.OwnXml;
    }

    [RelayCommand]
    private async Task DeleteMediaAnswerFileAsync()
    {
        try
        {
            _fileSystemService.DeleteFile(MediaAnswerFilePath);
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error deleting the answer file on the media: {ex.Message}", ex);
            await _dialogService.ShowErrorAsync(_localizationService.GetString("WIMUtil_Msg_AnswerFileDeleteFailed", ex.Message));
            return;
        }

        if (_checkState.Subject == MediaAnswerFilePath)
            _checkState.Publish(null, null);
        await PickUpMediaAnswerFileAsync();
    }

    [RelayCommand]
    private async Task ChooseOwnXmlFileAsync()
    {
        if (await PickOwnXmlAsync())
        {
            _declinedBuilder = true;
            _picked = true;
            Choice = AnswerFileChoice.OwnXml;
        }
    }

    // A radio that did not take the click has to snap back, and a one-way bind only redraws on a change notification.
    private void RefreshChoiceFlags()
    {
        OnPropertyChanged(nameof(IsBuilderChosen));
        OnPropertyChanged(nameof(IsOwnXmlChosen));
    }

    partial void OnChoiceChanged(AnswerFileChoice oldValue, AnswerFileChoice newValue)
    {
        if (oldValue == AnswerFileChoice.OwnXml && newValue != AnswerFileChoice.OwnXml)
            _checkState.Publish(null, null);
    }

    // Opening the page joins the session to this media. Its editions go to the Unattend page, and its build becomes the
    // target Windows. A lone language or edition is picked too, since Setup would only stop to ask for it. An empty
    // folder is read again once Step 1 has put media in it.
    public async Task JoinSessionAsync()
    {
        if (!IsAutounattendSession)
            return;

        _joined = true;
        ShareFolder();
        if (_seededFolder == WorkingDirectory)
            return;

        try
        {
            var image = await MediaImageAsync();
            var edition = image?.Editions is [var only] ? MediaEditions.OptionFor(only.EditionId) : null;
            _session.Editions = image?.Editions ?? [];
            PreselectFromMedia(await MediaLanguageAsync(), edition);
            TargetMediaBuild(image is { Build: > 0 } ? image.Build : null);
            _seededFolder = image is null ? null : WorkingDirectory;
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Could not read the media for the session: {ex.Message}");
        }
    }

    [RelayCommand]
    private Task OpenAutounattendAsync()
    {
        _eventBus.Publish(new PageRequestedEvent { PageTag = "Autounattend" });
        return Task.CompletedTask;
    }

    partial void OnSelectedXmlPathChanged(string value) => OnPropertyChanged(nameof(OwnAnswerFileName));

    private async Task<ImageFormatInfo?> MediaImageAsync() =>
        string.IsNullOrEmpty(WorkingDirectory) ? null : await _wimImageService.DetectImageFormatAsync(WorkingDirectory);

    // The media is the Windows the file will install, so its build beats the default of the newest release.
    private void TargetMediaBuild(int? build)
    {
        if (build is { } b)
            _targetWindows.Set(b);
    }

    // Setup can only show a language the media carries, so media with exactly one leaves nothing to ask.
    private async Task<CultureInfo?> MediaLanguageAsync()
    {
        if (string.IsNullOrEmpty(WorkingDirectory))
            return null;

        var langIni = _fileSystemService.CombinePath(WorkingDirectory, "sources", "lang.ini");
        try
        {
            return _fileSystemService.FileExists(langIni)
                && MediaLanguages.Parse(await _fileSystemService.ReadAllTextAsync(langIni)) is [var only]
                    ? only
                    : null;
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Could not read the languages on the media: {ex.Message}");
            return null;
        }
    }

    // A language or edition the user already picked in this session wins over the media's. Media holding one edition
    // gets that edition's install key, or Setup would stop to ask which edition to install.
    private void PreselectFromMedia(CultureInfo? language, int? editionOption)
    {
        var seeded = false;
        if (language is not null && _applicationModeService.GetBuilderEdit(SetupLanguageId) is null)
        {
            _applicationModeService.RecordBuilderEdit(
                new SettingChoice(SetupLanguageId, new ChoiceValue.Keyed(language.Name, language.DisplayName)));
            seeded = true;
        }

        if (editionOption is { } option && _applicationModeService.GetBuilderEdit(EditionId) is null)
        {
            _applicationModeService.RecordBuilderEdit(new SettingChoice(EditionId, new ChoiceValue.Option(option)));
            seeded = true;
        }

        if (seeded)
            _eventBus.Publish(new BuilderSeededEvent());
    }

    // Picking records the file and checks it. Nothing reaches the media until Create.
    private async Task<bool> PickOwnXmlAsync()
    {
        try
        {
            var selectedPath = _filePickerService.PickFile(
                ["XML Files", "*.xml"],
                _localizationService.GetString("WIMUtil_FileDialog_SelectXml"));
            if (string.IsNullOrEmpty(selectedPath)) return false;

            XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlValidating");
            var isValidXml = await ValidateXmlFile(selectedPath);
            await PublishReportAsync(selectedPath);
            if (!isValidXml)
            {
                XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlInvalid");
                await _dialogService.ShowErrorAsync(
                    _localizationService.GetString("WIMUtil_Msg_XmlInvalidError"));
                return false;
            }

            _ownXmlOnMedia = false;
            SelectedXmlPath = selectedPath;
            XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlSelectSuccess");
            var reopened = await OpenWinhanceFileAsync(selectedPath, ask: true);
            if (reopened)
                Choice = AnswerFileChoice.Builder;
            return !reopened;
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error selecting XML: {ex.Message}", ex);
            XmlStatus = string.Format(_localizationService.GetString("WIMUtil_Status_ErrorPrefix"), ex.Message);
            await _dialogService.ShowErrorAsync(
                string.Format(_localizationService.GetString("WIMUtil_Msg_XmlSelectError"), ex.Message));
            return false;
        }
    }

    public async Task<string?> ApplyAsync(IProgress<TaskProgressDetail> progress, CancellationToken cancellationToken)
    {
        try
        {
            var addResult = await _wimCustomizationService.AddXmlToImageAsync(SelectedXmlPath, WorkingDirectory);
            return addResult.Success
                ? null
                : string.Format(_localizationService.GetString("WIMUtil_Msg_XmlAddFailed"), addResult.ErrorMessage);
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error adding the answer file: {ex.Message}", ex);
            return string.Format(_localizationService.GetString("WIMUtil_Msg_XmlAddFailed"), ex.Message);
        }
    }

    private string MediaAnswerFilePath => _fileSystemService.CombinePath(WorkingDirectory, "autounattend.xml");

    // Media from an earlier build or a custom ISO can already carry an answer file, and it installs just as if it had
    // been picked. A file Winhance made reopens as its session, asking only when that replaces changes already made.
    // The Winhance XML card is only the default until then, so the file still wins over it; a card the user clicked
    // for this media does not.
    public async Task PickUpMediaAnswerFileAsync()
    {
        try
        {
            var path = MediaAnswerFilePath;
            var onMedia = !string.IsNullOrEmpty(WorkingDirectory) && _fileSystemService.FileExists(path);
            MediaAnswerFileIsWinhance = onMedia && await IsWinhanceFileAsync(path);
            HasMediaAnswerFile = onMedia;
            if (!HasMediaAnswerFile)
            {
                if (_ownXmlOnMedia)
                {
                    _ownXmlOnMedia = false;
                    _picked = false;
                    SelectedXmlPath = string.Empty;
                    XmlStatus = _localizationService.GetString("WIMUtil_Status_NoXmlAdded");
                    Choice = IsAutounattendSession && !_declinedBuilder ? AnswerFileChoice.Builder : Unpicked;
                }
            }
            else if (!_picked)
            {
                _picked = true;
                _ownXmlOnMedia = true;
                SelectedXmlPath = path;
                XmlStatus = _localizationService.GetString("WIMUtil_Status_XmlOnMedia");
                Choice = AnswerFileChoice.OwnXml;
                await PublishReportAsync(path);
                if (await OpenWinhanceFileAsync(path, ask: false))
                    Choice = AnswerFileChoice.Builder;
            }
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error reading the answer file on the media: {ex.Message}", ex);
        }

        await JoinSessionAsync();
    }

    [RelayCommand]
    private async Task OpenSchneegansXmlGeneratorAsync()
    {
        try { await Windows.System.Launcher.LaunchUriAsync(new Uri("https://schneegans.de/windows/unattend-generator/")); }
        catch (Exception ex) { _logService.LogError($"Error opening Schneegans XML generator: {ex.Message}", ex); }
    }

    // A Winhance file too old to carry its choices cannot reopen as a session, so it is offered as the user's own.
    private async Task<bool> IsWinhanceFileAsync(string path)
    {
        try
        {
            return await _autounattendReader.ReadAsync(path) is { Config: not null };
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Could not read the choices in {path}: {ex.Message}");
            return false;
        }
    }

    // A file Winhance made carries the choices behind it, so it can reopen as a session to review or change. Replacing
    // changes already made in Builder always asks.
    private async Task<bool> OpenWinhanceFileAsync(string path, bool ask)
    {
        WinhanceAnswerFile? made;
        try
        {
            made = await _autounattendReader.ReadAsync(path);
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Could not read the choices in {path}: {ex.Message}");
            return false;
        }

        if (made is null)
            return false;

        var title = _localizationService.GetString("WIMUtil_OpenWinhanceFile_Title");
        if (made.Config is null)
        {
            await _dialogService.ShowInformationAsync(
                _localizationService.GetString("WIMUtil_OpenWinhanceFile_NoChoices", made.Version), title);
            return false;
        }

        var replaces = _applicationModeService.HasBuilderChanges;
        if (ask || replaces)
        {
            var message = _localizationService.GetString("WIMUtil_OpenWinhanceFile_Message");
            if (replaces)
                message += "\n\n" + _localizationService.GetString("WIMUtil_OpenWinhanceFile_Replaces");

            var answer = await _dialogService.ShowConfirmationAsync(new ConfirmationRequest
            {
                Title = title,
                Message = message,
                ConfirmButtonText = _localizationService.GetString("Button_Yes"),
                CancelButtonText = _localizationService.GetString("Button_No"),
            });
            if (!answer.Confirmed)
                return false;
        }

        // The file carries its own language, edition and Windows, so the media is not read into the new session again.
        var image = await MediaImageAsync();
        if (!await _builderModeEntry.EnterFromAsync(BuilderTarget.Autounattend, made.Config))
            return false;

        _joined = true;
        _seededFolder = WorkingDirectory;
        _session.Editions = image?.Editions ?? [];
        ShareFolder();
        return true;
    }

    // The banner shows where a picked file breaks, whether or not it parses.
    private async Task PublishReportAsync(string selectedPath)
    {
        try
        {
            var report = await _answerFileValidator.ValidateAsync(selectedPath);
            _checkState.Publish(selectedPath, report);
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Could not check the selected file: {ex.Message}");
        }
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _applicationModeService.ModeChanged -= OnModeChanged;
        GC.SuppressFinalize(this);
    }

}
