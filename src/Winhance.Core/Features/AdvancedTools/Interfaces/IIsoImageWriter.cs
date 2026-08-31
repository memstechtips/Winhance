using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.AdvancedTools.Interfaces;

public interface IIsoImageWriter
{
    void Write(
        string workingDirectory,
        string outputPath,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken);
}
