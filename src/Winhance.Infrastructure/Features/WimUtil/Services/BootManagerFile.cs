using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

internal readonly record struct BootManagerFileInfo(bool SignedByCa2023, uint? SecurityVersion);

internal static class BootManagerFile
{
    // The signer's issuer, as text inside the Authenticode certificates. Microsoft's own KB5025885 check
    // reads the Secure Boot databases the same way: an ASCII search for the certificate's name.
    private static readonly byte[] Ca2023Name = Encoding.ASCII.GetBytes("Windows UEFI CA 2023");

    // RT_RCDATA. The boot manager compares this resource with the SVN stored in the firmware and refuses
    // to run when it is lower; Rufus reads the same resource to flag revoked media.
    private const int RcDataType = 10;
    private const string SecurityVersionResource = "BOOTMGRSECURITYVERSIONNUMBER";

    internal static BootManagerFileInfo Read(byte[] peImage)
    {
        using var reader = new PEReader(new MemoryStream(peImage), PEStreamOptions.PrefetchEntireImage);
        var image = reader.GetEntireImage().GetContent().AsSpan();
        var header = reader.PEHeaders.PEHeader
            ?? throw new BadImageFormatException("The boot manager has no optional header.");

        // The certificate table's address is a file offset, not an RVA.
        var certificates = header.CertificateTableDirectory;
        var signed = certificates.Size > 0
            && image.Slice(certificates.RelativeVirtualAddress, certificates.Size).IndexOf(Ca2023Name) >= 0;

        return new BootManagerFileInfo(signed, ReadSecurityVersion(reader, header.ResourceTableDirectory.RelativeVirtualAddress));
    }

    // Three levels, as the PE format lays them out: type, then name, then language.
    private static uint? ReadSecurityVersion(PEReader reader, int resourceRva)
    {
        if (resourceRva == 0)
            return null;

        var resources = reader.GetSectionData(resourceRva).GetContent().AsSpan();
        var names = FindEntry(resources, 0, RcDataType, null);
        var languages = names is { } n ? FindEntry(resources, n, null, SecurityVersionResource) : null;
        var data = languages is { } l ? FindEntry(resources, l, null, null) : null;
        if (data is not { } dataEntry)
            return null;

        var dataRva = BinaryPrimitives.ReadInt32LittleEndian(resources[dataEntry..]);
        return BinaryPrimitives.ReadUInt32LittleEndian(reader.GetSectionData(dataRva).GetContent(0, 4).AsSpan());
    }

    // Matches an entry by numeric id, by name, or (both null) takes the first. Returns the offset the entry
    // points at, inside the resource section.
    private static int? FindEntry(ReadOnlySpan<byte> resources, int directory, int? id, string? name)
    {
        var count = BinaryPrimitives.ReadUInt16LittleEndian(resources[(directory + 12)..])
            + BinaryPrimitives.ReadUInt16LittleEndian(resources[(directory + 14)..]);
        for (var i = 0; i < count; i++)
        {
            var at = directory + 16 + (8 * i);
            var key = BinaryPrimitives.ReadUInt32LittleEndian(resources[at..]);
            var target = BinaryPrimitives.ReadUInt32LittleEndian(resources[(at + 4)..]);
            var isNamed = (key & 0x80000000) != 0;
            var value = (int)(key & 0x7FFFFFFF);
            var matches = id is { } wantedId ? !isNamed && value == wantedId
                : name is null || (isNamed && ReadName(resources, value) == name);
            if (matches)
                return (int)(target & 0x7FFFFFFF);
        }

        return null;
    }

    private static string ReadName(ReadOnlySpan<byte> resources, int offset)
    {
        var length = BinaryPrimitives.ReadUInt16LittleEndian(resources[offset..]);
        return Encoding.Unicode.GetString(resources.Slice(offset + 2, length * 2));
    }
}
