using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.WimUtil.Interfaces;

public interface IIsoService
{
    Task<bool> ValidateIsoFileAsync(string isoPath);

    Task<bool> ExtractIsoAsync(
        string isoPath,
        string workingDirectory,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default);

    Task<bool> CreateIsoAsync(
        string workingDirectory,
        string outputPath,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default);
}
