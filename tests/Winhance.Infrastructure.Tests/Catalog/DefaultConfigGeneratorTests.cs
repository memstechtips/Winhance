using System.Text.Json;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Models;
using Winhance.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Catalog;

/// <summary>
/// GENERATOR (not an assertion test): regenerates the two shipped Default configs
/// (<c>Winhance_Default_Config_Windows10_22H2.winhance</c> / <c>..._Windows11_25H2.winhance</c>) from the live
/// catalog via <see cref="DefaultConfigProjection"/> - Step 3 of the Windows-defaults audit. The old files were
/// one-shot VM exports and drifted from the catalog's (since-corrected) WindowsDefault roles; from now on the
/// catalog IS the source and this generator is COMMITTED (the previous one-shot config generator was deleted and
/// regenerating it cost real time - CatalogProbeManifestGeneratorTests precedent).
///
/// Version / CreatedAt / WindowsApps / ExternalApps are carried over verbatim from the existing file (the apps
/// sections are user-selection data, out of catalog scope). Customize/Optimize are rebuilt wholesale: per-build
/// availability + WindowsDefault, catalog order within each feature. Output is UTF-8 no BOM, CRLF, the app's own
/// <see cref="ConfigFileConstants.JsonOptions"/> shape - byte-stable across regenerations of unchanged data.
///
/// Run: winhance-harness DefaultConfigGenerator
/// </summary>
[Collection(RepoFileWritersCollection.Name)]
public class DefaultConfigGeneratorTests
{
    private readonly ITestOutputHelper _output;

    public DefaultConfigGeneratorTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Generate_default_configs_from_catalog()
    {
        foreach (var (fileName, build) in DefaultConfigProjection.Targets)
        {
            string path = DefaultConfigProjection.ConfigPath(fileName);

            // Version / CreatedAt / the apps sections are carried forward from the file, so a damaged file
            // cannot be regenerated from the catalog alone. Say that, rather than surfacing a raw JsonException.
            UnifiedConfigurationFile existing;
            try
            {
                existing = JsonSerializer.Deserialize<UnifiedConfigurationFile>(
                        File.ReadAllText(path), ConfigFileConstants.JsonOptions)
                    ?? throw new InvalidOperationException($"{fileName} deserialized to null.");
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"{fileName} is not valid JSON ({new FileInfo(path).Length} bytes) - it is damaged, not "
                    + $"drifted. Restore it (git restore) and re-run; the generator carries data forward from "
                    + $"it and cannot rebuild it from the catalog alone.", ex);
            }

            var generated = new UnifiedConfigurationFile
            {
                Version = existing.Version,
                CreatedAt = existing.CreatedAt,
                WindowsApps = existing.WindowsApps,
                ExternalApps = existing.ExternalApps,
                Customize = BuildSection(DefaultConfigProjection.CustomizeFeatures, build),
                Optimize = BuildSection(DefaultConfigProjection.OptimizeFeatures, build),
            };

            int items = generated.Customize.Features.Values.Sum(f => f.Items.Count)
                + generated.Optimize.Features.Values.Sum(f => f.Items.Count);
            Assert.True(items > 250, $"{fileName}: only {items} projected items - projection/scoping bug, refusing to write.");

            string json = JsonSerializer.Serialize(generated, ConfigFileConstants.JsonOptions);
            json = json.Replace("\r\n", "\n").Replace("\n", "\r\n") + "\r\n";
            bool rewritten = GeneratedFile.WriteIfChanged(path, json);
            _output.WriteLine($"{(rewritten ? "wrote" : "unchanged")} {fileName}: {items} setting items for build {build.Build}.");
        }
    }

    private static FeatureGroupSection BuildSection(IReadOnlyList<string> featureIds, WinBuild build)
    {
        var features = new Dictionary<string, ConfigSection>();
        foreach (var featureId in featureIds)
        {
            var settings = SettingCatalog.ByFeature[featureId];
            var projected = settings
                .Select(s => DefaultConfigProjection.Project(s, build))
                .Where(item => item is not null)
                .Select(item => item!)
                .ToList();
            features[featureId] = new ConfigSection { IsIncluded = true, Items = projected };
        }
        return new FeatureGroupSection { IsIncluded = true, Features = features };
    }
}
