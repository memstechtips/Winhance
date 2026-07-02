using System;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// Appends user-facing entries to ChangeHistory.txt — the plain-language receipt of
/// every change Winhance makes (issue #367). Implementations MUST never throw:
/// a failed history write logs a warning and the actual operation proceeds.
/// All strings passed in are already localized; entries are written in whatever
/// language was active at the time of the change.
/// </summary>
public interface IChangeHistoryService
{
    /// <summary>One entry: "[ts] {group} — {name}: {before} → {after}" (group omitted when null).</summary>
    void LogSettingChange(string displayName, string? localizedGroupName, string before, string after);

    /// <summary>One entry for an Action-type setting that ran: "[ts] {group} — {name}".</summary>
    void LogSettingAction(string displayName, string? localizedGroupName);

    /// <summary>One entry: "[ts] {App installed|App removed}: {appName}".</summary>
    void LogAppChange(string appDisplayName, AppChangeKind kind);

    /// <summary>
    /// Starts a batch (config import, bulk action). The header line is written lazily
    /// when the first entry inside the batch arrives; entries inside are indented.
    /// Dispose the return value to end the batch. Nested batches join the outermost one.
    /// </summary>
    IDisposable BeginBatch(string localizedHeader);

    /// <summary>Ensures the file exists (writing the localized header if creating) and returns its full path. Never throws.</summary>
    string GetFilePath();
}
