using System;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    /// <summary>
    /// Executes PowerShell scripts and validates script/XML syntax.
    /// </summary>
    public interface IPowerShellRunner
    {
        Task<string> RunScriptAsync(string script, IProgress<TaskProgressDetail>? progress = null, CancellationToken ct = default);
        Task<string> RunScriptFileAsync(string scriptPath, string arguments = "", IProgress<TaskProgressDetail>? progress = null, CancellationToken ct = default);
        Task ValidateScriptSyntaxAsync(string scriptContent, CancellationToken ct = default);
        Task ValidateXmlSyntaxAsync(string xmlContent, CancellationToken ct = default);
    }
}
