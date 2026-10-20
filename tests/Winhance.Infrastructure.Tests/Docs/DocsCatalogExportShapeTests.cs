using FluentAssertions;
using System.Text.Json;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.TechnicalDetails;
using Xunit;

namespace Winhance.Infrastructure.Tests.Docs;

public class DocsCatalogExportShapeTests
{
    private static readonly EnJsonLocalization Loc = EnJsonLocalization.Load();
    private static readonly DocsExport Export = DocsCatalogExport.Build(Loc, "0.0.0");
    private static readonly string[] IconPacks = { "Material", "Fluent", "AppAsset" };

    private static DocsSetting Find(string id) =>
        Export.Features.SelectMany(f => f.Settings).Single(s => s.Id == id);

    [Fact]
    public void Covers_the_whole_catalog_in_catalog_order()
    {
        Assert.True(Export.SettingCount > 300, $"only {Export.SettingCount} settings - catalog composition bug");
        Assert.Equal(SettingCatalog.All.Count, Export.SettingCount);
        Assert.Equal(SettingCatalog.ByFeature.Keys, Export.Features.Select(f => f.Id));
        Assert.Equal(SettingCatalog.All.Select(s => s.Id), Export.Features.SelectMany(f => f.Settings).Select(s => s.Id));
    }

    [Fact]
    public void Every_setting_has_a_matrix()
    {
        var missing = Export.Features.SelectMany(f => f.Settings)
            .Where(s => s.Matrix is null)
            .Select(s => s.Id)
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public void The_power_plan_picker_documents_the_predefined_plans_without_a_per_machine_status()
    {
        var matrix = Find("power-plan-selection").Matrix;

        Assert.NotNull(matrix);
        Assert.Equal(PowerPlanCatalog.BuiltInPowerPlans.Count, matrix.Options.Count);
        Assert.All(PowerPlanCatalog.BuiltInPowerPlans,
            p => Assert.Contains(matrix.Options, o => o.Cells.Any(c => c.Text == p.Guid)));
        Assert.Single(matrix.Columns);
    }

    // The website draws these rows in the order they are exported.
    [Fact]
    public void The_picture_and_the_colour_document_the_options_every_install_has()
    {
        var picture = Find("theme-wallpaper-picture").Matrix!;
        var color = Find("theme-wallpaper-color").Matrix!;

        picture.Options.Select(o => o.Label).Should().Equal(
            SettingCatalog.Find("theme-wallpaper-picture")!.States.Select(s => Loc.GetString(s.Label.Value)));
        picture.Options.Select(o => o.Cells[^1].Text).Should().Equal(
            @"C:\Windows\Web\Wallpaper\Windows\img0.jpg",
            @"C:\Windows\Web\Wallpaper\Windows\img19.jpg",
            @"C:\Windows\Web\4K\Wallpaper\Windows\img0_3840x2160.jpg");
        color.Options.Should().HaveCount(24);
        color.Options[0].Label.Should().Be("#FF8C00");
        color.Options[^1].Label.Should().Be("#000000");
    }

    [Fact]
    public void The_desktop_background_parent_exports_as_a_selection_its_children_hang_off()
    {
        var parent = Find("theme-wallpaper");
        var picture = Find("theme-wallpaper-picture");

        Assert.Equal("Selection", parent.Control);
        Assert.Equal(Loc.GetString("SettingGroup_Background"), parent.Group);
        Assert.NotNull(parent.Matrix);
        Assert.Null(parent.UiParentId);
        Assert.Equal("KeyedSelection", picture.Control);
        Assert.Equal("theme-wallpaper", picture.UiParentId);
    }

    [Fact]
    public void Every_setting_exports_its_icon_identity()
    {
        var settings = Export.Features.SelectMany(f => f.Settings).ToList();
        Assert.All(settings, s => Assert.False(string.IsNullOrEmpty(s.Icon?.Name), $"{s.Id} has no icon"));
        Assert.All(settings, s => Assert.Contains(s.Icon!.Pack, IconPacks));
        Assert.Equal("MonitorSpeaker", Find("sound-startup").Icon!.Name);
        Assert.Equal(new DocsIcon("AppAsset", "winhance-rocket-card.png"), Find("autounattend-desktop-shortcut").Icon);
    }

    [Fact]
    public void Strings_come_from_en_json_not_the_catalog_literal()
    {
        var s = Find("sound-startup");

        Assert.Equal(Loc.GetString("Setting_sound-startup_Name"), s.Name);
        Assert.Equal(Loc.GetString("Setting_sound-startup_Description"), s.Description);
        Assert.Equal(Loc.GetString("SettingGroup_System_Sounds"), s.Group);   // compact key absent, snake form is what ships
        Assert.DoesNotContain("[", s.Name);
    }

    [Fact]
    public void Win10_matrix_is_carried_only_when_it_differs()
    {
        // Light Mode is the Windows default only on 11, so the two builds disagree on a role badge.
        Assert.NotNull(Find("theme-mode-windows").MatrixWin10);
        Assert.Null(Find("sound-startup").MatrixWin10);
    }

    [Fact]
    public void Compatibility_message_is_the_apps_localized_sentence()
    {
        var s = Find("explorer-customization-context-menu");

        Assert.Equal(Loc.GetString("Compatibility_Windows11Only"), s.Availability.Message.Win10);
        Assert.Null(s.Availability.Message.Win11);
        Assert.Equal("22000.0", s.Availability.Builds[0].Min);
        Assert.Equal("*", s.Availability.Builds[0].Max);
    }

    [Fact]
    public void Ui_parents_resolve_to_exported_settings()
    {
        var ids = Export.Features.SelectMany(f => f.Settings).Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        var dangling = Export.Features.SelectMany(f => f.Settings)
            .Where(s => s.UiParentId is not null && !ids.Contains(s.UiParentId))
            .Select(s => $"{s.Id} -> {s.UiParentId}")
            .ToList();

        Assert.Empty(dangling);
    }

    [Fact]
    public void Json_is_ascii_camel_case_and_deterministic()
    {
        var first = DocsCatalogExport.ToJson(Export);
        var second = DocsCatalogExport.ToJson(DocsCatalogExport.Build(Loc, "0.0.0"));

        Assert.Equal(first, second);
        Assert.True(first.All(c => c < 128), "export contains non-ASCII");

        using var doc = JsonDocument.Parse(first);
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(19045, root.GetProperty("referenceBuilds").GetProperty("win10").GetInt32());
        var setting = root.GetProperty("features")[0].GetProperty("settings")[0];
        Assert.Equal("Selection", setting.GetProperty("control").GetString());
        Assert.Equal(JsonValueKind.Array, setting.GetProperty("matrix").GetProperty("options").ValueKind);
        Assert.Equal("Registry", setting.GetProperty("matrix").GetProperty("groups")[0].GetProperty("kind").GetString());
    }

    [Fact]
    public void Option_labels_are_resolved_never_raw_localization_keys()
    {
        // Reachable only if a resolution path hands back the key text on a miss.
        static bool LooksLikeARawKey(string label) =>
            label.StartsWith("Setting_", StringComparison.Ordinal)
            || label.StartsWith("Template_", StringComparison.Ordinal)
            || label.StartsWith("ServiceOption_", StringComparison.Ordinal);

        static IEnumerable<string> Labels(OptionMatrix? m) =>
            m is null ? [] : m.Options.Select(o => o.Label).Concat(m.CodeBlocks.Select(c => c.Label));

        var raw = Export.Features.SelectMany(f => f.Settings)
            .SelectMany(s => Labels(s.Matrix).Concat(Labels(s.MatrixWin10)).Select(l => $"{s.Id}: {l}"))
            .Where(l => LooksLikeARawKey(l.Split(": ", 2)[1]))
            .ToList();

        Assert.Empty(raw);

        var ducking = Find("sound-communication-ducking").Matrix!;
        Assert.Equal(Loc.GetString("Setting_sound-communication-ducking_Option_3"), ducking.Options[3].Label);
        var displayTimeout = Find("power-display-timeout").Matrix!;
        Assert.Contains(displayTimeout.Options, o => o.Label == Loc.GetString("Template_TimeIntervals_Option_0"));
    }

    [Fact]
    public void A_keyed_selection_documents_its_registry_targets_and_no_options()
    {
        // Its options are whatever Windows offers the machine, so there is no build-time list to publish.
        var zone = Find("region-time-zone");

        zone.Control.Should().Be("KeyedSelection");
        zone.Group.Should().Be(Loc.GetString("SettingGroup_Time"));
        zone.Name.Should().Be(Loc.GetString("Setting_region-time-zone_Name"));
        zone.Matrix.Should().NotBeNull();
        zone.Matrix!.Options.Should().BeEmpty();
        zone.Matrix.Groups.Should().Contain(g => g.Paths.Any(p =>
            p.Full == @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\TimeZoneInformation"));
    }

    [Fact]
    public void The_new_feature_carries_its_five_keyed_settings_and_the_six_moved_ones()
    {
        var feature = Export.Features.Single(f => f.Id == "TimeRegionLanguage");

        feature.Settings.Should().HaveCount(11);
        feature.Settings.Take(5).Select(s => s.Control).Should().AllBe("KeyedSelection");
        feature.Settings.Should().Contain(s => s.Id == "explorer-customization-short-date"
            && s.Group == Loc.GetString("SettingGroup_Formats"));
    }
}
