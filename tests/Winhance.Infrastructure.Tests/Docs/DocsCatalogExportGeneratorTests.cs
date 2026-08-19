using Winhance.Infrastructure.Tests.Catalog;
using Winhance.TestSupport;
using Xunit;
using Xunit.Abstractions;

namespace Winhance.Infrastructure.Tests.Docs;

// GENERATOR, not an assertion: writes the catalog + Technical Details export the winhance.net docs generator
// renders. Lives in a test project because nothing else can load the C# catalog, and runs on the Windows gate
// because Core is net10.0-windows. Run: winhance-harness DocsCatalogExport
[Collection(RepoFileWritersCollection.Name)]
public class DocsCatalogExportGeneratorTests
{
    private readonly ITestOutputHelper _output;

    public DocsCatalogExportGeneratorTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Generate_docs_catalog_export()
    {
        var solutionDir = RepoPaths.SolutionDir();
        var version = DocsCatalogExport.ReadCsprojVersion(solutionDir);
        var export = DocsCatalogExport.Build(EnJsonLocalization.Load(), version);
        Assert.True(export.SettingCount > 300, $"only {export.SettingCount} settings enumerated - catalog composition bug.");

        var json = DocsCatalogExport.ToJson(export);
        Assert.True(json.All(c => c < 128), "export contains non-ASCII.");

        var dir = Path.Combine(solutionDir, "extras", "docs-export");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "catalog.json");
        var changed = GeneratedFile.WriteIfChanged(path, Crlf(json));

        _output.WriteLine($"winhanceVersion : {version}");
        _output.WriteLine($"catalogHash     : {export.CatalogHash}");
        _output.WriteLine($"settings        : {export.SettingCount}");
        _output.WriteLine($"{(changed ? "wrote" : "unchanged")}           : {path}");
    }

    // The repo is CRLF throughout and the generator may run from either OS.
    private static string Crlf(string text) => text.Replace("\r\n", "\n").Replace("\n", "\r\n");
}
