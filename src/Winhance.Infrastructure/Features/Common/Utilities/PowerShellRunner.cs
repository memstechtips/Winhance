using System;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Utilities;

internal static class PowerShellRunner
{
    public static async Task<string> RunScriptAsync(
        string script,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(script))
            throw new ArgumentException("Script cannot be null or empty.", nameof(script));

        return await Task.Run(() =>
        {
            using var ps = PowerShell.Create();
            ps.AddScript(script);

            ps.Streams.Information.DataAdded += (_, e) =>
            {
                var record = ps.Streams.Information[e.Index];
                var text = record.MessageData?.ToString();
                var match = Regex.Match(text ?? "", @"(\d+(?:\.\d+)?)%");
                if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var pct))
                {
                    progress?.Report(new TaskProgressDetail
                    {
                        Progress = pct,
                        TerminalOutput = text,
                        IsActive = true
                    });
                }
                else
                {
                    progress?.Report(new TaskProgressDetail
                    {
                        TerminalOutput = text,
                        IsActive = true,
                        LogLevel = LogLevel.Info
                    });
                }
            };

            ps.Streams.Warning.DataAdded += (_, e) =>
            {
                var record = ps.Streams.Warning[e.Index];
                progress?.Report(new TaskProgressDetail
                {
                    TerminalOutput = record.Message,
                    IsActive = true,
                    LogLevel = LogLevel.Warning
                });
            };

            ps.Streams.Error.DataAdded += (_, e) =>
            {
                var record = ps.Streams.Error[e.Index];
                progress?.Report(new TaskProgressDetail
                {
                    TerminalOutput = record.Exception?.Message ?? record.ToString(),
                    IsActive = true,
                    LogLevel = LogLevel.Error
                });
            };

            var results = ps.Invoke();

            if (ps.HadErrors)
            {
                var errors = string.Join(Environment.NewLine,
                    ps.Streams.Error.Select(e => e.Exception?.Message ?? e.ToString()));
                throw new InvalidOperationException($"PowerShell execution failed:\n{errors}");
            }

            return string.Join(Environment.NewLine,
                results.Select(r => r?.ToString() ?? string.Empty));
        }, ct);
    }

    public static async Task<string> RunScriptFileAsync(
        string scriptPath,
        string arguments = "",
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(scriptPath))
            throw new ArgumentException("Script path cannot be null or empty.", nameof(scriptPath));

        if (!File.Exists(scriptPath))
            throw new FileNotFoundException($"PowerShell script file not found: {scriptPath}");

        var content = await File.ReadAllTextAsync(scriptPath, ct);
        return await RunScriptAsync(content, progress, ct);
    }
}
