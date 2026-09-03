namespace Winhance.Infrastructure.Features.AdvancedTools.Services;

// Mirrors FsiFileSystems. Windows Setup media uses UDF alone; the other two are here because they
// are the choices the API actually offers.
[Flags]
internal enum IsoFileSystems
{
    None = 0,
    Iso9660 = 1,
    Joliet = 2,
    Udf = 4,
}

internal enum BootPlatform
{
    // oscdimg's p0 / pEF. The values are IMAPI2's PlatformId, not an arbitrary ordering.
    BiosX86 = 0,
    Uefi = 0xEF,
}

internal readonly record struct BootEntry(BootPlatform Platform, string BootImagePath);

internal interface IIsoResultImage : IDisposable
{
    long TotalBytes { get; }

    void WriteTo(string outputPath, Action<long, long>? onProgress, CancellationToken cancellationToken);
}

internal interface IFileSystemImageWrapper : IDisposable
{
    IsoFileSystems FileSystemsToCreate { get; set; }

    int UdfRevision { get; set; }

    int FreeMediaBlocks { get; set; }

    bool StageFiles { get; set; }

    string VolumeName { get; set; }

    // Set-only, plus a count: IMAPI2 hands the entries back as opaque COM objects, so the count
    // is the only thing a read can honestly return, and it is what proves the assignment took.
    void SetBootImageOptions(IReadOnlyList<BootEntry> entries);

    int BootImageEntryCount { get; }

    void AddTree(string sourceDirectory, bool includeBaseDirectory);

    IIsoResultImage CreateResultImage();
}
