using FluentAssertions;
using Winhance.Infrastructure.Features.WimUtil.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.WimUtil;

public class DismImageInfoReaderTests
{
    [Theory]
    [InlineData(0u, "x86")]
    [InlineData(9u, "x64")]
    [InlineData(12u, "arm64")]
    [InlineData(5u, "")]
    [InlineData(6u, "")]
    [InlineData(99u, "")]
    public void ArchitectureName_MapsDismProcessorArchitecture(uint dism, string expected)
    {
        DismImageInfoReader.ArchitectureName(dism).Should().Be(expected);
    }
}
