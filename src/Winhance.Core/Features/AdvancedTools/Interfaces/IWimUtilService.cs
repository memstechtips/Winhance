using System;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.AdvancedTools.Models;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.AdvancedTools.Interfaces
{
    public interface IWimUtilService
    {
        Task<ImageFormatInfo?> DetectImageFormatAsync(string workingDirectory);

        Task<bool> ConvertImageAsync(
            string workingDirectory,
            ImageFormat targetFormat,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default);
        Task<bool> ExtractIsoAsync(
            string isoPath,
            string workingDirectory,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default);

        Task<bool> AddXmlToImageAsync(
            string xmlPath,
            string workingDirectory);

        Task<string> DownloadUnattendedWinstallXmlAsync(
            string destinationPath,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default);

        Task<bool> AddDriversAsync(
            string workingDirectory,
            string? driverSourcePath = null,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default);

        Task<bool> CreateIsoAsync(
            string workingDirectory,
            string outputPath,
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default);

        Task<bool> EnsureOscdimgAvailableAsync(
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default);

        Task<bool> IsOscdimgAvailableAsync();

        string GetOscdimgPath();

        Task<bool> ValidateIsoFileAsync(string isoPath);

        Task<bool> CleanupWorkingDirectoryAsync(string workingDirectory);
    }
}
