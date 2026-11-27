using Winhance.Core.Features.Autounattend.Interfaces;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.Infrastructure.Features.Autounattend.Helpers;

namespace Winhance.Infrastructure.Features.Autounattend.Services;

internal sealed class AutounattendWriter : IAutounattendWriter
{
    private readonly ICatalogSettingsRegistry _registry;
    private readonly IWinhancementsScriptBuilder _scriptBuilder;
    private readonly IFileSystemService _files;
    private readonly IVersionService _version;
    private readonly ILogService _log;

    public AutounattendWriter(
        ICatalogSettingsRegistry registry,
        IWinhancementsScriptBuilder scriptBuilder,
        IFileSystemService files,
        IVersionService version,
        ILogService log)
    {
        _registry = registry;
        _scriptBuilder = scriptBuilder;
        _files = files;
        _version = version;
        _log = log;
    }

    public Task<string> WriteAsync(SelectionSet set, CatalogScope scope, string outputPath) =>
        WriteCoreAsync(set, scope, outputPath, null);

    public async Task<string> WriteMediaAsync(SelectionSet set, CatalogScope scope, string outputPath, string mediaFolder,
        Func<string, Task> validate, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        string scratch = Path.Combine(mediaFolder, ".winhance-media-" + Guid.NewGuid().ToString("N"));
        string stagedXml = Path.Combine(scratch, "autounattend.xml");
        MediaPayloads.CheckNotRedirected(_files, scratch);
        _files.CreateDirectory(scratch);
        bool keepRecovery = false;
        try
        {
            await WriteCoreAsync(set, scope, stagedXml, scratch).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            await validate(stagedXml).ConfigureAwait(false);
            await MediaPayloads.PublishAsync(_files, stagedXml, outputPath, mediaFolder, scratch, ct).ConfigureAwait(false);
            return outputPath;
        }
        catch (MediaRollbackException)
        {
            keepRecovery = true;
            throw;
        }
        finally
        {
            if (!keepRecovery)
                _files.DeleteDirectory(scratch, recursive: true);
        }
    }

    private async Task<string> WriteCoreAsync(SelectionSet set, CatalogScope scope, string outputPath, string? mediaFolder)
    {
        if (mediaFolder is null)
            set = set.WithOnlySelectedPicture();
        await _registry.InitializeAsync().ConfigureAwait(false);
        var byFeature = _registry.GetAll(scope);

        _log.Log(LogLevel.Info, $"Generating autounattend.xml from {set.Settings.Count} setting choices and {set.WindowsApps.Count} Windows apps");
        var script = await _scriptBuilder.BuildAsync(set, byFeature, scope.TargetBuild).ConfigureAwait(false);
        // The pictures already travel as File elements, so the carried copy leaves them out. The passwords are
        // already in the file for Setup, so the copy keeps them for the next reopen.
        var config = ConfigFileMapper.ToFile(
            set with { Files = [], Settings = [.. set.Settings.Select(WithPasswords)] }, byFeature);
        config.TargetBuild = scope.TargetBuild?.Build;
        var context = new AutounattendRenderContext(
            script, _version.GetCurrentVersion().Version.TrimStart('v'), set)
        {
            Config = AnswerFileConfig.Serialize(config),
        };
        var doc = AutounattendDocumentBuilder.Build(context);
        if (mediaFolder is not null)
        {
            await MediaPayloads.StageAsync(_files, doc, mediaFolder).ConfigureAwait(false);
        }
        var xml = AutounattendDocumentBuilder.Serialize(doc);

        // Windows Setup requires UTF-8 without a BOM; File.WriteAllTextAsync's default encoding writes none.
        await _files.WriteAllTextAsync(outputPath, xml).ConfigureAwait(false);
        _log.Log(LogLevel.Info, $"Autounattend.xml generated successfully: {outputPath}");
        return outputPath;
    }

    private static SettingChoice WithPasswords(SettingChoice choice) =>
        choice.Value is ChoiceValue.List list ? choice with { Value = list with { SavePasswords = true } } : choice;
}
