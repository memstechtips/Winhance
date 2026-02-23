using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Utilities;

public static class PowerShellRunner
{
    private const string PowerShellPath = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";

    /// <summary>
    /// Executes a PowerShell script string via Windows PowerShell 5.1 (powershell.exe).
    /// The script is written to a temp file, executed, and the temp file is cleaned up.
    /// Stdout is captured line-by-line for progress reporting (Write-Host output).
    /// </summary>
    public static async Task<string> RunScriptAsync(
        string script,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(script))
            throw new ArgumentException("Script cannot be null or empty.", nameof(script));

        var tempFile = Path.Combine(Path.GetTempPath(), $"winhance_{Guid.NewGuid()}.ps1");
        try
        {
            await File.WriteAllTextAsync(tempFile, script, ct).ConfigureAwait(false);
            return await RunScriptFileAsync(tempFile, "", progress, ct).ConfigureAwait(false);
        }
        finally
        {
            try { File.Delete(tempFile); }
            catch { /* best effort cleanup */ }
        }
    }

    /// <summary>
    /// Executes a PowerShell script file via Windows PowerShell 5.1 (powershell.exe).
    /// Stdout is captured line-by-line for progress reporting (Write-Host output).
    /// </summary>
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

        var args = string.IsNullOrEmpty(arguments)
            ? $"-ExecutionPolicy Bypass -NoProfile -File \"{scriptPath}\""
            : $"-ExecutionPolicy Bypass -NoProfile -File \"{scriptPath}\" {arguments}";

        var psi = new ProcessStartInfo
        {
            FileName = PowerShellPath,
            Arguments = args,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        var output = new StringBuilder();
        var errors = new StringBuilder();

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            output.AppendLine(e.Data);
            ReportLine(e.Data, progress, LogLevel.Info);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            errors.AppendLine(e.Data);
            ReportLine(e.Data, progress, LogLevel.Error);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var reg = ct.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch { /* process may have already exited */ }
        });

        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        if (process.ExitCode != 0 && errors.Length > 0)
        {
            throw new InvalidOperationException(
                $"PowerShell execution failed (exit code {process.ExitCode}):\n{errors}");
        }

        return output.ToString();
    }

    /// <summary>
    /// Validates a PowerShell script for syntax errors without executing it.
    /// Uses PowerShell's built-in Parser.ParseFile() API.
    /// </summary>
    public static async Task ValidateScriptSyntaxAsync(
        string scriptContent,
        CancellationToken ct = default)
    {
        // Write script to temp file for parsing
        var tempFile = Path.Combine(Path.GetTempPath(), $"winhance_validate_{Guid.NewGuid():N}.ps1");
        try
        {
            await File.WriteAllTextAsync(tempFile, scriptContent, ct).ConfigureAwait(false);

            // Use PowerShell's parser to check for syntax errors
            var parseScript = @"
$errors = $null
[System.Management.Automation.Language.Parser]::ParseFile('" + tempFile.Replace("'", "''") + @"', [ref]$null, [ref]$errors)
if ($errors.Count -gt 0) {
    foreach ($e in $errors) { Write-Host ""PARSE_ERROR: $($e.ToString())"" }
    exit 1
}
Write-Host 'Script validation passed - no parse errors found'
exit 0";

            await RunScriptAsync(parseScript, ct: ct).ConfigureAwait(false);
        }
        finally
        {
            try { File.Delete(tempFile); }
            catch { /* best effort cleanup */ }
        }
    }

    /// <summary>
    /// Validates an XML string for well-formedness errors without writing it.
    /// Uses .NET's XmlReader via PowerShell.
    /// </summary>
    public static async Task ValidateXmlSyntaxAsync(
        string xmlContent,
        CancellationToken ct = default)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"winhance_validate_{Guid.NewGuid():N}.xml");
        try
        {
            await File.WriteAllTextAsync(tempFile, xmlContent, ct).ConfigureAwait(false);

            var parseScript = @"
try {
    $settings = New-Object System.Xml.XmlReaderSettings
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Ignore
    $reader = [System.Xml.XmlReader]::Create('" + tempFile.Replace("'", "''") + @"', $settings)
    while ($reader.Read()) { }
    $reader.Close()
    Write-Host 'XML validation passed - document is well-formed'
    exit 0
} catch {
    Write-Host ""XML_ERROR: $($_.Exception.Message)""
    exit 1
}";

            await RunScriptAsync(parseScript, ct: ct).ConfigureAwait(false);
        }
        finally
        {
            try { File.Delete(tempFile); }
            catch { /* best effort cleanup */ }
        }
    }

    private static void ReportLine(string line, IProgress<TaskProgressDetail>? progress, LogLevel defaultLevel)
    {
        if (progress == null || string.IsNullOrWhiteSpace(line)) return;

        var match = Regex.Match(line, @"(\d+(?:\.\d+)?)%");
        if (match.Success && double.TryParse(match.Groups[1].Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var pct))
        {
            progress.Report(new TaskProgressDetail
            {
                Progress = pct,
                TerminalOutput = line,
                IsActive = true
            });
        }
        else
        {
            progress.Report(new TaskProgressDetail
            {
                TerminalOutput = line,
                IsActive = true,
                LogLevel = defaultLevel
            });
        }
    }
}
