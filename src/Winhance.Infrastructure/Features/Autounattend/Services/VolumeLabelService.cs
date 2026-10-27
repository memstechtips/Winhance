using System.Globalization;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;

namespace Winhance.Infrastructure.Features.Autounattend.Services;

internal sealed class VolumeLabelService(IWmiApi wmiApi) : IOptionProvider
{
    private const string StorageNamespace = @"root\Microsoft\Windows\Storage";

    // MSFT_Disk.BusType: USB, SD and MMC are drives that leave with the user, so their labels are no target.
    private static readonly HashSet<ushort> RemovableBusTypes = [7, 12, 13];

    // MSFT_Volume.DriveType.
    private const uint Fixed = 3;

    // An NTFS label holds 32 characters.
    private const int MaxLabelLength = 32;

    public IReadOnlyList<OptionSource> Sources { get; } = [OptionSource.VolumeLabels];

    public IReadOnlyList<DynamicOption> Options(Setting setting, IDetectionContext context)
    {
        var labels = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var disk in wmiApi.Query(StorageNamespace, "MSFT_Disk", null) ?? [])
        {
            using (disk)
            {
                if (RemovableBusTypes.Contains(Convert.ToUInt16(disk.Get("BusType"), CultureInfo.InvariantCulture)))
                    continue;
                var number = Convert.ToInt32(disk.Get("Number"), CultureInfo.InvariantCulture);
                foreach (var partition in wmiApi.Query(StorageNamespace, "MSFT_Partition", $"DiskNumber = {number}") ?? [])
                {
                    using (partition)
                    {
                        foreach (var volume in partition.GetRelated("MSFT_Volume") ?? [])
                        {
                            using (volume)
                            {
                                if (Convert.ToUInt32(volume.Get("DriveType"), CultureInfo.InvariantCulture) == Fixed
                                    && volume.Get("FileSystemLabel") is string label && Accepts(setting, label))
                                    labels.Add(label);
                            }
                        }
                    }
                }
            }
        }

        return [.. labels.Select(label => new DynamicOption(label, label))];
    }

    // The new PC is matched at setup, so this PC has no chosen label to read back.
    public string? CurrentKey(Setting setting, IDetectionContext context) => null;

    public bool Accepts(Setting setting, string key) =>
        !string.IsNullOrWhiteSpace(key) && key.Length <= MaxLabelLength && !key.Any(char.IsControl);

    public string? ValueFor(OptionValue value, string key) =>
        throw new ArgumentOutOfRangeException(nameof(value), value, "A volume label is the key itself.");

    public Effect? EffectFor(Setting setting, string key) => null;
}
