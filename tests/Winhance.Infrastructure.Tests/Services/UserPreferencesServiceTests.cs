using System.Text.Json;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class UserPreferencesServiceTests
{
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IInteractiveUserService> _mockInteractiveUserService = new();
    private readonly Mock<IFileSystemService> _mockFileSystemService = new();
    private readonly UserPreferencesService _service;

    private const string LocalAppDataPath = @"C:\Users\TestUser\AppData\Local";

    public UserPreferencesServiceTests()
    {
        _mockInteractiveUserService
            .Setup(s => s.GetInteractiveUserFolderPath(Environment.SpecialFolder.LocalApplicationData))
            .Returns(LocalAppDataPath);

        _mockFileSystemService
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] parts) => string.Join(@"\", parts));

        _mockFileSystemService
            .Setup(f => f.DirectoryExists(It.IsAny<string>()))
            .Returns(true);

        _mockFileSystemService
            .Setup(f => f.GetDirectoryName(It.IsAny<string>()))
            .Returns((string path) =>
            {
                int lastSep = path.LastIndexOf('\\');
                return lastSep > 0 ? path.Substring(0, lastSep) : null;
            });

        _service = new UserPreferencesService(
            _mockLogService.Object,
            _mockInteractiveUserService.Object,
            _mockFileSystemService.Object);
    }

    [Fact]
    public void GetPreference_StringKey_ReturnsStoredValue()
    {
        var prefs = new Dictionary<string, object> { { "Theme", "Dark" } };
        string json = JsonSerializer.Serialize(prefs);

        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystemService.Setup(f => f.ReadAllText(It.IsAny<string>()))
            .Returns(json);

        var result = _service.GetPreference("Theme", "Light");

        // STJ deserializes string values as JsonElement; the conversion logic handles it
        result.Should().Be("Dark");
    }

    [Fact]
    public async Task SetPreferenceAsync_StoresValue_AndSavesToFile()
    {
        _mockFileSystemService
            .Setup(f => f.FileExists(It.IsAny<string>()))
            .Returns(false);

        // After writing, file "exists"
        string? writtenContent = null;
        _mockFileSystemService
            .Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .Callback<string, string, System.Threading.CancellationToken>((_, content, _) =>
            {
                writtenContent = content;
                _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            })
            .Returns(Task.CompletedTask);

        var result = await _service.SetPreferenceAsync("Theme", "Dark");

        result.Success.Should().BeTrue();
        writtenContent.Should().NotBeNull();
        writtenContent.Should().Contain("Theme");
        writtenContent.Should().Contain("Dark");
    }

    [Fact]
    public async Task SetPreferenceAsync_UpdatesExistingKey()
    {
        var existingPrefs = new Dictionary<string, object> { { "Theme", "Light" } };
        string existingJson = JsonSerializer.Serialize(existingPrefs);

        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystemService.Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), default))
            .ReturnsAsync(existingJson);

        string? writtenContent = null;
        _mockFileSystemService
            .Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .Callback<string, string, System.Threading.CancellationToken>((_, content, _) =>
                writtenContent = content)
            .Returns(Task.CompletedTask);

        var result = await _service.SetPreferenceAsync("Theme", "Dark");

        result.Success.Should().BeTrue();
        writtenContent.Should().NotBeNull();
        writtenContent.Should().Contain("Dark");
        writtenContent.Should().NotContain("\"Light\"");
    }

    [Fact]
    public async Task GetPreferencesAsync_FileDoesNotExist_ReturnsEmptyDictionary()
    {
        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        var result = await _service.GetPreferencesAsync();

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPreferencesAsync_FileExists_ReturnsDeserializedPreferences()
    {
        var prefs = new Dictionary<string, object>
        {
            { "Theme", "Dark" },
            { "FontSize", 14 },
            { "AutoUpdate", true }
        };
        string json = JsonSerializer.Serialize(prefs);

        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystemService.Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), default))
            .ReturnsAsync(json);

        var result = await _service.GetPreferencesAsync();

        result.Should().NotBeNull();
        result.Should().HaveCount(3);
        result.Should().ContainKey("Theme");
        result.Should().ContainKey("FontSize");
        result.Should().ContainKey("AutoUpdate");
    }

    [Fact]
    public async Task GetPreferencesAsync_CorruptJson_ReturnsEmptyDictionary()
    {
        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystemService.Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), default))
            .ReturnsAsync("{ this is not valid json }}}");

        var result = await _service.GetPreferencesAsync();

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SavePreferencesAsync_WritesToFile_ReturnsSuccess()
    {
        var prefs = new Dictionary<string, object>
        {
            { "Theme", "Dark" },
            { "AutoUpdate", true }
        };

        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystemService
            .Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);

        var result = await _service.SavePreferencesAsync(prefs);

        result.Success.Should().BeTrue();
        _mockFileSystemService.Verify(
            f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), default),
            Times.Once);
    }

    [Fact]
    public async Task SavePreferencesAsync_FileNotFoundAfterWrite_ReturnsFailure()
    {
        var prefs = new Dictionary<string, object> { { "Key", "Value" } };

        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
        _mockFileSystemService
            .Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);

        var result = await _service.SavePreferencesAsync(prefs);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("File not found after writing");
    }

    [Fact]
    public async Task SavePreferencesAsync_WriteThrows_ReturnsFailure()
    {
        var prefs = new Dictionary<string, object> { { "Key", "Value" } };

        _mockFileSystemService
            .Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ThrowsAsync(new System.IO.IOException("Disk full"));

        var result = await _service.SavePreferencesAsync(prefs);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Disk full");
    }

    [Fact]
    public void GetPreference_KeyMissing_ReturnsDefaultValue()
    {
        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        var result = _service.GetPreference("MissingKey", 99);

        result.Should().Be(99);
    }

    [Fact]
    public void GetPreference_KeyExists_ReturnsValue()
    {
        var prefs = new Dictionary<string, object> { { "Volume", 75 } };
        string json = JsonSerializer.Serialize(prefs);

        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystemService.Setup(f => f.ReadAllText(It.Is<string>(s => s.Contains("UserPreferences"))))
            .Returns(json);

        var result = _service.GetPreference("Volume", 50);

        // STJ deserializes numbers as JsonElement; JsonElement.Deserialize<int>() handles it
        result.Should().Be(75);
    }

    [Fact]
    public void GetPreference_EmptyFile_ReturnsDefaultValue()
    {
        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystemService.Setup(f => f.ReadAllText(It.Is<string>(s => s.Contains("UserPreferences"))))
            .Returns(string.Empty);

        var result = _service.GetPreference("AnyKey", "fallback");

        result.Should().Be("fallback");
    }

    [Fact]
    public void GetPreference_BoolFromJson_ReturnsCorrectBool()
    {
        var prefs = new Dictionary<string, object> { { "DarkMode", true } };
        string json = JsonSerializer.Serialize(prefs);

        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystemService.Setup(f => f.ReadAllText(It.Is<string>(s => s.Contains("UserPreferences"))))
            .Returns(json);

        var result = _service.GetPreference("DarkMode", false);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task GetPreference_WhileSaveInFlight_WaitsForTheWrite()
    {
        var writeGate = new TaskCompletionSource();
        string? written = null;
        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystemService.Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), default)).ReturnsAsync("{}");
        _mockFileSystemService
            .Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .Callback<string, string, System.Threading.CancellationToken>((_, content, _) => written = content)
            .Returns(writeGate.Task);
        _mockFileSystemService.Setup(f => f.ReadAllText(It.IsAny<string>())).Returns(() => written ?? "{}");

        var save = _service.SetPreferenceAsync("InitialConfigBackupCompleted", true);
        var read = Task.Run(() => _service.GetPreference("InitialConfigBackupCompleted", false));

        (await Task.WhenAny(read, Task.Delay(200))).Should().NotBeSameAs(read);
        _mockFileSystemService.Verify(f => f.ReadAllText(It.IsAny<string>()), Times.Never);

        writeGate.SetResult();
        await save;
        (await read).Should().BeTrue();
    }

    [Fact]
    public async Task GetPreference_ManyReads_ParseTheFileOnce()
    {
        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystemService.Setup(f => f.ReadAllText(It.IsAny<string>()))
            .Returns(JsonSerializer.Serialize(new Dictionary<string, object> { { "Theme", "Dark" } }));

        for (int i = 0; i < 20; i++)
            _service.GetPreference("Theme", "Light").Should().Be("Dark");
        (await _service.GetPreferencesAsync()).Should().ContainKey("Theme");

        _mockFileSystemService.Verify(f => f.ReadAllText(It.IsAny<string>()), Times.Once);
        _mockFileSystemService.Verify(f => f.ReadAllTextAsync(It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task GetPreference_AfterASave_SeesTheSavedValueWithoutRereadingTheFile()
    {
        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystemService.Setup(f => f.ReadAllText(It.IsAny<string>()))
            .Returns(JsonSerializer.Serialize(new Dictionary<string, object> { { "Theme", "Light" } }));
        _mockFileSystemService
            .Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);

        _service.GetPreference("Theme", "").Should().Be("Light");
        (await _service.SetPreferenceAsync("Theme", "Dark")).Success.Should().BeTrue();
        (await _service.SetPreferenceAsync("Volume", 75)).Success.Should().BeTrue();

        _service.GetPreference("Theme", "").Should().Be("Dark");
        _service.GetPreference("Volume", 0).Should().Be(75);
        (await _service.GetPreferencesAsync()).Should().ContainKeys("Theme", "Volume");
        _mockFileSystemService.Verify(f => f.ReadAllText(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task SavePreferencesAsync_Always_ReplacesWhatLaterReadsSee()
    {
        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystemService.Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), default))
            .ReturnsAsync(JsonSerializer.Serialize(new Dictionary<string, object> { { "Theme", "Light" } }));
        _mockFileSystemService
            .Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);

        var prefs = await _service.GetPreferencesAsync();
        prefs["Theme"] = "Dark";
        await _service.SavePreferencesAsync(prefs);

        _service.GetPreference("Theme", "").Should().Be("Dark");
    }

    [Fact]
    public async Task GetPreferencesAsync_WhenReturnedDictionaryEditedWithoutSaving_ChangesNothing()
    {
        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystemService.Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), default))
            .ReturnsAsync(JsonSerializer.Serialize(new Dictionary<string, object> { { "Theme", "Light" } }));

        var prefs = await _service.GetPreferencesAsync();
        prefs["Theme"] = "Dark";
        prefs["Extra"] = true;

        _service.GetPreference("Theme", "").Should().Be("Light");
        (await _service.GetPreferencesAsync()).Should().NotContainKey("Extra");
    }

    [Fact]
    public async Task SetPreferenceAsync_WhenSaveFails_MakesTheNextReadGoBackToTheFile()
    {
        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystemService.Setup(f => f.ReadAllTextAsync(It.IsAny<string>(), default))
            .ReturnsAsync(JsonSerializer.Serialize(new Dictionary<string, object> { { "Theme", "Light" } }));
        _mockFileSystemService.Setup(f => f.ReadAllText(It.IsAny<string>()))
            .Returns(JsonSerializer.Serialize(new Dictionary<string, object> { { "Theme", "Light" } }));
        _mockFileSystemService
            .Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ThrowsAsync(new System.IO.IOException("Disk full"));

        (await _service.SetPreferenceAsync("Theme", "Dark")).Success.Should().BeFalse();

        _service.GetPreference("Theme", "").Should().Be("Light");
        _mockFileSystemService.Verify(f => f.ReadAllTextAsync(It.IsAny<string>(), default), Times.Once);
        _mockFileSystemService.Verify(f => f.ReadAllText(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void GetPreference_AfterAFailedRead_TriesAgainRatherThanCaching()
    {
        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystemService.SetupSequence(f => f.ReadAllText(It.IsAny<string>()))
            .Throws(new System.IO.IOException("in use"))
            .Returns(JsonSerializer.Serialize(new Dictionary<string, object> { { "Theme", "Dark" } }));

        _service.GetPreference("Theme", "Light").Should().Be("Light");
        _service.GetPreference("Theme", "Light").Should().Be("Dark");
    }

    [Fact]
    public void GetPreference_RepeatedReads_LogsThePreferenceCountOnlyOnTheRealLoad()
    {
        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystemService.Setup(f => f.ReadAllText(It.IsAny<string>()))
            .Returns(JsonSerializer.Serialize(new Dictionary<string, object> { { "Theme", "Dark" } }));

        for (int i = 0; i < 5; i++)
            _service.GetPreference("Theme", "Light");

        _mockLogService.Verify(
            l => l.Log(Winhance.Core.Features.Common.Enums.LogLevel.Info, It.Is<string>(m => m.StartsWith("Loaded 1 preferences", StringComparison.Ordinal)), null, It.IsAny<string>()),
            Times.Once);
    }
}
