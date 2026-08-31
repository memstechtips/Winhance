using System.Runtime.InteropServices;
using FluentAssertions;
using Winhance.Core.Features.Common.Native;
using Xunit;

namespace Winhance.Core.Tests.Style;

// DISM is not in the Win32 metadata (it ships with the ADK, not the SDK - microsoft/win32metadata#1289),
// so CsWin32 cannot generate these and nothing checks the layout at compile time. No P/Invoke style
// would: [LibraryImport] generates marshalling for the SIGNATURE, never for a struct's packing.
//
// That matters because MarshalArray strides by Marshal.SizeOf. A wrong size does not throw - it reads
// every element after the first from a shifted offset, hands PtrToStringUni a garbage pointer, and
// takes the process down with no managed exception and nothing in the log. DISM_IMAGE_INFO shipped
// without Pack = 4 from the day it was declared until the first caller found it that way on
// 2026-08-26; a single-edition image would have read fine, and a real Windows 11 install.wim has ten.
//
// This file is the substitute for the compile-time check we cannot have.
public class DismStructLayoutTests
{
    // The rule, not its consequences: dismapi.h packs to 4, so every struct in it does. Stated by
    // reflection so a struct added later is covered without anyone remembering to add a size here.
    [Fact]
    public void EveryDismStruct_DeclaresSequentialPack4()
    {
        var dismStructs = typeof(DismApi).GetNestedTypes()
            .Where(type => type.IsValueType && !type.IsEnum)
            .ToArray();

        dismStructs.Should().HaveCountGreaterThan(2,
            "DismApi declares DISM_CAPABILITY, DISM_FEATURE and DISM_IMAGE_INFO - finding fewer means "
            + "this scan stopped matching, not that the structs went away");

        foreach (var dismStruct in dismStructs)
        {
            var layout = dismStruct.StructLayoutAttribute!;

            layout.Value.Should().Be(LayoutKind.Sequential,
                $"{dismStruct.Name} mirrors a native struct field for field");

            layout.Pack.Should().Be(4,
                $"{dismStruct.Name} must declare [StructLayout(LayoutKind.Sequential, Pack = 4)] - "
                + "without it the struct pads to natural alignment, Marshal.SizeOf overshoots the "
                + "native size, and every array element after the first is read at the wrong offset");
        }
    }

    // Independent of the rule above: packing cannot catch a field added, dropped or reordered against
    // the native declaration, and these sizes can.
    [Theory]
    [InlineData(typeof(DismApi.DISM_CAPABILITY), 12)]
    [InlineData(typeof(DismApi.DISM_FEATURE), 12)]
    [InlineData(typeof(DismApi.DISM_IMAGE_INFO), 140)]
    public void DismStruct_Marshalled_MatchesTheNativeSize(Type dismStruct, int expectedBytes)
    {
        Marshal.SizeOf(dismStruct).Should().Be(expectedBytes);
    }

    [Fact]
    public void DismImageInfo_PointerFields_SitWhereTheNativeStructPutsThem()
    {
        // ImageName is the trap: it sits at 8 packed or unpacked, so the one field Winhance reads is
        // correct in element 0 either way. That is precisely why the bug stayed invisible.
        Marshal.OffsetOf<DismApi.DISM_IMAGE_INFO>(nameof(DismApi.DISM_IMAGE_INFO.ImageName))
            .Should().Be(8);
        Marshal.OffsetOf<DismApi.DISM_IMAGE_INFO>(nameof(DismApi.DISM_IMAGE_INFO.ProductName))
            .Should().Be(36);
        Marshal.OffsetOf<DismApi.DISM_IMAGE_INFO>(nameof(DismApi.DISM_IMAGE_INFO.SystemRoot))
            .Should().Be(108);
    }
}
