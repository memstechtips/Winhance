using System.Text.Json;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class CoverageSummaryServiceTests
{
    private const string Music = "explorer-customization-thispc-folder-music";
    private const string Documents = "explorer-customization-thispc-folder-documents";
    private const string Version = "26.10.22";
    private const string ReminderKey = UserPreferenceKeys.ProfileReminderOffPrefix + "Gaming";

    private static readonly SettingsProfile Gaming = new("Gaming", "Gaming", ProfileKind.User, @"C:\p\Gaming.winhance");

    private readonly Mock<IDialogService> _dialogs = new();
    private readonly Mock<IUserPreferencesService> _preferences = new();
    private readonly Mock<ICatalogSettingsRegistry> _registry = new();
    private readonly Mock<IVersionService> _version = new();
    private readonly Mock<IConfigLoadService> _configLoad = new();
    private readonly Mock<IConfigImportState> _importState = new();
    private readonly Mock<IFileSystemService> _files = new();
    private readonly Mock<ISettingSnapshotSource> _snapshot = new();
    private SummaryRequest? _shown;

    public CoverageSummaryServiceTests()
    {
        _preferences.Setup(p => p.GetPreference(ReminderKey, string.Empty)).Returns(string.Empty);
        _version.Setup(v => v.GetCurrentVersion()).Returns(new VersionInfo { Version = Version });
        _registry.Setup(r => r.GetAll(CatalogScope.CurrentMachine)).Returns(new Dictionary<string, IReadOnlyList<Setting>>
        {
            [FeatureIds.Explorer] = [SettingCatalog.ById[Music], SettingCatalog.ById[Documents]],
        });
        Readable(Music, Documents);
        Answer(new ConfirmationResponse());
    }

    private void Readable(params string[] ids) =>
        _snapshot.Setup(s => s.CaptureAsync(CatalogScope.CurrentMachine))
            .ReturnsAsync(ids.Select(id => new SettingChoice(id, new ChoiceValue.Toggle(false))).ToList());

    private void Answer(ConfirmationResponse response) =>
        _dialogs.Setup(d => d.AskSummaryAsync(It.IsAny<SummaryRequest>()))
            .Callback((SummaryRequest r) => _shown = r)
            .ReturnsAsync(response);

    private CoverageSummaryService CreateService() => new(
        _importState.Object,
        _configLoad.Object,
        _registry.Object,
        _files.Object,
        _dialogs.Object,
        Mock.Of<ILocalizationService>(),
        Mock.Of<ILogService>(),
        _preferences.Object,
        _snapshot.Object,
        _version.Object);

    private static WinhanceConfigFile WithMusicOnly() => new()
    {
        Customize = new FeatureGroupSection
        {
            IsIncluded = true,
            Features = new Dictionary<string, ConfigSection>
            {
                [FeatureIds.Explorer] = new() { IsIncluded = true, Items = [new ConfigurationItem { Id = Music, Name = Music, IsSelected = true }] },
            },
        },
    };

    [Fact]
    public async Task ProfileReminder_ListsWhatTheProfileLeavesOut()
    {
        var edit = await CreateService().ShowProfileReminderAsync(Gaming, WithMusicOnly());

        edit.Should().BeFalse();
        _shown!.Sections.Should().ContainSingle().Which.Rows.Should().ContainSingle();
        _preferences.Verify(p => p.SetPreferenceAsync(ReminderKey, It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ProfileReminder_TickedOff_StaysQuietForThisVersionOnly()
    {
        Answer(new ConfirmationResponse { Confirmed = true, CheckboxChecked = true });

        var edit = await CreateService().ShowProfileReminderAsync(Gaming, WithMusicOnly());

        edit.Should().BeTrue();
        _preferences.Verify(p => p.SetPreferenceAsync(ReminderKey, Version), Times.Once);
    }

    [Fact]
    public async Task ProfileReminder_AlreadyTurnedOffForThisVersion_ShowsNothing()
    {
        _preferences.Setup(p => p.GetPreference(ReminderKey, string.Empty)).Returns(Version);

        await CreateService().ShowProfileReminderAsync(Gaming, WithMusicOnly());

        _shown.Should().BeNull();
    }

    [Fact]
    public async Task ProfileReminder_TurnedOffForAnOlderVersion_ComesBack()
    {
        _preferences.Setup(p => p.GetPreference(ReminderKey, string.Empty)).Returns("26.06.12");

        await CreateService().ShowProfileReminderAsync(Gaming, WithMusicOnly());

        _shown.Should().NotBeNull();
    }

    [Fact]
    public async Task ProfileReminder_NeverShowsForABuiltInProfile()
    {
        var recommended = Gaming with { Id = "Recommended", Kind = ProfileKind.Recommended, FilePath = null };

        await CreateService().ShowProfileReminderAsync(recommended, WithMusicOnly());

        _shown.Should().BeNull();
    }

    [Fact]
    public async Task ImportSummary_SavingAnUpdatedCopy_ReadsTheFileThroughTheMigratingReader()
    {
        const string path = @"C:\Configs\mine.winhance";
        _importState.SetupGet(s => s.SourcePath).Returns(path);
        _files.Setup(f => f.ReadAllTextAsync(path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(WithMusicOnly(), ConfigFileConstants.JsonOptions));
        _configLoad.Setup(c => c.ReadConfigurationAsync(path)).ReturnsAsync(WithMusicOnly());
        Answer(new ConfirmationResponse { Confirmed = true });

        await CreateService().ShowImportSummaryAsync([], []);

        _configLoad.Verify(c => c.ReadConfigurationAsync(path), Times.Once);
        _files.Verify(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ImportSummary_SavingAnUpdatedCopy_WhenTheReaderRefusesTheFile_WritesNothing()
    {
        const string path = @"C:\Configs\mine.winhance";
        _importState.SetupGet(s => s.SourcePath).Returns(path);
        _files.Setup(f => f.ReadAllTextAsync(path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(WithMusicOnly(), ConfigFileConstants.JsonOptions));
        _configLoad.Setup(c => c.ReadConfigurationAsync(path)).ReturnsAsync((WinhanceConfigFile?)null);
        Answer(new ConfirmationResponse { Confirmed = true });

        await CreateService().ShowImportSummaryAsync([], []);

        _files.Verify(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProfileReminder_ASettingThisPcCannotRead_IsNotAGap()
    {
        Readable(Music);

        (await CreateService().ShowProfileReminderAsync(Gaming, WithMusicOnly())).Should().BeFalse();

        _shown.Should().BeNull();
    }

    [Fact]
    public async Task ImportSummary_AFileThisPcCouldHaveSaved_IsNotOutOfDate()
    {
        const string path = @"C:\Configs\mine.winhance";
        _importState.SetupGet(s => s.SourcePath).Returns(path);
        _files.Setup(f => f.ReadAllTextAsync(path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(WithMusicOnly(), ConfigFileConstants.JsonOptions));
        Readable(Music);

        await CreateService().ShowImportSummaryAsync([], []);

        _shown!.Verdict.Should().BeNull();
        _shown.Sections.Should().BeEmpty();
    }

    [Fact]
    public async Task TakeProfileReminder_CountsTheGaps_AndUsesUpThisVersionsReminder()
    {
        var service = CreateService();

        (await service.TakeProfileReminderAsync(Gaming, WithMusicOnly())).Should().Be(1);

        _preferences.Verify(p => p.SetPreferenceAsync(ReminderKey, Version), Times.Once);
        _shown.Should().BeNull();
    }
}
