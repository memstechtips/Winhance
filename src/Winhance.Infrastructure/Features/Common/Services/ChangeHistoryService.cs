using System.Text;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services;

// C:\ProgramData\Winhance\ChangeHistory.txt; append-only, localized at write time, UTF-8 with BOM, CRLF.
// Never throws - a failed receipt must never block the operation.
internal class ChangeHistoryService(
    IFileSystemService fileSystemService,
    ILocalizationService localizationService,
    ILogService logService) : IChangeHistoryService
{
    private static readonly Encoding Utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    private readonly object _lock = new();
    private int _batchDepth;
    private string? _pendingBatchHeader;

    private static string FilePath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Winhance",
        "ChangeHistory.txt");

    public void LogSettingChange(string displayName, string? localizedGroupName, string before, string after)
    {
        var label = FormatSettingLabel(displayName, localizedGroupName);
        WriteEntry(localizationService.GetStringOrDefault(
            "ChangeHistory_SettingChanged", $"{label}: {before} → {after}", label, before, after));
    }

    public void LogSettingAction(string displayName, string? localizedGroupName) =>
        WriteEntry(FormatSettingLabel(displayName, localizedGroupName));

    public void LogAppChange(string appDisplayName, AppChangeKind kind)
    {
        try
        {
            // The separator between label and name lives in the template, not here: French wants a space
            // before the colon and CJK a fullwidth one, and no translation can correct a colon baked into C#.
            var (key, fallback) = kind switch
            {
                AppChangeKind.Removed => ("ChangeHistory_AppRemoved", $"App removed: {appDisplayName}"),
                AppChangeKind.EnableStarted => ("ChangeHistory_AppEnableStarted", $"Enable started for {appDisplayName} in a separate window; success can only be determined there"),
                _ => ("ChangeHistory_AppInstalled", $"App installed: {appDisplayName}"),
            };
            WriteEntry(localizationService.GetStringOrDefault(key, fallback, appDisplayName));
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Warning, $"[ChangeHistoryService] Failed to write entry: {ex.Message}");
        }
    }

    public IDisposable BeginBatch(string localizedHeader)
    {
        lock (_lock)
        {
            _batchDepth++;
            if (_batchDepth == 1)
                _pendingBatchHeader = localizedHeader;
        }
        return new BatchScope(this);
    }

    public string GetFilePath()
    {
        lock (_lock)
        {
            try
            {
                EnsureFileExistsNoLock();
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"[ChangeHistoryService] Failed to create history file: {ex.Message}");
            }
        }
        return FilePath;
    }

    private static string FormatSettingLabel(string displayName, string? localizedGroupName) =>
        string.IsNullOrEmpty(localizedGroupName) ? displayName : $"{localizedGroupName} — {displayName}";

    private void WriteEntry(string line)
    {
        lock (_lock)
        {
            try
            {
                EnsureFileExistsNoLock();

                var sb = new StringBuilder();
                if (_pendingBatchHeader != null)
                {
                    sb.Append($"[{Timestamp()}] {_pendingBatchHeader}:\r\n");
                    _pendingBatchHeader = null;
                }
                var indent = _batchDepth > 0 ? "    " : string.Empty;
                sb.Append($"{indent}[{Timestamp()}] {line}\r\n");

                fileSystemService.AppendAllText(FilePath, sb.ToString(), Utf8Bom);
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"[ChangeHistoryService] Failed to write entry: {ex.Message}");
            }
        }
    }

    private void EnsureFileExistsNoLock()
    {
        if (fileSystemService.FileExists(FilePath))
            return;

        var directory = System.IO.Path.GetDirectoryName(FilePath)!;
        if (!fileSystemService.DirectoryExists(directory))
            fileSystemService.CreateDirectory(directory);

        var header = localizationService.GetString("ChangeHistory_FileHeader");
        if (string.IsNullOrEmpty(header))
            header = "Changes made by Winhance are listed below (newest at the bottom).";
        // Use AppendAllText with Utf8Bom so the file is created with the BOM preamble.
        // File.WriteAllText (the 2-arg overload) writes UTF-8 without BOM; AppendAllText
        // with UTF8Encoding(true) writes the BOM on the first call when the file is new.
        fileSystemService.AppendAllText(FilePath, $"{header}\r\n\r\n", Utf8Bom);
    }

    private static string Timestamp() => DateTime.Now.ToString("yyyy-MM-dd HH:mm");

    private sealed class BatchScope(ChangeHistoryService owner) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            lock (owner._lock)
            {
                owner._batchDepth--;
                if (owner._batchDepth <= 0)
                {
                    owner._batchDepth = 0;
                    owner._pendingBatchHeader = null;
                }
            }
        }
    }
}
