using System.Globalization;
using Winhance.Core.Features.AdvancedTools.Models;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.AdvancedTools.Services;

internal sealed class WmiStorageService(IStorageManagementApi api, ILogService logService) : IStorageOperations
{
    // MSFT_Disk's PartitionStyle. MBR, not GPT: one MBR + FAT32 + active partition boots both
    // firmware types, which is exactly why Microsoft's instructions specify FAT32. IsActive is
    // documented as "only valid when the disk's PartitionStyle property is MBR".
    private const ushort PartitionStyleMbr = 1;
    private const ushort PartitionStyleGpt = 2;

    // MSFT_Disk.Initialize: "the disk has already been initialized".
    private const uint AlreadyInitialized = 41001;

    // MSFT_Partition.DeleteObject: "the partition was deleted, although its access paths were not".
    // The partition is gone either way, which is all this needs.
    private const uint DeletedButAccessPathsRemain = 42000;

    // MSFT_Disk.CreatePartition's MbrType for FAT32.
    private const ushort MbrTypeFat32 = 12;

    public IReadOnlyList<RemovableDrive> GetDisks()
    {
        var disks = new List<RemovableDrive>();
        foreach (var disk in api.Query("MSFT_Disk", null))
        {
            using (disk)
            {
                disks.Add(new RemovableDrive(
                    Convert.ToInt32(disk.Get("Number"), CultureInfo.InvariantCulture),
                    disk.Get("FriendlyName") as string ?? "Unknown device",
                    Convert.ToInt64(disk.Get("Size"), CultureInfo.InvariantCulture),
                    DescribeBusType(Convert.ToUInt16(disk.Get("BusType"), CultureInfo.InvariantCulture)),
                    Convert.ToBoolean(disk.Get("IsSystem"), CultureInfo.InvariantCulture)));
            }
        }

        return disks;
    }

    public IReadOnlyList<char> GetDriveLetters(int diskNumber)
    {
        var letters = new List<char>();
        foreach (var partition in GetPartitions(diskNumber))
        {
            using (partition)
            {
                var letter = ReadDriveLetter(partition);
                if (letter != '\0')
                {
                    letters.Add(letter);
                }
            }
        }

        return letters;
    }

    public void Clear(int diskNumber)
    {
        using var disk = GetDisk(diskNumber);
        Invoke(disk, "Clear", new Dictionary<string, object>
        {
            ["RemoveData"] = true,
            ["RemoveOEM"] = true,
            ["ZeroOutEntireDisk"] = false,
        }, $"clear disk {diskNumber}").Dispose();
        logService.LogInformation($"Cleared disk {diskNumber}");

        DeleteSurvivingPartitions(diskNumber);
    }

    // Clear reports success on a stick Windows formatted as a super floppy and changes nothing: the
    // volume sits on the raw device with no partition table to remove, so it keeps the whole disk and
    // CreatePartition has nowhere to go (40000, not enough free space). Deleting the partition Windows
    // synthesises for that volume is what frees the extent. Measured on a SanDisk Cruzer Blade,
    // 2026-08-27: Clear left one partition at offset 0 and LargestFreeExtent at 0 for 30 seconds.
    private void DeleteSurvivingPartitions(int diskNumber)
    {
        foreach (var partition in GetPartitions(diskNumber))
        {
            using (partition)
            {
                Invoke(partition, "DeleteObject", null, $"delete a partition on disk {diskNumber}",
                    DeletedButAccessPathsRemain).Dispose();
                logService.LogInformation($"Deleted a partition that survived clearing disk {diskNumber}");
            }
        }

        // Cheaper to say so here than to let CreatePartition fail with a bare 40000, which is what
        // three rounds of this bug looked like from the outside.
        var free = ReadLargestFreeExtent(diskNumber);
        if (free == 0)
        {
            throw new InvalidOperationException(
                $"Disk {diskNumber} still reports no free space after being cleared.");
        }

        logService.LogInformation($"Disk {diskNumber} has {free:N0} bytes free");
    }

    // Clear is documented as returning a disk "to a RAW state", and on the super-floppy stick above it
    // did nothing at all, so the disk still read MBR here. Initialize takes a RAW disk only and fails
    // with 41001 on anything else, and Microsoft's own diskpart sequence for a bootable USB has no
    // initialize step at all. So ask the disk what it is rather than telling it.
    public void EnsureMbr(int diskNumber)
    {
        var style = ReadPartitionStyle(diskNumber);
        if (style == PartitionStyleMbr)
        {
            logService.LogInformation($"Disk {diskNumber} is already MBR");
            return;
        }

        // ConvertStyle is the documented route off GPT; it refuses a disk that still holds
        // partitions (41013), which is why Clear runs first.
        var method = style == PartitionStyleGpt ? "ConvertStyle" : "Initialize";

        using var disk = GetDisk(diskNumber);

        // Windows can initialize the disk between the read above and this call - it does so on its
        // own for removable media - so 41001 from Initialize is tolerated. It says nothing about
        // which style Windows chose, and ConvertStyle has no such race, hence the read-back below.
        var benign = method == "Initialize" ? AlreadyInitialized : (uint?)null;
        Invoke(disk, method, new Dictionary<string, object> { ["PartitionStyle"] = PartitionStyleMbr },
            $"make disk {diskNumber} MBR", benign).Dispose();

        var after = ReadPartitionStyle(diskNumber);
        if (after != PartitionStyleMbr)
        {
            Invoke(disk, "Refresh", null, $"refresh disk {diskNumber}").Dispose();
            after = ReadPartitionStyle(diskNumber);
        }

        // IsActive is only honoured on MBR, so anything else here is a stick that boots on UEFI
        // and does nothing on a BIOS machine, with no error anywhere to say why.
        if (after != PartitionStyleMbr)
        {
            throw new InvalidOperationException(
                $"Disk {diskNumber} is still partition style {after} after {method}; an MBR disk is required.");
        }

        logService.LogInformation($"Disk {diskNumber} is MBR");
    }

    public int CreateActiveFat32Partition(int diskNumber)
    {
        using var disk = GetDisk(diskNumber);
        var parameters = new Dictionary<string, object>
        {
            ["MbrType"] = MbrTypeFat32,
            ["IsActive"] = true,
        };

        // Windows' own formatter refuses FAT32 past 32 GB, so on a bigger stick the partition
        // stops there and the rest of the drive stays unallocated.
        var free = ReadLargestFreeExtent(diskNumber);
        if (free > (ulong)UsbWriteLayoutPlanner.Fat32MaxVolumeBytes)
        {
            parameters["Size"] = (ulong)UsbWriteLayoutPlanner.Fat32MaxVolumeBytes;
        }
        else
        {
            parameters["UseMaximumSize"] = true;
        }

        // No AssignDriveLetter: the letter is assigned after the format.
        using var result = Invoke(disk, "CreatePartition", parameters, $"partition disk {diskNumber}");

        // The method page types CreatedPartition as a String; the wire carries the embedded
        // MSFT_Partition itself, not a path to one.
        using var created = result.Output.Get("CreatedPartition") as IStorageInstance
            ?? throw new InvalidOperationException($"CreatePartition on disk {diskNumber} returned no partition.");
        var partitionNumber = Convert.ToInt32(created.Get("PartitionNumber"), CultureInfo.InvariantCulture);

        logService.LogInformation($"Created active FAT32 partition {partitionNumber} on disk {diskNumber}");
        return partitionNumber;
    }

    public void FormatFat32(int diskNumber, int partitionNumber, string label)
    {
        using var volume = GetVolume(diskNumber, partitionNumber);

        // A full format zeroes the whole stick, which on a 64 GB drive is minutes of nothing.
        Invoke(volume, "Format", new Dictionary<string, object>
        {
            ["FileSystem"] = "FAT32",
            ["FileSystemLabel"] = label,
            ["Full"] = false,
        }, $"format partition {partitionNumber} on disk {diskNumber}").Dispose();
        logService.LogInformation($"Formatted partition {partitionNumber} on disk {diskNumber} as FAT32");
    }

    public char AssignDriveLetter(int diskNumber, int partitionNumber)
    {
        // Windows may already have mounted the volume with a letter of its own; only ask when
        // it has not.
        using var partition = GetPartition(diskNumber, partitionNumber);
        var letter = ReadDriveLetter(partition);
        if (!char.IsLetter(letter))
        {
            Invoke(partition, "AddAccessPath", new Dictionary<string, object> { ["AssignDriveLetter"] = true },
                $"assign a drive letter to partition {partitionNumber} on disk {diskNumber}").Dispose();

            using var refreshed = GetPartition(diskNumber, partitionNumber);
            letter = ReadDriveLetter(refreshed);
        }

        if (!char.IsLetter(letter))
        {
            throw new InvalidOperationException(
                $"Partition {partitionNumber} on disk {diskNumber} still has no drive letter.");
        }

        logService.LogInformation($"Partition {partitionNumber} on disk {diskNumber} is mounted at {letter}:");
        return letter;
    }

    private ushort ReadPartitionStyle(int diskNumber)
    {
        using var disk = GetDisk(diskNumber);
        return Convert.ToUInt16(disk.Get("PartitionStyle"), CultureInfo.InvariantCulture);
    }

    private ulong ReadLargestFreeExtent(int diskNumber)
    {
        using var disk = GetDisk(diskNumber);
        return Convert.ToUInt64(disk.Get("LargestFreeExtent"), CultureInfo.InvariantCulture);
    }

    private static char ReadDriveLetter(IStorageInstance partition) =>
        (char)Convert.ToUInt16(partition.Get("DriveLetter"), CultureInfo.InvariantCulture);

    private IReadOnlyList<IStorageInstance> GetPartitions(int diskNumber) =>
        api.Query("MSFT_Partition", $"DiskNumber = {diskNumber}");

    private IStorageInstance GetDisk(int diskNumber) =>
        Single("MSFT_Disk", $"Number = {diskNumber}", $"Disk {diskNumber}");

    private IStorageInstance GetPartition(int diskNumber, int partitionNumber) =>
        Single("MSFT_Partition", $"DiskNumber = {diskNumber} AND PartitionNumber = {partitionNumber}",
            $"Partition {partitionNumber} on disk {diskNumber}");

    private IStorageInstance Single(string className, string condition, string what)
    {
        var matches = api.Query(className, condition);
        if (matches.Count == 0)
        {
            throw new InvalidOperationException($"{what} is no longer present.");
        }

        foreach (var extra in matches.Skip(1))
        {
            extra.Dispose();
        }

        return matches[0];
    }

    // MSFT_Volume carries no disk or partition number; the MSFT_PartitionToVolume association is
    // the only way from one to the other, and it is how Get-Partition | Get-Volume gets there too.
    private IStorageInstance GetVolume(int diskNumber, int partitionNumber)
    {
        using var partition = GetPartition(diskNumber, partitionNumber);
        var volumes = partition.GetRelated("MSFT_Volume");
        if (volumes.Count == 0)
        {
            throw new InvalidOperationException(
                $"Partition {partitionNumber} on disk {diskNumber} has no volume to format.");
        }

        foreach (var extra in volumes.Skip(1))
        {
            extra.Dispose();
        }

        return volumes[0];
    }

    private static StorageMethodResult Invoke(
        IStorageInstance target,
        string method,
        IReadOnlyDictionary<string, object>? parameters,
        string description,
        uint? benignReturnValue = null)
    {
        var result = target.Invoke(method, parameters);
        if (result.ReturnValue == 0 || result.ReturnValue == benignReturnValue)
        {
            return result;
        }

        using (result)
        {
            throw new InvalidOperationException(
                $"Could not {description}: {DescribeFailure(result.ReturnValue, result.Output)}");
        }
    }

    // The API's own text where it supplies one; its return codes are far more actionable than a
    // generic failure - 41000 tells the user the disk was never initialized, "operation failed"
    // tells them nothing.
    private static string DescribeFailure(uint returnValue, IStorageInstance output)
    {
        var extended = ReadExtendedStatus(output);
        if (!string.IsNullOrWhiteSpace(extended))
        {
            return $"{extended} (code {returnValue})";
        }

        return returnValue switch
        {
            40001 => "access denied (code 40001)",
            41000 => "the disk has not been initialized (code 41000)",
            41001 => "the disk has already been initialized (code 41001)",
            41013 => "the disk still holds partitions (code 41013)",
            42010 => "the operation is not allowed on a system or critical partition (code 42010)",
            _ => $"the Storage Management API returned code {returnValue}",
        };
    }

    // The method pages type this as a String; the wire carries an embedded MSFT_StorageExtendedStatus.
    // Read both, or a failure arrives as a bare number.
    private static string? ReadExtendedStatus(IStorageInstance output)
    {
        var value = output.Get("ExtendedStatus");
        if (value is string text)
        {
            return text;
        }

        if (value is IStorageInstance status)
        {
            using (status)
            {
                return status.Get("Message") as string ?? status.Get("CIMStatusCodeDescription") as string;
            }
        }

        return null;
    }

    // MSFT_Disk.BusType. Only USB is load-bearing (it is what makes a disk a candidate target); the
    // rest exist so the log line that lists every disk can say what an excluded one actually is.
    private static string DescribeBusType(ushort busType) => busType switch
    {
        1 => "SCSI",
        2 => "ATAPI",
        3 => "ATA",
        4 => "1394",
        5 => "SSA",
        6 => "Fibre Channel",
        7 => "USB",
        8 => "RAID",
        9 => "iSCSI",
        10 => "SAS",
        11 => "SATA",
        12 => "SD",
        13 => "MMC",
        15 => "Virtual",
        16 => "Storage Spaces",
        17 => "NVMe",
        _ => "Unknown",
    };
}
