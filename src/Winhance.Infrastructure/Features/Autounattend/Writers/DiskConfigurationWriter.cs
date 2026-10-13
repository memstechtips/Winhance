using System.Globalization;
using System.Xml.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Localization;

namespace Winhance.Infrastructure.Features.Autounattend.Writers;

// Builds DiskConfiguration and the ImageInstall target that has to agree with it, because CreatePartition is a
// repeated element no single target path can address.
// The layout is Microsoft's own unattend sample for a UEFI system, the one on the CreatePartition/Type page:
// recovery, then the ESP, then the MSR, then Windows taking the rest, with the MSR skipped in ModifyPartitions
// and InstallTo naming the fourth partition. Recovery comes FIRST because CreatePartition has no equivalent of
// diskpart's SHRINK, so the only partition that can claim the remaining space is the last one, and that has to
// be Windows. The deployment guide would rather see recovery immediately after Windows so a future update can
// grow it; that is not expressible here, which is why the card defaults to 1000 MB rather than the sample's
// 250 - the guide's own floor is "990MB with a minimum of 250MB of free space".
internal sealed class DiskConfigurationWriter : IAutounattendElementWriter
{
    // The GPT type that marks a partition as Windows RE tools, spelled as the unattend sample spells it. Its MBR
    // counterpart is documented only in diskpart terms ("use type 0x27", "set id=27"); no Microsoft page shows an
    // MBR TypeID inside an answer file, so the hex spelling here follows the GPT one.
    private const string RecoveryTypeGpt = "de94bba4-06d1-4d40-a16a-bfd50179d6ac";
    private const string RecoveryTypeMbr = "0x27";

    // "The size of the MSR is 16 MB", from the UEFI/GPT deployment guide, the page Microsoft still updates. The
    // CreatePartition/Type reference says 32 MB under 16 GB and 128 MB above it, and its own sample writes 128;
    // that page has not moved since 2020, and the diskpart script the current guide ships uses size=16. Nothing
    // reads this partition, so the smaller reservation is the one to follow.
    private const int MsrSizeMb = 16;

    private static readonly XNamespace U = AutounattendDocument.Unattend;

    public IReadOnlyCollection<string> Handles =>
    [
        "autounattend-disk",
        "autounattend-disk-number",
        "autounattend-disk-system-size",
        "autounattend-disk-recovery-size",
    ];

    public void Write(XDocument doc, AutounattendRenderContext context)
    {
        var setting = SettingCatalog.ById["autounattend-disk"];
        if (context.StateOf("autounattend-disk") is not { } chosen)
            return;

        bool gpt = chosen.Label == LocKey.Setting.AutounattendDisk.Option1;
        bool mbr = chosen.Label == LocKey.Setting.AutounattendDisk.Option2;

        // Setup asking is the file saying nothing: no DiskConfiguration element, no ImageInstall target.
        if (!gpt && !mbr)
            return;

        int diskId = Megabytes(context.Text("autounattend-disk-number"), 0);
        int systemSize = Megabytes(context.Text("autounattend-disk-system-size"), 300);
        int recoverySize = Megabytes(context.Text("autounattend-disk-recovery-size"), 0);

        var creates = new List<XElement>();
        var modifies = new List<XElement>();
        int partition = 0;

        if (recoverySize > 0)
        {
            creates.Add(CreatePartition(creates.Count + 1, "Primary", recoverySize));
            modifies.Add(ModifyPartition(modifies.Count + 1, ++partition, "Recovery", "NTFS",
                typeId: gpt ? RecoveryTypeGpt : RecoveryTypeMbr));
        }

        if (gpt)
        {
            creates.Add(CreatePartition(creates.Count + 1, "EFI", systemSize));
            modifies.Add(ModifyPartition(modifies.Count + 1, ++partition, "System", "FAT32"));
            // The MSR is required on a GPT disk and cannot be formatted, labelled or lettered, so it takes a
            // partition number and no ModifyPartition entry.
            creates.Add(CreatePartition(creates.Count + 1, "MSR", MsrSizeMb));
            partition++;
        }
        else
        {
            creates.Add(CreatePartition(creates.Count + 1, "Primary", systemSize));
            modifies.Add(ModifyPartition(modifies.Count + 1, ++partition, "System", "NTFS", active: true));
        }

        creates.Add(CreatePartition(creates.Count + 1, "Primary", size: null));
        int windows = ++partition;
        modifies.Add(ModifyPartition(modifies.Count + 1, windows, "Windows", "NTFS", letter: "C"));

        var component = doc.Pass("windowsPE").Component("Microsoft-Windows-Setup");
        component.Child("DiskConfiguration").Add(new XElement(U + "Disk",
            new XAttribute(AutounattendDocument.Wcm + "action", "add"),
            new XElement(U + "DiskID", diskId),
            new XElement(U + "WillWipeDisk", "true"),
            new XElement(U + "CreatePartitions", creates),
            new XElement(U + "ModifyPartitions", modifies)));

        // WillShowUI stays the card's own value, written after Disk because the schema puts it last.
        AutounattendElementWriter.WriteState(doc, setting, chosen);

        component.Child("ImageInstall").Add(new XElement(U + "OSImage",
            new XElement(U + "InstallTo",
                new XElement(U + "DiskID", diskId),
                new XElement(U + "PartitionID", windows))));
    }

    // An omitted Size is what makes a partition take the rest of the disk; Extend and Size are exclusive.
    private static XElement CreatePartition(int order, string type, int? size) =>
        new(U + "CreatePartition",
            new XAttribute(AutounattendDocument.Wcm + "action", "add"),
            new XElement(U + "Order", order),
            new XElement(U + "Type", type),
            size is { } megabytes
                ? new XElement(U + "Size", megabytes)
                : new XElement(U + "Extend", "true"));

    private static XElement ModifyPartition(
        int order, int partitionId, string label, string format, string? letter = null, bool active = false, string? typeId = null)
    {
        var element = new XElement(U + "ModifyPartition",
            new XAttribute(AutounattendDocument.Wcm + "action", "add"),
            new XElement(U + "Order", order),
            new XElement(U + "PartitionID", partitionId),
            new XElement(U + "Label", label),
            new XElement(U + "Format", format));

        if (letter is not null)
            element.Add(new XElement(U + "Letter", letter));
        if (active)
            element.Add(new XElement(U + "Active", "true"));
        if (typeId is not null)
            element.Add(new XElement(U + "TypeID", typeId));

        return element;
    }

    // A box the rule rejected still reaches the writer, because a Winhance check reports and never blocks.
    private static int Megabytes(string? typed, int fallback) =>
        int.TryParse(typed, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : fallback;
}
