using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.AdvancedTools.Services;

internal interface IIsoImageWriter
{
    void Write(
        string workingDirectory,
        string outputPath,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken);
}
