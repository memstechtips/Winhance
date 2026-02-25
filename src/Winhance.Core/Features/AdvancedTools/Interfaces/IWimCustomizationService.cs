using System;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.AdvancedTools.Interfaces
{
    public interface IWimCustomizationService
    {
        Task<bool> AddDriversAsync(
            string workingDirectory,
            string? driverSourcePath = null,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default);

        Task<bool> AddXmlToImageAsync(
            string xmlPath,
            string workingDirectory);

        Task<string> DownloadUnattendedWinstallXmlAsync(
            string destinationPath,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default);
    }
}
