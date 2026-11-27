namespace Winhance.Infrastructure.Features.WimUtil.Services;

internal readonly record struct UsbWriteLayout(long BootBytes, long InstallBytes);

// Two partitions on one MBR disk, the only partition table that boots both UEFI and legacy BIOS. UEFI firmware
// reads FAT32 alone, so the active FAT32 partition carries what the PC starts from, up to Setup's first
// screen. Setup then finds install.wim on the NTFS partition by itself, whole and at any size. Verified on
// Gigabyte boards with Secure Boot on, off, and legacy CSM.
internal static class UsbWriteLayoutPlanner
{
    // An untouched 25H2 boot set is 635 MB; the rest is headroom for a customised boot.wim.
    internal const long BootPartitionBytes = 2L * 1024 * 1024 * 1024;

    internal static UsbWriteLayout Plan(IReadOnlyDictionary<string, long> sizeByRelativePath)
    {
        long boot = 0, install = 0;
        foreach (var (path, size) in sizeByRelativePath)
        {
            if (IsOnBootPartition(path))
                boot += size;

            if (!IsLeftOffInstallPartition(path))
                install += size;
        }

        return new UsbWriteLayout(boot, install);
    }

    // Takes folders as well as files, so the copy never creates an empty sources\$OEM$ or support\ on the
    // boot partition. autounattend.xml sits on both roots: Setup searches the root of every removable drive.
    // $WinpeDriver$ stays on the install partition: KB 2686316 has Setup probe the root of every drive letter.
    internal static bool IsOnBootPartition(string relativePath) =>
        relativePath.Equals("bootmgr", StringComparison.OrdinalIgnoreCase)
        || relativePath.Equals("bootmgr.efi", StringComparison.OrdinalIgnoreCase)
        || relativePath.Equals("bootmgfw.efi", StringComparison.OrdinalIgnoreCase)
        || relativePath.Equals("autounattend.xml", StringComparison.OrdinalIgnoreCase)
        || relativePath.Equals("sources", StringComparison.OrdinalIgnoreCase)
        || relativePath.Equals(@"sources\boot.wim", StringComparison.OrdinalIgnoreCase)
        || IsIn(relativePath, "boot")
        || IsIn(relativePath, "efi");

    // efi\boot is the fallback path firmware boots from. Gigabyte's firmware reads NTFS too, and a copy there
    // adds a second "UEFI:" entry to the boot menu. The rest of efi\ and boot\ stays: setup.exe started from
    // inside Windows sizes Boot, Efi and Sources beside it and fails with 0x80070003 when one is missing.
    internal static bool IsLeftOffInstallPartition(string relativePath) => IsIn(relativePath, @"efi\boot");

    private static bool IsIn(string relativePath, string folder) =>
        relativePath.Equals(folder, StringComparison.OrdinalIgnoreCase)
        || relativePath.StartsWith(folder + @"\", StringComparison.OrdinalIgnoreCase);
}
