using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Exceptions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Utilities;

internal class PowerShellRunner : IPowerShellRunner
{
    private const string PowerShellPath = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";

    // Windows command-line size limit is ~32,767 chars. base64-encoded UTF-16-LE
    // is ~4 chars per 2 bytes of script, so the effective script-size limit before
    // encoding is roughly 24 KB. We cap below that to give headroom for the
    // surrounding `-ExecutionPolicy Bypass -NoProfile -EncodedCommand ` prefix
    // (~50 chars) and any future arg additions.
    private const int MaxEncodedScriptBytes = 24_000;

    private static readonly Regex PercentRegex = new(@"(\d+(?:\.\d+)?)%", RegexOptions.Compiled);
    private readonly IFileSystemService _fileSystemService;

    public PowerShellRunner(IFileSystemService fileSystemService)
    {
        _fileSystemService = fileSystemService;
    }

    public async Task<string> RunScriptAsync(
        string script,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(script))
            throw new ArgumentException("Script cannot be null or empty.", nameof(script));

        var tempFile = _fileSystemService.CombinePath(_fileSystemService.GetTempPath(), $"winhance_{Guid.NewGuid()}.ps1");
        try
        {
            await _fileSystemService.WriteAllTextAsync(tempFile, script, ct).ConfigureAwait(false);
            return await RunScriptFileAsync(tempFile, "", progress, ct).ConfigureAwait(false);
        }
        finally
        {
            try { _fileSystemService.DeleteFile(tempFile); }
            catch { }
        }
    }

    public async Task<string> RunScriptInMemoryAsync(
        string script,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(script))
            throw new ArgumentException("Script cannot be null or empty.", nameof(script));

        var scriptBytes = Encoding.Unicode.GetBytes(script);
        if (scriptBytes.Length > MaxEncodedScriptBytes)
        {
            throw new ArgumentException(
                $"Script is {scriptBytes.Length} bytes (UTF-16); -EncodedCommand path supports up to {MaxEncodedScriptBytes}. Use RunScriptAsync for larger scripts.",
                nameof(script));
        }

        var encoded = Convert.ToBase64String(scriptBytes);
        var args = $"-ExecutionPolicy Bypass -NoProfile -EncodedCommand {encoded}";

        var (output, errors, exitCode) = await LaunchPowerShellAsync(args, progress, ct).ConfigureAwait(false);

        if (exitCode != 0 && errors.Length > 0)
        {
            throw new InvalidOperationException(
                $"In-memory PowerShell script failed (exit code {exitCode}):\n{errors}");
        }

        return output.ToString();
    }

    // If execution policy blocks the script, retries with -EncodedCommand.
    public async Task<string> RunScriptFileAsync(
        string scriptPath,
        string arguments = "",
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(scriptPath))
            throw new ArgumentException("Script path cannot be null or empty.", nameof(scriptPath));

        if (!_fileSystemService.FileExists(scriptPath))
            throw new FileNotFoundException($"PowerShell script file not found: {scriptPath}");

        var args = string.IsNullOrEmpty(arguments)
            ? $"-ExecutionPolicy Bypass -NoProfile -File \"{scriptPath}\""
            : $"-ExecutionPolicy Bypass -NoProfile -File \"{scriptPath}\" {arguments}";

        var (output, errors, exitCode) = await LaunchPowerShellAsync(args, progress, ct).ConfigureAwait(false);

        if (exitCode != 0 && errors.Length > 0)
        {
            var errorText = errors.ToString();

            if (IsExecutionPolicyError(errorText) && string.IsNullOrEmpty(arguments))
            {
                var scriptContent = await _fileSystemService.ReadAllTextAsync(scriptPath, ct).ConfigureAwait(false);

                // Guard: Base64 of Unicode doubles size; Windows command line limit ~32K
                if (scriptContent.Length > 28_000)
                {
                    throw new ExecutionPolicyException(
                        $"Execution policy blocked script and script is too large ({scriptContent.Length} chars) for -EncodedCommand fallback.\n{errorText}");
                }

                var base64 = Convert.ToBase64String(Encoding.Unicode.GetBytes(scriptContent));
                var fallbackArgs = $"-ExecutionPolicy Bypass -NoProfile -EncodedCommand {base64}";

                progress?.Report(new TaskProgressDetail
                {
                    TerminalOutput = "Execution policy blocked script file. Retrying with -EncodedCommand...",
                    IsActive = true,
                    LogLevel = LogLevel.Warning
                });

                var (retryOutput, retryErrors, retryExitCode) =
                    await LaunchPowerShellAsync(fallbackArgs, progress, ct).ConfigureAwait(false);

                if (retryExitCode == 0 || retryErrors.Length == 0)
                    return retryOutput.ToString();

                throw new ExecutionPolicyException(
                    $"Execution policy blocked script file and -EncodedCommand fallback also failed (exit code {retryExitCode}):\n{retryErrors}");
            }

            throw new InvalidOperationException(
                $"PowerShell execution failed (exit code {exitCode}):\n{errorText}");
        }

        return output.ToString();
    }

    private async Task<(StringBuilder Output, StringBuilder Errors, int ExitCode)> LaunchPowerShellAsync(
        string arguments, IProgress<TaskProgressDetail>? progress, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = PowerShellPath,
            Arguments = arguments,
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
            catch { }
        });

        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        return (output, errors, process.ExitCode);
    }

    private static bool IsExecutionPolicyError(string errorOutput)
    {
        if (string.IsNullOrEmpty(errorOutput)) return false;
        return errorOutput.Contains("running scripts is disabled", StringComparison.OrdinalIgnoreCase)
            || errorOutput.Contains("AuthorizationManager check failed", StringComparison.OrdinalIgnoreCase)
            || errorOutput.Contains("is not digitally signed", StringComparison.OrdinalIgnoreCase);
    }

    public async Task ValidateScriptSyntaxAsync(
        string scriptContent,
        CancellationToken ct = default)
    {
        const string name = "script";

        var errors = await FindParseErrorsAsync(new Dictionary<string, string> { [name] = scriptContent }, ct).ConfigureAwait(false);

        // The message keeps the PARSE_ERROR marker the syntax gate matches on.
        if (errors.Count > 0)
            throw new InvalidOperationException($"PowerShell script failed to parse:\nPARSE_ERROR: {errors[name]}");
    }

    public async Task<IReadOnlyDictionary<string, string>> FindParseErrorsAsync(
        IReadOnlyDictionary<string, string> scriptsByName,
        CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string>(StringComparer.Ordinal);
        if (scriptsByName.Count == 0)
            return errors;

        var names = scriptsByName.Keys.ToList();
        var batch = Guid.NewGuid().ToString("N");
        var tempFiles = names
            .Select((_, i) => _fileSystemService.CombinePath(_fileSystemService.GetTempPath(), $"winhance_parse_{batch}_{i}.ps1"))
            .ToList();
        try
        {
            // Windows PowerShell 5.1 reads a BOM-less file as ANSI, where one UTF-8 em dash decodes to a smart
            // quote and desyncs the parse. The autounattend extractor writes the script WITH a UTF-8 preamble,
            // so validate the same bytes Setup will run.
            var utf8WithPreamble = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            for (var i = 0; i < names.Count; i++)
            {
                var bytes = utf8WithPreamble.GetPreamble().Concat(utf8WithPreamble.GetBytes(scriptsByName[names[i]])).ToArray();
                await _fileSystemService.WriteAllBytesAsync(tempFiles[i], bytes, ct).ConfigureAwait(false);
            }

            var fileList = string.Join(", ", tempFiles.Select(f => "'" + f.Replace("'", "''", StringComparison.Ordinal) + "'"));
            var parseScript = @"
$files = @(" + fileList + @")
for ($i = 0; $i -lt $files.Count; $i++) {
    $errors = $null
    [System.Management.Automation.Language.Parser]::ParseFile($files[$i], [ref]$null, [ref]$errors)
    foreach ($e in $errors) { Write-Host (""PARSE_ERROR`t"" + $i + ""`tline "" + $e.Extent.StartLineNumber + "": "" + ($e.Message -replace '[\r\n]+', ' ')) }
}
Write-Host 'PARSE_DONE'
exit 0";

            var output = await RunScriptAsync(parseScript, ct: ct).ConfigureAwait(false);

            // The batch script reports through stdout and always exits 0, so a run that stopped partway
            // still looks like a success; only the sentinel proves every file reached the parser.
            if (!output.Contains("PARSE_DONE", StringComparison.Ordinal))
                throw new InvalidOperationException($"PowerShell parse batch did not complete:\n{output}");

            foreach (var rawLine in output.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');
                if (!line.StartsWith("PARSE_ERROR\t", StringComparison.Ordinal))
                    continue;

                var parts = line.Split('\t', 3);
                if (parts.Length < 3 || !int.TryParse(parts[1], out var index) || index < 0 || index >= names.Count)
                    continue;

                var name = names[index];
                errors[name] = errors.TryGetValue(name, out var previous) ? previous + Environment.NewLine + parts[2] : parts[2];
            }

            return errors;
        }
        finally
        {
            foreach (var tempFile in tempFiles)
            {
                try { _fileSystemService.DeleteFile(tempFile); }
                catch { }
            }
        }
    }

    public async Task ValidateXmlSyntaxAsync(
        string xmlContent,
        CancellationToken ct = default)
    {
        var tempFile = _fileSystemService.CombinePath(_fileSystemService.GetTempPath(), $"winhance_validate_{Guid.NewGuid():N}.xml");
        try
        {
            await _fileSystemService.WriteAllTextAsync(tempFile, xmlContent, ct).ConfigureAwait(false);

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

            var output = await RunScriptAsync(parseScript, ct: ct).ConfigureAwait(false);
            if (output.Contains("XML_ERROR:", StringComparison.Ordinal))
                throw new InvalidOperationException($"XML is not well-formed:\n{output}");
        }
        finally
        {
            try { _fileSystemService.DeleteFile(tempFile); }
            catch { }
        }
    }

    private void ReportLine(string line, IProgress<TaskProgressDetail>? progress, LogLevel defaultLevel)
    {
        if (progress == null || string.IsNullOrWhiteSpace(line)) return;

        var match = PercentRegex.Match(line);
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
