using FluentAssertions;
using Winhance.Core.Features.Common.Helpers;
using Xunit;

namespace Winhance.Core.Tests.Helpers;

public class LocalPathTests
{
    [Theory]
    [InlineData(@"C:\Windows\Web\Wallpaper\img0.jpg")]
    [InlineData(@"D:\Albums\Trip")]
    public void IsLocal_DrivePath_IsTrue(string path)
        => LocalPath.IsLocal(path).Should().BeTrue();

    [Theory]
    [InlineData(@"\\evil.example\share\a.jpg")]
    [InlineData("//evil.example/share/a.jpg")]
    [InlineData(@"\\?\C:\a.jpg")]
    [InlineData(@"\\.\pipe\x")]
    public void IsLocal_DevicePath_IsFalse(string path)
        => LocalPath.IsLocal(path).Should().BeFalse();

    [Theory]
    [InlineData("a.jpg")]
    [InlineData(@"..\a.jpg")]
    [InlineData("")]
    public void IsLocal_NotFullyQualified_IsFalse(string path)
        => LocalPath.IsLocal(path).Should().BeFalse();
}
