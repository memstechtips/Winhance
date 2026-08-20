using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

public sealed class BuilderSaveService : IBuilderSaveService
{
    private readonly ISelectionSetBuilder _selections;
    private readonly ISelectionSaveService _saves;
    private readonly IDialogService _dialogs;
    private readonly ILocalizationService _loc;
    private readonly ILogService _log;

    public BuilderSaveService(
        ISelectionSetBuilder selections,
        ISelectionSaveService saves,
        IDialogService dialogs,
        ILocalizationService loc,
        ILogService log)
    {
        _selections = selections;
        _saves = saves;
        _dialogs = dialogs;
        _loc = loc;
        _log = log;
    }

    public async Task SaveAsync(BuilderTarget target)
    {
        try
        {
            var set = await _selections.FromBuilderSessionAsync();
            await _saves.SaveAsync(target, set);
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Error, $"Builder Save failed: {ex.Message}");
            await _dialogs.ShowErrorAsync(
                _loc.GetString("Config_Export_Error_Message", ex.Message),
                _loc.GetString("Config_Export_Error_Title"));
        }
    }
}
