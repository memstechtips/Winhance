using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.SoftwareApps.Models;
using Xunit;

namespace Winhance.Core.Tests.Selections;

public class ConfigCoverageTests
{
    private const string Desktop = "explorer-customization-thispc-folder-desktop";
    private const string Music = "explorer-customization-thispc-folder-music";
    private const string Pictures = "explorer-customization-thispc-folder-pictures";
    private const string Documents = "explorer-customization-thispc-folder-documents";

    private static WinhanceConfigFile FileWith(params (string Feature, string Id)[] items) => new()
    {
        Customize = new FeatureGroupSection
        {
            IsIncluded = true,
            Features = items.GroupBy(i => i.Feature).ToDictionary(
                g => g.Key,
                g => new ConfigSection { IsIncluded = true, Items = g.Select(i => new ConfigurationItem { Id = i.Id, Name = i.Id, IsSelected = true }).ToList() }),
        },
    };

    private static IReadOnlyDictionary<string, IReadOnlyList<Setting>> Shown(params string[] ids) =>
        ids.Select(id => SettingCatalog.ById[id]).GroupBy(s => SettingCatalog.FeatureById[s.Id]).ToDictionary(g => g.Key, g => (IReadOnlyList<Setting>)g.ToList());

    [Fact]
    public void Measure_SortsTheFileIntoRetiredMissingAndMoved()
    {
        SettingCatalog.FeatureById[Desktop].Should().Be(FeatureIds.Explorer);
        var file = FileWith(
            ("ExplorerCustomization", Desktop + "-win10"),
            (FeatureIds.Taskbar, Pictures),
            (FeatureIds.Explorer, Music),
            (FeatureIds.Explorer, "explorer-customization-gone-for-good"));

        var coverage = ConfigCoverage.Measure(file, Shown(Desktop, Music, Pictures, Documents));

        coverage.Retired.Select(i => i.Id).Should().Equal("explorer-customization-gone-for-good");
        coverage.Missing.Select(s => s.Id).Should().Equal(Documents);
        coverage.Moved.Should().Be(2);
        coverage.CanUpdate.Should().BeTrue();
    }

    [Fact]
    public void Measure_AFileThatCoversEverythingShown_IsComplete()
    {
        var coverage = ConfigCoverage.Measure(FileWith((FeatureIds.Explorer, Music)), Shown(Music));

        coverage.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void Measure_NeverCountsAnActionAsMissing()
    {
        var action = SettingCatalog.All.First(s => s.Control == ControlKind.Action
            && FeatureDefinitions.IsSettingsFeature(SettingCatalog.FeatureById[s.Id]));

        ConfigCoverage.Measure(FileWith(), Shown(action.Id)).Missing.Should().BeEmpty();
    }

    [Fact]
    public void Measure_AHandEditedFileWithNullSections_CountsWhatIsThere()
    {
        var file = FileWith((FeatureIds.Explorer, Music));
        file.Optimize = null!;
        file.Customize.Features = new Dictionary<string, ConfigSection>(file.Customize.Features)
        {
            [FeatureIds.Privacy] = null!,
            [FeatureIds.Taskbar] = new ConfigSection { Items = null! },
        };

        ConfigCoverage.Measure(file, Shown(Music)).IsComplete.Should().BeTrue();
    }

    [Fact]
    public void Updated_RefilesOldIdsAndFeatures_DropsRetiredItems_AndKeepsTheApps()
    {
        var file = FileWith(
            ("ExplorerCustomization", Desktop + "-win10"),
            (FeatureIds.Explorer, "explorer-customization-gone-for-good"));
        file.WindowsApps = new ConfigSection { IsIncluded = true, Items = [new ConfigurationItem { Id = "Microsoft.BingNews", Name = "News" }] };

        var updated = ConfigCoverage.Updated(file);

        updated.Customize.Features.Should().NotContainKey("ExplorerCustomization");
        updated.Customize.Features[FeatureIds.Explorer].Items.Select(i => i.Id).Should().Equal(Desktop);
        updated.WindowsApps.Should().BeSameAs(file.WindowsApps);
        ConfigCoverage.Measure(updated, Shown(Desktop)).IsComplete.Should().BeTrue();
    }

    [Fact]
    public void Updated_KeepsTheInstallOptionsOfTheFilesOwnApps()
    {
        var machine = new InstallOptions { Scope = InstallScope.Machine };
        var file = FileWith();
        file.ExternalApps = new ConfigSection { IsIncluded = true, Items = [new ConfigurationItem { Id = "in-file", Name = "In file" }] };
        file.AppInstallOptions = new InstallOptionsStore
        {
            Defaults = machine,
            Apps = new(StringComparer.Ordinal) { ["in-file"] = machine, ["not-in-file"] = machine },
        };

        var updated = ConfigCoverage.Updated(file);

        updated.AppInstallOptions!.Defaults.Should().Be(machine);
        updated.AppInstallOptions.Apps.Keys.Should().Equal("in-file");
    }

    [Fact]
    public void Updated_KeepsOneItem_WhenTheFileHoldsAnIdAndItsOldTwin()
    {
        var file = FileWith((FeatureIds.Explorer, Documents), (FeatureIds.Explorer, Documents + "-win10"));

        var updated = ConfigCoverage.Updated(file);

        updated.Customize.Features[FeatureIds.Explorer].Items.Select(i => i.Id).Should().Equal(Documents);
    }
}
