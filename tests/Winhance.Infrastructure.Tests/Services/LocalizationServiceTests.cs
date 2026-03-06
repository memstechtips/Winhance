using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class LocalizationServiceTests
{
    private readonly Mock<IFileSystemService> _mockFileSystem = new();
    private readonly LocalizationService _sut;

    public LocalizationServiceTests()
    {
        // Set up a minimal file system mock so the constructor doesn't fail:
        // - CombinePath returns concatenated path
        // - No language files exist, so it falls back to empty dictionaries
        _mockFileSystem
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] parts) => string.Join("\\", parts));
        _mockFileSystem
            .Setup(f => f.DirectoryExists(It.IsAny<string>()))
            .Returns(false);
        _mockFileSystem
            .Setup(f => f.FileExists(It.IsAny<string>()))
            .Returns(false);

        _sut = new LocalizationService(_mockFileSystem.Object);
    }

    // ── GetString(key) ──

    [Fact]
    public void GetString_UnknownKey_ReturnsBracketedKey()
    {
        var result = _sut.GetString("NonExistentKey");

        result.Should().Be("[NonExistentKey]");
    }

    [Fact]
    public void GetString_WithFallbackAvailable_ReturnsFallbackValue()
    {
        // Create a service with a mock that provides an English fallback file
        var mockFs = new Mock<IFileSystemService>();
        mockFs.Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] parts) => string.Join("\\", parts));
        mockFs.Setup(f => f.DirectoryExists(It.IsAny<string>()))
            .Returns(true);
        mockFs.Setup(f => f.GetFiles(It.IsAny<string>(), "*.json"))
            .Returns(new[] { "en.json" });
        mockFs.Setup(f => f.GetFileNameWithoutExtension(It.Is<string>(s => s.Contains("en"))))
            .Returns("en");
        mockFs.Setup(f => f.FileExists(It.Is<string>(s => s.Contains("en.json"))))
            .Returns(true);
        mockFs.Setup(f => f.ReadAllText(It.Is<string>(s => s.Contains("en.json"))))
            .Returns("{\"Greeting\": \"Hello\"}");

        var sut = new LocalizationService(mockFs.Object);

        var result = sut.GetString("Greeting");

        result.Should().Be("Hello");
    }

    // ── GetString(key, args) ──

    [Fact]
    public void GetString_WithFormatArgs_FormatsString()
    {
        var mockFs = new Mock<IFileSystemService>();
        mockFs.Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] parts) => string.Join("\\", parts));
        mockFs.Setup(f => f.DirectoryExists(It.IsAny<string>()))
            .Returns(true);
        mockFs.Setup(f => f.GetFiles(It.IsAny<string>(), "*.json"))
            .Returns(new[] { "en.json" });
        mockFs.Setup(f => f.GetFileNameWithoutExtension(It.Is<string>(s => s.Contains("en"))))
            .Returns("en");
        mockFs.Setup(f => f.FileExists(It.Is<string>(s => s.Contains("en.json"))))
            .Returns(true);
        mockFs.Setup(f => f.ReadAllText(It.Is<string>(s => s.Contains("en.json"))))
            .Returns("{\"Welcome\": \"Hello, {0}!\"}");

        var sut = new LocalizationService(mockFs.Object);

        var result = sut.GetString("Welcome", "World");

        result.Should().Be("Hello, World!");
    }

    [Fact]
    public void GetString_WithBadFormat_ReturnsFormatStringUnformatted()
    {
        var mockFs = new Mock<IFileSystemService>();
        mockFs.Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] parts) => string.Join("\\", parts));
        mockFs.Setup(f => f.DirectoryExists(It.IsAny<string>()))
            .Returns(true);
        mockFs.Setup(f => f.GetFiles(It.IsAny<string>(), "*.json"))
            .Returns(new[] { "en.json" });
        mockFs.Setup(f => f.GetFileNameWithoutExtension(It.Is<string>(s => s.Contains("en"))))
            .Returns("en");
        mockFs.Setup(f => f.FileExists(It.Is<string>(s => s.Contains("en.json"))))
            .Returns(true);
        // Format string expects 2 args but we'll provide 0
        mockFs.Setup(f => f.ReadAllText(It.Is<string>(s => s.Contains("en.json"))))
            .Returns("{\"BadFormat\": \"Value is {0} and {1}\"}");

        var sut = new LocalizationService(mockFs.Object);

        // No args provided, so string.Format will throw, and GetString catches it
        var result = sut.GetString("BadFormat");

        result.Should().Be("Value is {0} and {1}");
    }

    // ── CurrentLanguage ──

    [Fact]
    public void CurrentLanguage_DefaultsToResolvedLanguageCode()
    {
        // When no language files exist, it falls back to "en"
        _sut.CurrentLanguage.Should().NotBeNullOrEmpty();
    }

    // ── SetLanguage ──

    [Fact]
    public void SetLanguage_ValidLanguageCode_ReturnsTrue()
    {
        var result = _sut.SetLanguage("en");

        result.Should().BeTrue();
        _sut.CurrentLanguage.Should().Be("en");
    }

    [Fact]
    public void SetLanguage_EmptyLanguageCode_ReturnsFalseOrHandlesGracefully()
    {
        // Empty string may throw CultureNotFoundException depending on .NET version
        // The method should either return false (catch) or succeed with invariant culture
        var action = () => _sut.SetLanguage("");

        // Either way, it should not throw to the caller
        action.Should().NotThrow();
    }

    [Fact]
    public void SetLanguage_RaisesLanguageChangedEvent()
    {
        bool eventRaised = false;
        _sut.LanguageChanged += (_, _) => eventRaised = true;

        _sut.SetLanguage("en");

        eventRaised.Should().BeTrue();
    }

    // ── IsRightToLeft ──

    [Fact]
    public void IsRightToLeft_ForEnglish_ReturnsFalse()
    {
        _sut.SetLanguage("en");

        _sut.IsRightToLeft.Should().BeFalse();
    }

    // ── Localization file loading edge cases ──

    [Fact]
    public void GetString_WhenFileDoesNotExist_ReturnsBracketedKey()
    {
        // Default mock setup: FileExists returns false for everything
        var result = _sut.GetString("SomeKey");

        result.Should().Be("[SomeKey]");
    }
}
