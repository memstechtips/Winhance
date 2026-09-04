using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class SaveFilePickerTests
{
    private readonly Mock<IMainWindowProvider> _mainWindow = new();
    private readonly Mock<ILogService> _log = new();
    private readonly Mock<IDialogService> _dialogs = new();
    private readonly Mock<ILocalizationService> _loc = new();

    // The dialog needs an owner hwnd, so the only branch a unit test can reach is the one without a window.
    [Fact]
    public void PickSavePath_WithoutAMainWindow_LogsReportsAndReturnsNull()
    {
        _mainWindow.Setup(m => m.MainWindow).Returns((Microsoft.UI.Xaml.Window?)null);
        _loc.Setup(l => l.GetString("Dialog_FileDialogUnavailable")).Returns("Cannot show file dialog.");
        _dialogs.Setup(d => d.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        var sut = new SaveFilePicker(_mainWindow.Object, _log.Object, _dialogs.Object, _loc.Object);

        var path = sut.PickSavePath("title", "filter", "*.x", "default.x", "x");

        path.Should().BeNull();
        _log.Verify(l => l.Log(LogLevel.Error, It.Is<string>(m => m.Contains("no main window")), null, It.IsAny<string>()), Times.Once);
        _dialogs.Verify(d => d.ShowErrorAsync("Cannot show file dialog.", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }
}
