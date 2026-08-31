using Winhance.Core.Features.AdvancedTools.Models;

namespace Winhance.Infrastructure.Features.AdvancedTools.Services;

internal interface IStorageEnumerator
{
    IReadOnlyList<RemovableDrive> GetDisks();

    // The letters currently mounted from the disk's partitions; Clear takes them away, so the
    // writer reads them first to catch a working folder that lives on the target.
    IReadOnlyList<char> GetDriveLetters(int diskNumber);
}

// The Windows Storage Management API (WMI, root\Microsoft\Windows\Storage), NOT VDS: Microsoft's
// own VDS page opens with "superseded by the Windows Storage Management API" and adds "we strongly
// recommend using the Storage Management API".
internal interface IStorageOperations
{
    void Clear(int diskNumber);

    void EnsureMbr(int diskNumber);

    // Returns the new partition's number. Its drive letter comes last, after the format, the
    // way the diskpart recipe does it: a RAW volume with a letter is what Explorer greets with
    // its "you need to format the disk" prompt.
    int CreateActiveFat32Partition(int diskNumber);

    void FormatFat32(int diskNumber, int partitionNumber, string label);

    char AssignDriveLetter(int diskNumber, int partitionNumber);
}
