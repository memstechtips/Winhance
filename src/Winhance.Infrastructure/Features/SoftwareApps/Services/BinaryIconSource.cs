using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;

namespace Winhance.Infrastructure.Features.SoftwareApps.Services;

/// <summary>
/// Layer 1b orchestration: wraps <see cref="IShellImageFactory"/> with the
/// 8-second per-call timeout, exception-to-null translation, and empty-byte
/// guard. Mirrors the failure-as-null contract used by IAppxIconSource and
/// IRepoIconSource so AppIconResolver treats every layer uniformly.
/// </summary>
public class BinaryIconSource : IBinaryIconSource
{
    private static readonly TimeSpan PerCallTimeout = TimeSpan.FromSeconds(8);

    private readonly IShellImageFactory _factory;
    private readonly ILogService _logService;

    public BinaryIconSource(IShellImageFactory factory, ILogService logService)
    {
        _factory = factory;
        _logService = logService;
    }

    public async Task<Stream?> GetIconStreamAsync(string filePath, Size size, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(PerCallTimeout);

            var bytes = await _factory.GetIconBytesAsync(filePath, size, linked.Token).ConfigureAwait(false);
            if (bytes is null || bytes.Length == 0)
                return null;

            return new MemoryStream(bytes, writable: false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"BinaryIconSource: extraction failed for '{filePath}': {ex.Message}");
            return null;
        }
    }

    public async Task<Stream?> GetIconStreamByIndexAsync(string filePath, int iconSelector, Size size, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linked.CancelAfter(PerCallTimeout);

            var bytes = await _factory.GetIconBytesByIndexAsync(filePath, iconSelector, size, linked.Token).ConfigureAwait(false);
            if (bytes is null || bytes.Length == 0)
                return null;

            return new MemoryStream(bytes, writable: false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"BinaryIconSource: index extraction failed for '{filePath}' (selector={iconSelector}): {ex.Message}");
            return null;
        }
    }
}
