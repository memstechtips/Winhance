using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class LocalizationServiceTests
{
    private static readonly string[] EnglishOnly = ["en.json"];

    private readonly Mock<IFileSystemService> _mockFileSystem = new();
    private readonly LocalizationService _sut;

    public LocalizationServiceTests()
    {
        // Minimal file system mock so the constructor doesn't fail: no language files exist, so it falls back to empty dictionaries.
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

    [Fact]
    public void GetString_UnknownKey_ReturnsBracketedKey()
    {
        var result = _sut.GetString("NonExistentKey");

        result.Should().Be("[NonExistentKey]");
    }

    [Fact]
    public void GetString_WithFallbackAvailable_ReturnsFallbackValue()
    {
        var mockFs = new Mock<IFileSystemService>();
        mockFs.Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] parts) => string.Join("\\", parts));
        mockFs.Setup(f => f.DirectoryExists(It.IsAny<string>()))
            .Returns(true);
        mockFs.Setup(f => f.GetFiles(It.IsAny<string>(), "*.json"))
            .Returns(EnglishOnly);
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

    [Fact]
    public void GetString_WithFormatArgs_FormatsString()
    {
        var mockFs = new Mock<IFileSystemService>();
        mockFs.Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] parts) => string.Join("\\", parts));
        mockFs.Setup(f => f.DirectoryExists(It.IsAny<string>()))
            .Returns(true);
        mockFs.Setup(f => f.GetFiles(It.IsAny<string>(), "*.json"))
            .Returns(EnglishOnly);
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
            .Returns(EnglishOnly);
        mockFs.Setup(f => f.GetFileNameWithoutExtension(It.Is<string>(s => s.Contains("en"))))
            .Returns("en");
        mockFs.Setup(f => f.FileExists(It.Is<string>(s => s.Contains("en.json"))))
            .Returns(true);
        mockFs.Setup(f => f.ReadAllText(It.Is<string>(s => s.Contains("en.json"))))
            .Returns("{\"BadFormat\": \"Value is {0} and {1}\"}");

        var sut = new LocalizationService(mockFs.Object);

        // No args provided, so string.Format will throw, and GetString catches it
        var result = sut.GetString("BadFormat");

        result.Should().Be("Value is {0} and {1}");
    }

    [Fact]
    public void CurrentLanguage_DefaultsToResolvedLanguageCode()
    {
        _sut.CurrentLanguage.Should().NotBeNullOrEmpty();
    }

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

    [Fact]
    public void IsRightToLeft_ForEnglish_ReturnsFalse()
    {
        _sut.SetLanguage("en");

        _sut.IsRightToLeft.Should().BeFalse();
    }

    [Fact]
    public void GetString_WhenFileDoesNotExist_ReturnsBracketedKey()
    {
        var result = _sut.GetString("SomeKey");

        result.Should().Be("[SomeKey]");
    }

    // GetString is defined in terms of TryGetString, so these pin the contract the rest of
    // the app relies on - and the one LocalizationMock.MirrorTryGetString imitates. Without
    // them the mock's idea of a miss and the real service's can drift apart silently.

    [Fact]
    public void TryGetString_UnknownKey_ReturnsFalseAndEmptyValue()
    {
        var found = _sut.TryGetString("NonExistentKey", out var value);

        found.Should().BeFalse();
        value.Should().BeEmpty("callers use the out value unconditionally, so it must not be null");
    }

    [Fact]
    public void TryGetString_KeyInCurrentLanguage_ReturnsTrueAndValue()
    {
        var sut = ServiceWith(("en", "{\"Greeting\": \"Hello\"}"));

        sut.TryGetString("Greeting", out var value).Should().BeTrue();
        value.Should().Be("Hello");
    }

    [Fact]
    public void TryGetString_KeyOnlyInEnglish_FallsBackAndReportsFound()
    {
        // The English fallback tier lives inside TryGetString, so a key the active language
        // has not translated is a HIT - callers must not substitute their own fallback for it.
        var sut = ServiceWith(
            ("en", "{\"Greeting\": \"Hello\"}"),
            ("de", "{\"Untranslated\": \"nur hier\"}"));
        sut.SetLanguage("de");

        sut.TryGetString("Greeting", out var value).Should().BeTrue();
        value.Should().Be("Hello");
    }

    [Fact]
    public void TryGetString_TranslationLooksLikeTheMissMarker_IsStillReportedAsFound()
    {
        // The bug this method exists to fix: a translation is allowed to be bracketed, and
        // the "[key]" marker cannot tell it apart from a miss.
        var sut = ServiceWith(
            ("en", "{\"SettingGroup_Other\": \"Other\"}"),
            ("de", "{\"SettingGroup_Other\": \"[Sonstige]\"}"));
        sut.SetLanguage("de");

        sut.TryGetString("SettingGroup_Other", out var value).Should().BeTrue();
        value.Should().Be("[Sonstige]");
    }

    [Fact]
    public void TryGetString_EmptyTranslation_FallsThroughToEnglish()
    {
        // An empty string in a locale file is an untranslated placeholder, not a value.
        var sut = ServiceWith(
            ("en", "{\"Greeting\": \"Hello\"}"),
            ("de", "{\"Greeting\": \"\"}"));
        sut.SetLanguage("de");

        sut.TryGetString("Greeting", out var value).Should().BeTrue();
        value.Should().Be("Hello");
    }

    [Fact]
    public void GetString_BracketedTranslation_IsReturnedRatherThanTreatedAsAMiss()
    {
        // Guards the "defined in terms of TryGetString" wiring: the two must agree about what a miss is.
        var sut = ServiceWith(
            ("en", "{\"SettingGroup_Other\": \"Other\"}"),
            ("de", "{\"SettingGroup_Other\": \"[Sonstige]\"}"));
        sut.SetLanguage("de");

        sut.GetString("SettingGroup_Other").Should().Be("[Sonstige]");
    }

    private static LocalizationService ServiceWith(params (string Lang, string Json)[] files)
    {
        var mockFs = new Mock<IFileSystemService>();
        mockFs.Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] parts) => string.Join("\\", parts));
        mockFs.Setup(f => f.DirectoryExists(It.IsAny<string>()))
            .Returns(true);
        mockFs.Setup(f => f.GetFiles(It.IsAny<string>(), "*.json"))
            .Returns(files.Select(file => $"{file.Lang}.json").ToArray());
        mockFs.Setup(f => f.GetFileNameWithoutExtension(It.IsAny<string>()))
            .Returns((string path) => Path.GetFileNameWithoutExtension(path));

        foreach (var (lang, json) in files)
        {
            mockFs.Setup(f => f.FileExists(It.Is<string>(s => s.EndsWith($"{lang}.json"))))
                .Returns(true);
            mockFs.Setup(f => f.ReadAllText(It.Is<string>(s => s.EndsWith($"{lang}.json"))))
                .Returns(json);
        }

        return new LocalizationService(mockFs.Object);
    }
}
