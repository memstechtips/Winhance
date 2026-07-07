using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Models;
using Winhance.Core.Features.Optimize.Models;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Migration;

/// <summary>Slice C/D foundation (additive, wired to nothing yet): proves the new catalog-Setting overloads of the
/// SettingLocalizationKeys key-builders produce byte-identical keys to the SettingDefinition versions over the whole
/// paired population. Both build <c>Setting_{base}_...</c>; the def base is <c>LocalizationId ?? Id</c>, the catalog
/// base is <c>Setting.Id</c>, and those are equal for every paired setting (LocalizeDisplayReadSwapEquivalenceTests
/// already proved it for Name/Description; this locks it at the key-builder method level for the whole family). Lets
/// the SAS change-history rendering (Name/OptionDisplay) + the LocalizationKeyReferenceTests port key off a catalog
/// Setting instead of a def. ExpectedKeys(Setting) is intentionally NOT ported here - it walks the catalog States and
/// needs its own set-equivalence proof, done with the test port that consumes it. Machine-independent (catalog + old
/// defs only). Run: dotnet test --filter SettingLocalizationKeysCatalog</summary>
public class SettingLocalizationKeysCatalogEquivalenceTests
{
    private readonly ITestOutputHelper _output;

    public SettingLocalizationKeysCatalogEquivalenceTests(ITestOutputHelper output) => _output = output;

    private static IEnumerable<SettingDefinition> AllDefinitions()
    {
        return new[]
        {
            ExplorerCustomizations.GetExplorerCustomizations().Settings,
            StartMenuCustomizations.GetStartMenuCustomizations().Settings,
            TaskbarCustomizations.GetTaskbarCustomizations().Settings,
            WindowsThemeCustomizations.GetWindowsThemeCustomizations().Settings,
            PowerOptimizations.GetPowerOptimizations().Settings,
            GamingAndPerformanceOptimizations.GetGamingAndPerformanceOptimizations().Settings,
            NotificationOptimizations.GetNotificationOptimizations().Settings,
            PrivacyAndSecurityOptimizations.GetPrivacyAndSecurityOptimizations().Settings,
            SoundOptimizations.GetSoundOptimizations().Settings,
            UpdateOptimizations.GetUpdateOptimizations().Settings,
        }.SelectMany(group => group);
    }

    [Fact]
    public void CatalogKeyBuilders_MatchDefVersions_OverAllSettings()
    {
        var catalogById = SettingCatalog.All.ToDictionary(s => s.Id);
        var mismatches = new List<string>();
        var compared = 0;
        var sawKnownPositive = false;

        foreach (var def in AllDefinitions())
        {
            if (!catalogById.TryGetValue(SettingIdAliases.Normalize(def.Id), out var setting))
                continue;
            compared++;
            if (def.Id == "gaming-game-mode")
                sawKnownPositive = true;

            void Check(string what, string defKey, string catKey)
            {
                if (defKey != catKey)
                    mismatches.Add($"{def.Id}: {what} def '{defKey}' != catalog '{catKey}'");
            }

            Check("Name", SettingLocalizationKeys.Name(def), SettingLocalizationKeys.Name(setting));
            Check("Description", SettingLocalizationKeys.Description(def), SettingLocalizationKeys.Description(setting));
            Check("OptionCustom", SettingLocalizationKeys.OptionCustom(def), SettingLocalizationKeys.OptionCustom(setting));

            // The per-option keys are index-parametric (Setting_{base}_Option_{i} etc.), so equality at a spread of
            // indices proves the base is identical - the only thing that can differ between the def and catalog overloads.
            foreach (var i in new[] { 0, 1, 3 })
            {
                Check($"OptionDisplay[{i}]", SettingLocalizationKeys.OptionDisplay(def, i), SettingLocalizationKeys.OptionDisplay(setting, i));
                Check($"OptionTooltip[{i}]", SettingLocalizationKeys.OptionTooltip(def, i), SettingLocalizationKeys.OptionTooltip(setting, i));
                Check($"OptionWarning[{i}]", SettingLocalizationKeys.OptionWarning(def, i), SettingLocalizationKeys.OptionWarning(setting, i));
            }
        }

        _output.WriteLine($"{compared} settings compared, {mismatches.Count} mismatches");
        foreach (var m in mismatches)
            _output.WriteLine("  " + m);

        Assert.True(compared > 300, $"only {compared} settings paired - population scoping bug");
        Assert.True(sawKnownPositive, "known-positive 'gaming-game-mode' not in the paired population - vacuity/scoping bug");
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} key-builder catalog-vs-def mismatches:\n" + string.Join("\n", mismatches));
    }
}
