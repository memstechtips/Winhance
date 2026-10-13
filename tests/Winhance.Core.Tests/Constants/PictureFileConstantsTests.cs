using FluentAssertions;
using Winhance.Core.Features.Common.Constants;
using Xunit;

namespace Winhance.Core.Tests.Constants;

public class PictureFileConstantsTests
{
    [Theory]
    [InlineData(@"C:\Pictures\beach.heic")]
    [InlineData(@"C:\Pictures\BEACH.JFIF")]
    [InlineData("slide.avif")]
    [InlineData("slide.jpg")]
    public void IsPicture_AcceptsTheTypesTheWindowsPickerOffers(string path) =>
        PictureFileConstants.IsPicture(path).Should().BeTrue();

    [Theory]
    [InlineData("notes.txt")]
    [InlineData("setup.exe")]
    [InlineData("wallpaper")]
    public void IsPicture_RejectsAnythingElse(string path) =>
        PictureFileConstants.IsPicture(path).Should().BeFalse();

    [Fact]
    public void FilePattern_OffersExactlyWhatIsPictureAccepts()
    {
        var patterns = PictureFileConstants.FilePattern.Split(';');

        patterns.Should().Contain("*.heic")
            .And.OnlyContain(p => p.StartsWith("*.", StringComparison.Ordinal) && PictureFileConstants.IsPicture(p));
    }
}
