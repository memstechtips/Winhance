namespace Winhance.Infrastructure.Features.AdvancedTools.Services;

internal interface IFileCopyNative
{
    // onProgress receives the bytes transferred so far, and fires repeatedly during a single
    // file rather than once per file - which is the whole point, because one Windows ISO is
    // about 976 files and one of them is 7 GB.
    void CopyWithProgress(
        string source,
        string destination,
        Action<long> onProgress,
        CancellationToken cancellationToken);
}
