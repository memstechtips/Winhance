using System.Text.Json;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Common.TechnicalDetails;
using Xunit;

namespace Winhance.Infrastructure.Tests.Docs;

public class DocsCatalogExportShapeTests
{
    private static readonly EnJsonLocalization Loc = EnJsonLocalization.Load();
    private static readonly DocsExport Export = DocsCatalogExport.Build(Loc, "0.0.0");
    private static readonly string[] IconPacks = { "Material", "Fluent" };

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
    public void Every_setting_but_the_power_plan_picker_has_a_matrix()
    {
        var missing = Export.Features.SelectMany(f => f.Settings)
            .Where(s => s.Matrix is null && s.Control != "PowerPlan")
            .Select(s => s.Id)
            .ToList();

        Assert.Empty(missing);
        Assert.Null(Find("power-plan-selection").Matrix);
    }

    [Fact]
    public void Every_setting_exports_its_icon_identity()
    {
        var settings = Export.Features.SelectMany(f => f.Settings).ToList();
        Assert.All(settings, s => Assert.False(string.IsNullOrEmpty(s.Icon?.Name), $"{s.Id} has no icon"));
        Assert.All(settings, s => Assert.Contains(s.Icon!.Pack, IconPacks));
        Assert.Equal("MonitorSpeaker", Find("sound-startup").Icon!.Name);
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
        static IEnumerable<string> Labels(OptionMatrix? m) =>
            m is null ? [] : m.Options.Select(o => o.Label).Concat(m.CodeBlocks.Select(c => c.Label));

        var raw = Export.Features.SelectMany(f => f.Settings)
            .SelectMany(s => Labels(s.Matrix).Concat(Labels(s.MatrixWin10)).Select(l => $"{s.Id}: {l}"))
            .Where(l => SettingLocalizationKeys.IsLocalizationKey(l.Split(": ", 2)[1]))
            .ToList();

        Assert.Empty(raw);

        var ducking = Find("sound-communication-ducking").Matrix!;
        Assert.Equal(Loc.GetString("Setting_sound-communication-ducking_Option_3"), ducking.Options[3].Label);
        var displayTimeout = Find("power-display-timeout").Matrix!;
        Assert.Contains(displayTimeout.Options, o => o.Label == Loc.GetString("Template_TimeIntervals_Option_0"));
    }
}
