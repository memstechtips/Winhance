using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Customize.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class ThemeModeApplierTests
{
    private const string DarkPicture = @"C:\Windows\Web\Wallpaper\Windows\img19.jpg";

    private readonly Mock<IStateWriter> _stateWriter = new();
    private readonly Mock<ILogService> _log = new();
    private readonly ThemeModeApplier _sut;

    public ThemeModeApplierTests()
    {
        _stateWriter
            .Setup(w => w.WriteRegistry(It.IsAny<RegTarget>(), It.IsAny<string>(), It.IsAny<object>()))
            .Returns(true);
        _sut = new ThemeModeApplier(_stateWriter.Object, _log.Object);
    }

    [Fact]
    public async Task TryApply_LeavesTheDesktopAlone()
    {
        await _sut.TryApplySpecialSettingAsync("theme-mode-windows", 1, additionalContext: true);

        _stateWriter.Verify(w => w.WriteRegistry(It.IsAny<RegTarget>(), It.IsAny<string>(), DarkPicture), Times.Never);
        _stateWriter.Verify(w => w.DeleteRegistry(It.IsAny<RegTarget>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task TryApply_NonThemeSettingId_ReturnsFalse()
    {

        var result = await _sut.TryApplySpecialSettingAsync("not-theme", 0);

        result.Should().BeFalse();
        _stateWriter.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TryApply_NonIntValue_ReturnsFalse()
    {

        var result = await _sut.TryApplySpecialSettingAsync("theme-mode-windows", "dark");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryApply_DarkMode_WritesZeroToBothThemeKeys()
    {
        // The handler applies the catalog theme-mode-windows "Dark Mode" state: both
        // AppsUseLightTheme + SystemUsesLightTheme are written 0 via the state writer.

        await _sut.TryApplySpecialSettingAsync("theme-mode-windows", 1);

        _stateWriter.Verify(w => w.WriteRegistry(It.IsAny<RegTarget>(), It.IsAny<string>(),
            It.Is<object>(v => v.Equals(0))), Times.Exactly(2));
    }

    [Fact]
    public async Task TryApply_LightMode_WritesOneToBothThemeKeys()
    {

        await _sut.TryApplySpecialSettingAsync("theme-mode-windows", 0);

        _stateWriter.Verify(w => w.WriteRegistry(It.IsAny<RegTarget>(), It.IsAny<string>(),
            It.Is<object>(v => v.Equals(1))), Times.Exactly(2));
    }

    [Fact]
    public async Task TryApply_DetectOnlyStateIndex_WritesNothing()
    {
        // Index 2 is the neutral "Mixed" state: detect-only, no Set. The relationship reverse-sync hands
        // this handler exactly that index when the two theme children disagree.
        // Falling through to Light Mode here would clobber the child the user had just changed.
        // It is still HANDLED (true), it just writes nothing.

        var result = await _sut.TryApplySpecialSettingAsync("theme-mode-windows", 2);

        result.Should().BeTrue();
        _stateWriter.Verify(w => w.WriteRegistry(It.IsAny<RegTarget>(), It.IsAny<string>(), It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task TryApply_OutOfRangeIndex_WritesNothing()
    {
        var result = await _sut.TryApplySpecialSettingAsync("theme-mode-windows", 99);

        result.Should().BeTrue();
        _stateWriter.Verify(w => w.WriteRegistry(It.IsAny<RegTarget>(), It.IsAny<string>(), It.IsAny<object>()), Times.Never);
    }
}
