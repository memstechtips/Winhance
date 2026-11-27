using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

internal interface IIsoImageWriter
{
    void Write(
        string workingDirectory,
        string outputPath,
        BootFileReplacements replacements,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken);
}
