using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Autounattend.Services;

internal sealed class AutounattendReader : IAutounattendReader
{
    private static readonly XNamespace X = AutounattendDocument.WinhanceExtensions;

    private readonly IFileSystemService _files;
    private readonly IConfigMigrationService _migration;
    private readonly ILogService _log;

    public AutounattendReader(IFileSystemService files, IConfigMigrationService migration, ILogService log)
    {
        _files = files;
        _migration = migration;
        _log = log;
    }

    public async Task<WinhanceAnswerFile?> ReadAsync(string path)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(await _files.ReadAllTextAsync(path).ConfigureAwait(false));
        }
        catch (XmlException)
        {
            return null;
        }

        var extensions = doc.Root?.Element(X + "Extensions");
        if (extensions?.Element(X + "Generator") is not { } generator)
            return null;

        var version = (string?)generator.Attribute("version") ?? string.Empty;
        if (extensions.Element(X + "Config") is not { } embedded)
            return new WinhanceAnswerFile(version, null);

        try
        {
            var config = JsonSerializer.Deserialize<WinhanceConfigFile>(embedded.Value, ConfigFileConstants.JsonOptions);
            if (config is not null)
            {
                _migration.MigrateConfig(config);
                AttachCarriedPictures(config, extensions);
            }
            return new WinhanceAnswerFile(version, config);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.Log(LogLevel.Warning, $"The choices carried in {path} could not be read: {ex.Message}");
            return new WinhanceAnswerFile(version, null);
        }
    }

    // The writer carries each picture once, as a File element, so the choice that names it gets its bytes from there.
    private static void AttachCarriedPictures(WinhanceConfigFile config, XElement extensions)
    {
        var pictures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in extensions.Elements(X + "File").Where(f => (string?)f.Attribute("encoding") == "base64"))
            pictures[(string?)file.Attribute("path") ?? string.Empty] = file.Value.Trim();

        var items = config.Optimize.Features.Values
            .Concat(config.Customize.Features.Values)
            .Concat(config.Autounattend.Features.Values)
            .SelectMany(section => section.Items);
        foreach (var item in items)
        {
            if (item.File is null && item.SelectedKey is { } key && pictures.TryGetValue(key, out var base64))
                item.File = new CarriedFileItem { Destination = key, Base64 = base64 };
        }
    }
}
