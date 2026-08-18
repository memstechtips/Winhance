using FluentAssertions;
using Moq;
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
    private static readonly string[] IsoFilter = ["ISO Files", "*.iso"];

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
        var result = sut.PickFile(AllFilesFilter);

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

        var result = sut.PickSaveFile(XmlFilter);

        result.Should().BeNull();
    }

    [Fact]
    public void PickSaveFile_WhenMainWindowIsNull_WithAllParameters_ReturnsNull()
    {
        _mockMainWindowProvider.Setup(p => p.MainWindow).Returns((Microsoft.UI.Xaml.Window?)null);

        var sut = CreateSut();

        var result = sut.PickSaveFile(
            IsoFilter,
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
            AllFilesFilter,
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

    [Fact]
    public void PickSaveFile_AccessesMainWindowProperty()
    {
        _mockMainWindowProvider.Setup(p => p.MainWindow).Returns((Microsoft.UI.Xaml.Window?)null);

        var sut = CreateSut();
        sut.PickSaveFile(AllFilesFilter);

        _mockMainWindowProvider.Verify(p => p.MainWindow, Times.Once);
    }
}
