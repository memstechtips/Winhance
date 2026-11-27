using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Interfaces;

// The plain-language receipt of every change Winhance makes (issue #367). Implementations MUST never throw: a
// failed history write logs a warning and the operation proceeds. Entries are written in whatever language was
// active at the time of the change.
public interface IChangeHistoryService
{
    void LogSettingChange(string displayName, string? localizedGroupName, string before, string after);

    void LogSettingAction(string displayName, string? localizedGroupName);

    void LogAppChange(string appDisplayName, AppChangeKind kind);

    // The header line is written lazily when the first entry inside the batch arrives; nested batches join the outermost one.
    IDisposable BeginBatch(string localizedHeader);

    string GetFilePath();
}
