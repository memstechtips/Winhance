using System.Buffers.Binary;
using System.Text;

namespace Winhance.Infrastructure.Tests.WimUtil.WimServices;

// A minimal PE32+ with what BootManagerFile reads: an RT_RCDATA resource named
// BOOTMGRSECURITYVERSIONNUMBER and a certificate table holding the signer's name.
internal static class FakeBootManager
{
    private const int PeHeader = 0x80;
    private const int OptionalHeader = PeHeader + 24;
    private const int SectionTable = OptionalHeader + 240;
    private const int ResourceFileOffset = 0x200;
    private const int ResourceRva = 0x1000;
    private const int CertificateFileOffset = 0x400;

    internal static byte[] Build(string signer, uint? securityVersion)
    {
        var certificate = Encoding.ASCII.GetBytes($"CN={signer}, O=Microsoft Corporation");
        var image = new byte[CertificateFileOffset + 8 + certificate.Length];
        var span = image.AsSpan();

        span[0] = (byte)'M';
        span[1] = (byte)'Z';
        BinaryPrimitives.WriteInt32LittleEndian(span[0x3C..], PeHeader);
        Encoding.ASCII.GetBytes("PE\0\0").CopyTo(span[PeHeader..]);
        BinaryPrimitives.WriteUInt16LittleEndian(span[(PeHeader + 4)..], 0x8664);
        BinaryPrimitives.WriteUInt16LittleEndian(span[(PeHeader + 6)..], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(span[(PeHeader + 20)..], 240);
        BinaryPrimitives.WriteUInt16LittleEndian(span[(PeHeader + 22)..], 0x0022);

        BinaryPrimitives.WriteUInt16LittleEndian(span[OptionalHeader..], 0x20B);
        BinaryPrimitives.WriteUInt64LittleEndian(span[(OptionalHeader + 24)..], 0x10000000);
        BinaryPrimitives.WriteInt32LittleEndian(span[(OptionalHeader + 32)..], 0x1000);
        BinaryPrimitives.WriteInt32LittleEndian(span[(OptionalHeader + 36)..], 0x200);
        BinaryPrimitives.WriteInt32LittleEndian(span[(OptionalHeader + 56)..], 0x2000);
        BinaryPrimitives.WriteInt32LittleEndian(span[(OptionalHeader + 60)..], 0x200);
        BinaryPrimitives.WriteUInt16LittleEndian(span[(OptionalHeader + 68)..], 10);
        BinaryPrimitives.WriteInt32LittleEndian(span[(OptionalHeader + 108)..], 16);
        BinaryPrimitives.WriteInt32LittleEndian(span[(OptionalHeader + 128)..], ResourceRva);
        BinaryPrimitives.WriteInt32LittleEndian(span[(OptionalHeader + 132)..], 0x100);
        BinaryPrimitives.WriteInt32LittleEndian(span[(OptionalHeader + 144)..], CertificateFileOffset);
        BinaryPrimitives.WriteInt32LittleEndian(span[(OptionalHeader + 148)..], 8 + certificate.Length);

        Encoding.ASCII.GetBytes(".rsrc").CopyTo(span[SectionTable..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[(SectionTable + 8)..], 0x200);
        BinaryPrimitives.WriteInt32LittleEndian(span[(SectionTable + 12)..], ResourceRva);
        BinaryPrimitives.WriteInt32LittleEndian(span[(SectionTable + 16)..], 0x200);
        BinaryPrimitives.WriteInt32LittleEndian(span[(SectionTable + 20)..], ResourceFileOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(span[(SectionTable + 36)..], 0x40000040);

        if (securityVersion is { } version)
        {
            WriteResources(span[ResourceFileOffset..], version);
        }

        BinaryPrimitives.WriteInt32LittleEndian(span[CertificateFileOffset..], 8 + certificate.Length);
        certificate.CopyTo(span[(CertificateFileOffset + 8)..]);
        return image;
    }

    // Type 10 -> name -> language 1033 -> a four-byte value.
    private static void WriteResources(Span<byte> section, uint version)
    {
        const int nameDirectory = 0x18, languageDirectory = 0x30, dataEntry = 0x48, value = 0x58, name = 0x5C;

        BinaryPrimitives.WriteUInt16LittleEndian(section[14..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(section[16..], 10);
        BinaryPrimitives.WriteUInt32LittleEndian(section[20..], 0x80000000 | nameDirectory);

        BinaryPrimitives.WriteUInt16LittleEndian(section[(nameDirectory + 12)..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(section[(nameDirectory + 16)..], 0x80000000 | name);
        BinaryPrimitives.WriteUInt32LittleEndian(section[(nameDirectory + 20)..], 0x80000000 | languageDirectory);

        BinaryPrimitives.WriteUInt16LittleEndian(section[(languageDirectory + 14)..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(section[(languageDirectory + 16)..], 1033);
        BinaryPrimitives.WriteUInt32LittleEndian(section[(languageDirectory + 20)..], dataEntry);

        BinaryPrimitives.WriteInt32LittleEndian(section[dataEntry..], ResourceRva + value);
        BinaryPrimitives.WriteInt32LittleEndian(section[(dataEntry + 4)..], 4);
        BinaryPrimitives.WriteUInt32LittleEndian(section[value..], version);

        const string resourceName = "BOOTMGRSECURITYVERSIONNUMBER";
        BinaryPrimitives.WriteUInt16LittleEndian(section[name..], (ushort)resourceName.Length);
        Encoding.Unicode.GetBytes(resourceName).CopyTo(section[(name + 2)..]);
    }
}
