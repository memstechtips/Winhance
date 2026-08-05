using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Services;

/// <summary>
/// The write strategy for a mode that authors a configuration file instead of changing this
/// machine: record the edit, apply nothing, confirm nothing, warn about nothing.
///
/// It does not touch <see cref="ISettingWriteProgress"/>. Recording is instantaneous, and a
/// progress ring that flickers on every edit would be a visible defect.
/// </summary>
public sealed class BuilderSettingWriteStrategy : ISettingWriteStrategy
{
    private readonly IApplicationModeService _applicationModeService;
    private readonly ILogService _logService;

    public BuilderSettingWriteStrategy(
        IApplicationModeService applicationModeService,
        ILogService logService)
    {
        _applicationModeService = applicationModeService;
        _logService = logService;
    }

    public Task<SettingWriteResult> WriteAsync(
        SettingWriteRequest request,
        ISettingWriteProgress progress)
    {
        string settingId = request.SystemRequest.SettingId;

        if (request.AuthoredEdit is null)
        {
            // The caller could not express this edit in a serializable form. The session is still
            // marked dirty by the caller, so the user is warned before losing it — but it will not
            // survive a save, and a silent loss here is what made this class of gap so hard to
            // find the first time.
            _logService.Log(
                LogLevel.Warning,
                $"Builder cannot record an edit to '{settingId}' ({request.Description}): the input shape has no serializable form. It will not be saved.");
            return Task.FromResult(SettingWriteResult.Recorded);
        }

        _applicationModeService.RecordBuilderEdit(request.AuthoredEdit);
        _logService.LogDebug($"[BuilderWrite] Recorded {settingId}: {request.Description}");
        return Task.FromResult(SettingWriteResult.Recorded);
    }
}
