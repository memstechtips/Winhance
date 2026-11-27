using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

// Most logic is delegated to the static Win32FileDialogHelper, which cannot be mocked; these cover the
// null-window checks and filter handling.
public class FilePickerServiceTests
{
    private static readonly string[] AllFilesFilter = ["All Files", "*.*"];
    private static readonly string[] XmlFilter = ["XML Files", "*.xml"];

    private readonly Mock<IMainWindowProvider> _mockMainWindowProvider = new();
    private readonly Mock<IDialogService> _dialogs = new();
    private readonly Mock<ILocalizationService> _loc = new();
    private readonly Mock<ILogService> _log = new();

    public FilePickerServiceTests()
    {
        _loc.Setup(l => l.GetString("Dialog_FileDialogUnavailable")).Returns("Cannot show file dialog.");
        _dialogs.Setup(d => d.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
    }

    private FilePickerService CreateSut()
    {
        return new FilePickerService(_mockMainWindowProvider.Object, _dialogs.Object, _loc.Object, _log.Object);
    }

    [Fact]
    public void Constructor_WithValidProvider_DoesNotThrow()
    {
        var act = () => CreateSut();

        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_StoresMainWindowProvider()
    {
        // Verifying the provider is stored by exercising a method that uses it
        var sut = CreateSut();

        _mockMainWindowProvider.Setup(p => p.MainWindow).Returns((Microsoft.UI.Xaml.Window?)null);
        var result = sut.PickFile(AllFilesFilter);

        result.Should().BeNull();
    }

    [Fact]
    public void PickFile_WhenMainWindowIsNull_ReturnsNull()
    {
        _mockMainWindowProvider.Setup(p => p.MainWindow).Returns((Microsoft.UI.Xaml.Window?)null);

        var sut = CreateSut();

        var result = sut.PickFile(XmlFilter);

        result.Should().BeNull();
    }

    [Fact]
    public void PickFile_WhenMainWindowIsNull_WithEmptyFilters_ReturnsNull()
    {
        _mockMainWindowProvider.Setup(p => p.MainWindow).Returns((Microsoft.UI.Xaml.Window?)null);

        var sut = CreateSut();

        var result = sut.PickFile(Array.Empty<string>());

        result.Should().BeNull();
    }

    [Fact]
    public void PickFile_WhenMainWindowIsNull_WithSuggestedFileName_ReturnsNull()
    {
        _mockMainWindowProvider.Setup(p => p.MainWindow).Returns((Microsoft.UI.Xaml.Window?)null);

        var sut = CreateSut();

        var result = sut.PickFile(AllFilesFilter, "test.xml");

        result.Should().BeNull();
    }

    [Fact]
    public void PickFolder_WhenMainWindowIsNull_ReturnsNull()
    {
        _mockMainWindowProvider.Setup(p => p.MainWindow).Returns((Microsoft.UI.Xaml.Window?)null);

        var sut = CreateSut();

        var result = sut.PickFolder();

        result.Should().BeNull();
    }

    [Fact]
    public void PickFolder_WhenMainWindowIsNull_WithTitle_ReturnsNull()
    {
        _mockMainWindowProvider.Setup(p => p.MainWindow).Returns((Microsoft.UI.Xaml.Window?)null);

        var sut = CreateSut();

        var result = sut.PickFolder("Select Output Folder");

        result.Should().BeNull();
    }

    [Fact]
    public void PickFolder_WhenMainWindowIsNull_WithNullTitle_ReturnsNull()
    {
        _mockMainWindowProvider.Setup(p => p.MainWindow).Returns((Microsoft.UI.Xaml.Window?)null);

        var sut = CreateSut();

        var result = sut.PickFolder(null);

        result.Should().BeNull();
    }

    [Fact]
    public void PickFile_AccessesMainWindowProperty()
    {
        _mockMainWindowProvider.Setup(p => p.MainWindow).Returns((Microsoft.UI.Xaml.Window?)null);

        var sut = CreateSut();
        sut.PickFile(AllFilesFilter);

        _mockMainWindowProvider.Verify(p => p.MainWindow, Times.Once);
    }

    [Fact]
    public void PickFolder_AccessesMainWindowProperty()
    {
        _mockMainWindowProvider.Setup(p => p.MainWindow).Returns((Microsoft.UI.Xaml.Window?)null);

        var sut = CreateSut();
        sut.PickFolder();

        _mockMainWindowProvider.Verify(p => p.MainWindow, Times.Once);
    }

    // The dialog needs an owner hwnd, so the only branch a unit test can reach is the one without a window.
    [Fact]
    public void PickSavePath_WithoutAMainWindow_LogsReportsAndReturnsNull()
    {
        _mockMainWindowProvider.Setup(p => p.MainWindow).Returns((Microsoft.UI.Xaml.Window?)null);

        var path = CreateSut().PickSavePath("title", "filter", "*.x", "default.x", "x");

        path.Should().BeNull();
        _log.Verify(l => l.Log(LogLevel.Error, It.Is<string>(m => m.Contains("no main window")), null, It.IsAny<string>()), Times.Once);
        _dialogs.Verify(d => d.ShowErrorAsync("Cannot show file dialog.", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }
}
