using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

internal interface IIsoImageWriter
{
    void Write(
        string workingDirectory,
        string outputPath,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken);
}
