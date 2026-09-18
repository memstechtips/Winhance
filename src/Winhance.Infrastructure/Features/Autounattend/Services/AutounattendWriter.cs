using Winhance.Core.Features.Autounattend.Interfaces;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;

namespace Winhance.Infrastructure.Features.Autounattend.Services;

internal sealed class AutounattendWriter : IAutounattendWriter
{
    private readonly ICatalogSettingsRegistry _registry;
    private readonly IWinhancementsScriptBuilder _scriptBuilder;
    private readonly IPowerShellRunner _powerShell;
    private readonly IFileSystemService _files;
    private readonly IVersionService _version;
    private readonly ILogService _log;

    public AutounattendWriter(
        ICatalogSettingsRegistry registry,
        IWinhancementsScriptBuilder scriptBuilder,
        IPowerShellRunner powerShell,
        IFileSystemService files,
        IVersionService version,
        ILogService log)
    {
        _registry = registry;
        _scriptBuilder = scriptBuilder;
        _powerShell = powerShell;
        _files = files;
        _version = version;
        _log = log;
    }

    public async Task<string> WriteAsync(SelectionSet set, CatalogScope scope, string outputPath)
    {
        await _registry.InitializeAsync().ConfigureAwait(false);
        var byFeature = _registry.GetAll(scope);

        _log.Log(LogLevel.Info, $"Generating autounattend.xml from {set.Settings.Count} setting choices and {set.WindowsApps.Count} Windows apps");
        var script = await _scriptBuilder.BuildAsync(set, byFeature).ConfigureAwait(false);
        var context = new AutounattendRenderContext(
            script, _version.GetCurrentVersion().Version.TrimStart('v'), set);
        var xml = AutounattendDocumentBuilder.Serialize(AutounattendDocumentBuilder.Build(context));

        try
        {
            await _powerShell.ValidateXmlSyntaxAsync(xml).ConfigureAwait(false);
            _log.Log(LogLevel.Info, "autounattend.xml passed XML well-formedness validation");
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Error, $"autounattend.xml failed XML well-formedness validation: {ex.Message}");
            throw;
        }

        // Windows Setup requires UTF-8 without a BOM; File.WriteAllTextAsync's default encoding writes none.
        await _files.WriteAllTextAsync(outputPath, xml).ConfigureAwait(false);
        _log.Log(LogLevel.Info, $"Autounattend.xml generated successfully: {outputPath}");
        return outputPath;
    }
}
