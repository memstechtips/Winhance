using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.UI.Features.SoftwareApps.ViewModels;
using Winhance.UI.Features.WimUtil.Models;

namespace Winhance.UI.Features.WimUtil.ViewModels;

public partial class WimStep4UpdatesViewModel : ObservableObject, IMediaChoice
{
    private readonly IWimCustomizationService _wimCustomizationService;
    private readonly ILocalizationService _localizationService;
    private readonly ILogService _logService;
    private readonly IWindowsUpdateListService _updateListService;
    private readonly IWindowsUpdateStager _updateStager;
    private WindowsUpdate? _offer;
    private int _lookupVersion;
    private bool _isLookingUp;

    // The update is already staged on the media, so Create does not download it again.
    private bool _updateOnMedia;

    public string WorkingDirectory
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;

            // Another folder is another image, so the choice starts over.
            _updateOnMedia = false;
            AddUpdate = false;
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
            // A running lookup shows Looking, and writes the text for whichever mode is current when it ends.
            if (!_isLookingUp)
                ShowDescription();
        }
    }

    [ObservableProperty]
    public partial bool AddUpdate { get; set; }

    [ObservableProperty]
    public partial string UpdateDescription { get; set; }

    // Media with no update on the list, or none looked up yet, has nothing to tick.
    public bool CanAddUpdate => !_isLookingUp && (_offer is not null || _updateOnMedia);

    public string ApplyTitleKey => "WIMUtil_Build_PrepareUpdate";

    public FluentIcons.Common.Icon ApplyIcon => FluentIcons.Common.Icon.CloudArrowDown;

    public ServicingPhase? ServicedIn => ServicingPhase.Update;

    public bool NeedsApply => AddUpdate && !_updateOnMedia && _offer is not null;

    public WimStep4UpdatesViewModel(
        IWimCustomizationService wimCustomizationService,
        ILocalizationService localizationService,
        ILogService logService,
        IWindowsUpdateListService updateListService,
        IWindowsUpdateStager updateStager)
    {
        _wimCustomizationService = wimCustomizationService;
        _localizationService = localizationService;
        _logService = logService;
        _updateListService = updateListService;
        _updateStager = updateStager;

        UpdateDescription = _localizationService.GetString("WIMUtil_Card_AddUpdate_Description_None");
    }

    // x86 and media without a reported build or architecture have no update on the list.
    public async Task SetMediaAsync(ImageFormatInfo? media)
    {
        var version = ++_lookupVersion;
        _isLookingUp = false;
        _offer = null;

        if (media is not { Build: > 0, Architecture: "x64" or "arm64" })
        {
            ShowUpdateState();
            return;
        }

        _isLookingUp = true;
        OnPropertyChanged(nameof(CanAddUpdate));
        UpdateDescription = _localizationService.GetString("WIMUtil_Card_AddUpdate_Description_Looking");

        WindowsUpdate? offer;
        try
        {
            offer = await _updateListService.FindUpdateAsync(media.Build, media.Ubr, media.Architecture);
        }
        finally
        {
            if (version == _lookupVersion)
                _isLookingUp = false;
        }
        if (version != _lookupVersion)
            return;

        _offer = offer;
        ShowUpdateState();
    }

    // Read from the media, so a reopened folder that already holds the update shows it as chosen, and servicing moving
    // it into the image clears the choice.
    public void ShowUpdateState()
    {
        if (_isLookingUp)
            return;

        var staged = !string.IsNullOrEmpty(WorkingDirectory) && _updateStager.IsStaged(WorkingDirectory);
        if (staged)
        {
            _updateOnMedia = true;
            AddUpdate = true;
        }
        else if (_updateOnMedia || _offer is null)
        {
            _updateOnMedia = false;
            AddUpdate = false;
        }

        OnPropertyChanged(nameof(CanAddUpdate));
        OnPropertyChanged(nameof(NeedsApply));
        ShowDescription();
    }

    partial void OnAddUpdateChanged(bool value) => OnPropertyChanged(nameof(NeedsApply));

    // The description follows the mode, the offer and the language.
    public void ShowDescription()
    {
        if (_updateOnMedia)
        {
            var stagedKey = IsBuiltIntoImage ? "WIMUtil_Card_AddUpdate_Description_Staged_Image" : "WIMUtil_Card_AddUpdate_Description_Staged";
            UpdateDescription = _localizationService.GetString(stagedKey);
        }
        else if (_offer is { } offer)
        {
            var offerKey = IsBuiltIntoImage ? "WIMUtil_Card_AddUpdate_Description_Offer_Image" : "WIMUtil_Card_AddUpdate_Description_Offer";
            UpdateDescription = _localizationService.GetString(
                offerKey,
                offer.Kb,
                offer.ReleaseDate.ToString("d", CultureInfo.CurrentCulture),
                AppItemViewModel.FormatSize(offer.DownloadBytes));
        }
        else
        {
            UpdateDescription = _localizationService.GetString("WIMUtil_Card_AddUpdate_Description_None");
        }
    }

    public async Task<string?> ApplyAsync(IProgress<TaskProgressDetail> progress, CancellationToken cancellationToken)
    {
        if (_offer is not { } update)
            return null;

        try
        {
            var result = await _updateStager.StageAsync(WorkingDirectory, update, progress, cancellationToken);
            if (result != UpdateStageResult.Staged)
            {
                return _localizationService.GetString(result switch
                {
                    UpdateStageResult.HashMismatch => "WIMUtil_Msg_UpdateHashMismatch",
                    UpdateStageResult.SignatureInvalid => "WIMUtil_Msg_UpdateSignatureInvalid",
                    _ => "WIMUtil_Msg_UpdateDownloadFailed"
                });
            }

            await _wimCustomizationService.EnsureUpdateInstallStepAsync(WorkingDirectory, cancellationToken);
            _updateOnMedia = true;
            OnPropertyChanged(nameof(NeedsApply));
            ShowDescription();
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logService.LogError($"Error adding the Windows update: {ex.Message}", ex);
            return _localizationService.GetString("WIMUtil_Msg_UpdateError", ex.Message);
        }
    }
}
