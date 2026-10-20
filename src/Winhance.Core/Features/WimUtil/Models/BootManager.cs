namespace Winhance.Core.Features.WimUtil.Models;

// The certificate behind the UEFI boot manager the media starts from. Microsoft is revoking "Windows
// Production PCA 2011" (CVE-2023-24932, KB5025885): a PC that has added it to its DBX refuses 2011-signed
// media, and a PC without "Windows UEFI CA 2023" in its DB refuses 2023-signed media. With Secure Boot off,
// and on legacy BIOS, both start.
public enum BootManager
{
    Pca2011,
    Ca2023,
}

// Files the writers put on the media instead of the working folder's copies, keyed by the path relative to
// the media root. The working folder is never changed, so choosing 2011 again needs nothing restored, and
// nothing could restore it: boot.wim's own fonts and efisys.bin differ from the ones the ISO ships.
public sealed record BootFileReplacements(IReadOnlyDictionary<string, string> Files)
{
    public static BootFileReplacements None { get; } = new(new Dictionary<string, string>());
}

// Offers2023 is false when boot.wim carries no 2023 boot manager, or when the media already starts from one.
public sealed record BootManagerOptions(bool Offers2023, BootManager Default, bool IsOutdated);
