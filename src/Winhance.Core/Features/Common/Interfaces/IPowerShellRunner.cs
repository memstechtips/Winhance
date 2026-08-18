using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IPowerShellRunner
{
    Task<string> RunScriptAsync(string script, IProgress<TaskProgressDetail>? progress = null, CancellationToken ct = default);

    // powershell.exe -EncodedCommand takes the script as base64 of UTF-16-LE; no temp file. The command-line limit
    // caps it at ~24 KB after encoding - larger scripts go through RunScriptAsync (temp file) or RunScriptFileAsync.
    Task<string> RunScriptInMemoryAsync(string script, IProgress<TaskProgressDetail>? progress = null, CancellationToken ct = default);

    Task<string> RunScriptFileAsync(string scriptPath, string arguments = "", IProgress<TaskProgressDetail>? progress = null, CancellationToken ct = default);
    Task ValidateScriptSyntaxAsync(string scriptContent, CancellationToken ct = default);
    Task ValidateXmlSyntaxAsync(string xmlContent, CancellationToken ct = default);
}
