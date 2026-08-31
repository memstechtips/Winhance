namespace Winhance.Infrastructure.Features.AdvancedTools.Services;

internal interface IFileCopyNative
{
    // onProgress receives (bytes transferred so far, size of the file), and fires repeatedly
    // during a single file rather than once per file - which is the whole point, because one
    // Windows ISO is about 976 files and one of them is 7 GB.
    void CopyWithProgress(
        string source,
        string destination,
        Action<long, long> onProgress,
        CancellationToken cancellationToken);
}
