using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Services;

/// <summary>
/// The write strategy for a mode where the settings are not the user's to change — config review,
/// where the pending decision is accept or reject, not edit.
///
/// The cards are already disabled there, so a write reaching this class means a gate upstream let
/// one through. Refusing is the safe answer, and the warning is what makes that gap findable
/// instead of silent.
/// </summary>
public sealed class ReadOnlySettingWriteStrategy : ISettingWriteStrategy
{
    private readonly ILogService _logService;

    public ReadOnlySettingWriteStrategy(ILogService logService)
    {
        _logService = logService;
    }

    public Task<SettingWriteResult> WriteAsync(
        SettingWriteRequest request,
        ISettingWriteProgress progress)
    {
        _logService.Log(
            LogLevel.Warning,
            $"Ignored an edit to '{request.SystemRequest.SettingId}' ({request.Description}): settings are read-only in the current mode.");

        return Task.FromResult(SettingWriteResult.Rejected);
    }
}
