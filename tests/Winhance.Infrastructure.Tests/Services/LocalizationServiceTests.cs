using System.Globalization;
using System.Text;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

// SetLanguage sets the process-wide UI culture, so every fact puts it back and the culture readers never run alongside.
[Collection(ProcessCultureCollection.Name)]
public sealed class LocalizationServiceTests : IDisposable
{
    private static readonly string[] EnglishOnly = ["en.json"];

    private readonly Mock<IFileSystemService> _mockFileSystem = new();
    private readonly LocalizationService _sut;
    private readonly CultureInfo? _previousDefault = CultureInfo.DefaultThreadCurrentUICulture;
    private readonly CultureInfo _previousCurrent = CultureInfo.CurrentUICulture;

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

    public void Dispose()
    {
        CultureInfo.DefaultThreadCurrentUICulture = _previousDefault;
        CultureInfo.CurrentUICulture = _previousCurrent;
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
    public void SetLanguage_SetsTheDefaultThreadUICulture()
    {
        _sut.SetLanguage("de").Should().BeTrue();

        CultureInfo.DefaultThreadCurrentUICulture?.Name.Should().Be("de",
            because: "the region and language option lists read their display names on thread-pool threads, which start from this");
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

    [Fact]
    public void GetAvailableLanguages_ReadsTheLanguageFilesOnce()
    {
        var sut = ServiceWith(
            ("en", """{ "_Meta_LanguageDisplayName": "English" }"""),
            ("de", """{ "_Meta_LanguageDisplayName": "Deutsch (German)" }"""));

        var first = sut.GetAvailableLanguages();

        first.Select(l => l.DisplayName).Should().Equal("English", "Deutsch (German)");
        sut.GetAvailableLanguages().Should().BeSameAs(first);
    }

    [Fact]
    public void English_IsParsedOnceAcrossStartupAndTheLanguageList()
    {
        var mockFs = new Mock<IFileSystemService>();
        mockFs.Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] parts) => string.Join("\\", parts));
        mockFs.Setup(f => f.DirectoryExists(It.IsAny<string>()))
            .Returns(true);
        mockFs.Setup(f => f.GetFiles(It.IsAny<string>(), "*.json"))
            .Returns(EnglishOnly);
        mockFs.Setup(f => f.GetFileNameWithoutExtension(It.IsAny<string>()))
            .Returns((string path) => Path.GetFileNameWithoutExtension(path));
        mockFs.Setup(f => f.FileExists(It.Is<string>(s => s.EndsWith("en.json"))))
            .Returns(true);
        mockFs.Setup(f => f.ReadAllText(It.Is<string>(s => s.EndsWith("en.json"))))
            .Returns("{\"Greeting\": \"Hello\"}");

        var sut = new LocalizationService(mockFs.Object);
        sut.SetLanguage("en");
        sut.GetAvailableLanguages();

        sut.GetString("Greeting").Should().Be("Hello");
        mockFs.Verify(f => f.ReadAllText(It.Is<string>(s => s.EndsWith("en.json"))), Times.Once);
    }

    // Each language file is about 300 KB of JSON, so listing the languages must not parse the ones not shown.
    [Fact]
    public void LanguageList_ParsesNoLanguageButEnglishAndTheCurrentOne()
    {
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        var sut = ServiceWith(
            out var files,
            ("en", """{ "Greeting": "Hello", "_Meta_LanguageDisplayName": "English" }"""),
            ("de", """{ "Greeting": "Hallo", "_Meta_LanguageDisplayName": "Deutsch (German)" }"""),
            ("fr", "{ \"Long\": \"" + new string('x', 10_000) + "\", \"_Meta_LanguageDisplayName\": \"Fran\u00e7ais (French)\" }"),
            ("sv", """{ "Greeting": "Hej" }"""));
        sut.SetLanguage("de");

        sut.GetAvailableLanguages().Select(l => l.DisplayName).Should()
            .Equal("English", "Deutsch (German)", "Fran\u00e7ais (French)", CultureInfo.GetCultureInfo("sv").NativeName);

        files.Verify(f => f.ReadAllText(It.Is<string>(s => s.EndsWith("en.json"))), Times.Once);
        files.Verify(f => f.ReadAllText(It.Is<string>(s => s.EndsWith("de.json"))), Times.Once);
        files.Verify(f => f.ReadAllText(It.Is<string>(s => s.EndsWith("fr.json") || s.EndsWith("sv.json"))), Times.Never);
    }

    [Fact]
    public void KeepOnly_DropsOtherKeysFromEnglishAndTheCurrentLanguage()
    {
        var sut = ServiceWith(
            ("en", """{ "Tray_Open": "Open", "Other": "Other", "Profiles_X": "X" }"""),
            ("de", """{ "Tray_Open": "\u00d6ffnen", "Other": "Andere" }"""));
        sut.SetLanguage("de");

        sut.KeepOnly("Tray_", "Profiles_");

        sut.GetString("Tray_Open").Should().Be("\u00d6ffnen");
        sut.GetString("Profiles_X").Should().Be("X", "the English fallback keeps the kept keys");
        sut.TryGetString("Other", out _).Should().BeFalse();
    }

    [Fact]
    public void KeepOnly_AlsoFiltersALanguageLoadedLater()
    {
        var sut = ServiceWith(
            ("en", """{ "Tray_Open": "Open", "Other": "Other" }"""),
            ("de", """{ "Tray_Open": "\u00d6ffnen", "Other": "Andere" }"""));
        sut.SetLanguage("en");
        sut.KeepOnly("Tray_");

        sut.SetLanguage("de");

        sut.GetString("Tray_Open").Should().Be("\u00d6ffnen");
        sut.TryGetString("Other", out _).Should().BeFalse();
    }

    private static LocalizationService ServiceWith(params (string Lang, string Json)[] files) => ServiceWith(out _, files);

    private static LocalizationService ServiceWith(out Mock<IFileSystemService> fileSystem, params (string Lang, string Json)[] files)
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
            mockFs.Setup(f => f.OpenRead(It.Is<string>(s => s.EndsWith($"{lang}.json"))))
                .Returns(() => new MemoryStream(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(json)).ToArray()));
        }

        fileSystem = mockFs;
        return new LocalizationService(mockFs.Object);
    }
}
