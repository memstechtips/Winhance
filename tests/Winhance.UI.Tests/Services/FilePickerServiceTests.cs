using FluentAssertions;
using Moq;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

/// <summary>
/// Tests for FilePickerService. Note: Most logic is delegated to the static
/// Win32FileDialogHelper class which cannot be mocked. These tests cover the
/// constructor, null window checks, and filter parameter handling.
/// </summary>
public class FilePickerServiceTests
{
    private readonly Mock<IMainWindowProvider> _mockMainWindowProvider = new();

    private FilePickerService CreateSut()
    {
        return new FilePickerService(_mockMainWindowProvider.Object);
    }

    // -------------------------------------------------------
    // Constructor
    // -------------------------------------------------------

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

        // The provider should be used - calling PickFile with null window returns null
        _mockMainWindowProvider.Setup(p => p.MainWindow).Returns((Microsoft.UI.Xaml.Window?)null);
        var result = sut.PickFile(new[] { "All Files", "*.*" });

        result.Should().BeNull();
    }

    // -------------------------------------------------------
    // PickFile - null window handling
    // -------------------------------------------------------

    [Fact]
    public void PickFile_WhenMainWindowIsNull_ReturnsNull()
    {
        _mockMainWindowProvider.Setup(p => p.MainWindow).Returns((Microsoft.UI.Xaml.Window?)null);

        var sut = CreateSut();

        var result = sut.PickFile(new[] { "XML Files", "*.xml" });

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

        var result = sut.PickFile(new[] { "All Files", "*.*" }, "test.xml");

        result.Should().BeNull();
    }

    // -------------------------------------------------------
    // PickFolder - null window handling
    // -------------------------------------------------------

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

    // -------------------------------------------------------
    // PickSaveFile - null window handling
    // -------------------------------------------------------

    [Fact]
    public void PickSaveFile_WhenMainWindowIsNull_ReturnsNull()
    {
        _mockMainWindowProvider.Setup(p => p.MainWindow).Returns((Microsoft.UI.Xaml.Window?)null);

        var sut = CreateSut();

        var result = sut.PickSaveFile(new[] { "XML Files", "*.xml" });

        result.Should().BeNull();
    }

    [Fact]
    public void PickSaveFile_WhenMainWindowIsNull_WithAllParameters_ReturnsNull()
    {
        _mockMainWindowProvider.Setup(p => p.MainWindow).Returns((Microsoft.UI.Xaml.Window?)null);

        var sut = CreateSut();

        var result = sut.PickSaveFile(
            new[] { "ISO Files", "*.iso" },
            suggestedFileName: "output.iso",
            defaultExtension: "iso");

        result.Should().BeNull();
    }

    [Fact]
    public void PickSaveFile_WhenMainWindowIsNull_WithEmptyFilters_ReturnsNull()
    {
        _mockMainWindowProvider.Setup(p => p.MainWindow).Returns((Microsoft.UI.Xaml.Window?)null);

        var sut = CreateSut();

        var result = sut.PickSaveFile(Array.Empty<string>());

        result.Should().BeNull();
    }

    [Fact]
    public void PickSaveFile_WhenMainWindowIsNull_WithNullOptionalParams_ReturnsNull()
    {
        _mockMainWindowProvider.Setup(p => p.MainWindow).Returns((Microsoft.UI.Xaml.Window?)null);

        var sut = CreateSut();

        var result = sut.PickSaveFile(
            new[] { "All Files", "*.*" },
            suggestedFileName: null,
            defaultExtension: null);

        result.Should().BeNull();
    }

    // -------------------------------------------------------
    // MainWindowProvider interaction
    // -------------------------------------------------------

    [Fact]
    public void PickFile_AccessesMainWindowProperty()
    {
        _mockMainWindowProvider.Setup(p => p.MainWindow).Returns((Microsoft.UI.Xaml.Window?)null);

        var sut = CreateSut();
        sut.PickFile(new[] { "All Files", "*.*" });

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

    [Fact]
    public void PickSaveFile_AccessesMainWindowProperty()
    {
        _mockMainWindowProvider.Setup(p => p.MainWindow).Returns((Microsoft.UI.Xaml.Window?)null);

        var sut = CreateSut();
        sut.PickSaveFile(new[] { "All Files", "*.*" });

        _mockMainWindowProvider.Verify(p => p.MainWindow, Times.Once);
    }
}
