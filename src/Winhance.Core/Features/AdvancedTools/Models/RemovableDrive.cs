namespace Winhance.Core.Features.AdvancedTools.Models;

// BusType is the disk's connection as MSFT_Disk reports it ("USB", "NVMe", ...). It is the filter
// that keeps internal disks off the list; the picker shows only USB drives and never displays it,
// so the log line that lists every disk is where the other names surface.
public sealed record RemovableDrive(
    int DiskNumber,
    string Model,
    long SizeBytes,
    string BusType,
    bool IsSystemDisk)
{
    public double SizeGigabytes => SizeBytes / (1024.0 * 1024 * 1024);

    // What the device picker shows. GB is the unit symbol in every shipped language, so this needs
    // no localization pass of its own. BusType is left out: every row in the picker is a USB drive.
    public string DisplayName => $"{Model} - {SizeGigabytes:F1} GB";
}
