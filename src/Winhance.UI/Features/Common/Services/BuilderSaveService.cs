using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.WimUtil;

namespace Winhance.UI.Features.Common.Services;

public sealed class BuilderSaveService : IBuilderSaveService
{
    private readonly ISelectionSetBuilder _selections;
    private readonly ISelectionSaveService _saves;
    private readonly IAnswerFileValidator _answerFileValidator;
    private readonly IDialogService _dialogs;
    private readonly ILocalizationService _loc;
    private readonly ILogService _log;

    public BuilderSaveService(
        ISelectionSetBuilder selections,
        ISelectionSaveService saves,
        IAnswerFileValidator answerFileValidator,
        IDialogService dialogs,
        ILocalizationService loc,
        ILogService log)
    {
        _selections = selections;
        _saves = saves;
        _answerFileValidator = answerFileValidator;
        _dialogs = dialogs;
        _loc = loc;
        _log = log;
    }

    public async Task SaveAsync(BuilderTarget target)
    {
        try
        {
            var set = await _selections.FromBuilderSessionAsync();
            string? written = await _saves.SaveAsync(target, set);
            if (written is not null && target == BuilderTarget.Autounattend)
                await CheckAnswerFileAsync(written);
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Error, $"Builder Save failed: {ex.Message}");
            await _dialogs.ShowErrorAsync(
                _loc.GetString("Config_Export_Error_Message", ex.Message),
                _loc.GetString("Config_Export_Error_Title"));
        }
    }

    // The verdict never undoes the save: the user sees the findings and decides.
    private async Task CheckAnswerFileAsync(string path)
    {
        try
        {
            var report = await _answerFileValidator.ValidateAsync(path);
            if (report.Findings.Count > 0)
            {
                await _dialogs.ShowTaskOutputDialogAsync(
                    _loc.GetString("WIMUtil_AnswerFile_DialogTitle"),
                    AnswerFileReportDialog.Lines(report, _loc));
            }
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Warning, $"Could not check {path}: {ex.Message}");
        }
    }
}
