using System;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

/// <summary>
/// Executes PowerShell scripts and validates script/XML syntax.
/// </summary>
public interface IPowerShellRunner
{
    Task<string> RunScriptAsync(string script, IProgress<TaskProgressDetail>? progress = null, CancellationToken ct = default);

    /// <summary>
    /// Executes a short PowerShell script string entirely in memory via
    /// <c>powershell.exe -EncodedCommand &lt;base64&gt;</c>. No temp file is written.
    /// Suitable for toggle apply scripts (typically &lt; 3 KB).
    ///
    /// The script is encoded as UTF-16-LE then base64 — this is the format
    /// <c>powershell.exe</c>'s <c>-EncodedCommand</c> switch expects.
    ///
    /// Windows command-line limit caps the script at ~24 KB after encoding;
    /// for scripts larger than that, use <see cref="RunScriptAsync"/> (temp file)
    /// or <see cref="RunScriptFileAsync"/> (pre-written file) instead.
    /// </summary>
    Task<string> RunScriptInMemoryAsync(string script, IProgress<TaskProgressDetail>? progress = null, CancellationToken ct = default);

    Task<string> RunScriptFileAsync(string scriptPath, string arguments = "", IProgress<TaskProgressDetail>? progress = null, CancellationToken ct = default);
    Task ValidateScriptSyntaxAsync(string scriptContent, CancellationToken ct = default);
    Task ValidateXmlSyntaxAsync(string xmlContent, CancellationToken ct = default);
}
