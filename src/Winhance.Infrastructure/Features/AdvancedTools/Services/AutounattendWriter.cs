using System.Reflection;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;

namespace Winhance.Infrastructure.Features.AdvancedTools.Services;

internal sealed class AutounattendWriter : IAutounattendWriter
{
    private const string TemplateResourceName = "Winhance.Infrastructure.Resources.AdvancedTools.autounattend-template.xml";
    private const string ScriptPlaceholder = "<!--SCRIPT_PLACEHOLDER-->";

    private readonly ICatalogSettingsRegistry _registry;
    private readonly IAutounattendScriptBuilder _scriptBuilder;
    private readonly IPowerShellRunner _powerShell;
    private readonly IFileSystemService _files;
    private readonly ILogService _log;

    public AutounattendWriter(
        ICatalogSettingsRegistry registry,
        IAutounattendScriptBuilder scriptBuilder,
        IPowerShellRunner powerShell,
        IFileSystemService files,
        ILogService log)
    {
        _registry = registry;
        _scriptBuilder = scriptBuilder;
        _powerShell = powerShell;
        _files = files;
        _log = log;
    }

    public async Task<string> WriteAsync(SelectionSet set, CatalogScope scope, string outputPath)
    {
        await _registry.InitializeAsync().ConfigureAwait(false);
        var byFeature = _registry.GetAll(includeOtherOsVersions: scope.IncludeOtherOsVersions);

        var script = await _scriptBuilder.BuildAsync(set, byFeature).ConfigureAwait(false);
        var xml = InjectScript(LoadTemplate(), script);

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

    private static string LoadTemplate()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(TemplateResourceName)
            ?? throw new FileNotFoundException($"Embedded template not found: {TemplateResourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string InjectScript(string template, string script)
    {
        if (!template.Contains(ScriptPlaceholder, StringComparison.Ordinal))
            throw new InvalidOperationException("Script placeholder not found in template");
        return template.Replace(ScriptPlaceholder, $"<![CDATA[{script}]]>");
    }
}
