using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Services;

// The cards are already disabled in review, so a write reaching this class means an upstream gate let one
// through; refusing is the safe answer, and the warning is what makes the gap findable.
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
