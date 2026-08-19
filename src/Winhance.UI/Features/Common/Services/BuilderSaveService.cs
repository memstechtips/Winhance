using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

public sealed class BuilderSaveService : IBuilderSaveService
{
    private readonly ISelectionSetBuilder _selections;
    private readonly IConfigFileWriter _configFiles;

    // Interim, until Task 3.4 gives the autounattend its own writer: this service still has to build the
    // config file itself for the XML generator to consume.
    private readonly IAutounattendXmlGeneratorService _generator;
    private readonly ICatalogSettingsRegistry _registry;

    private readonly ISaveFilePicker _picker;
    private readonly IDialogService _dialogs;
    private readonly ILocalizationService _loc;
    private readonly ILogService _log;

    public BuilderSaveService(
        ISelectionSetBuilder selections,
        IConfigFileWriter configFiles,
        IAutounattendXmlGeneratorService generator,
        ICatalogSettingsRegistry registry,
        ISaveFilePicker picker,
        IDialogService dialogs,
        ILocalizationService loc,
        ILogService log)
    {
        _selections = selections;
        _configFiles = configFiles;
        _generator = generator;
        _registry = registry;
        _picker = picker;
        _dialogs = dialogs;
        _loc = loc;
        _log = log;
    }

    public async Task SaveAsync(BuilderTarget target)
    {
        try
        {
            var set = await _selections.FromBuilderSessionAsync();
            string? path;
            if (target == BuilderTarget.Config)
            {
                path = _picker.PickSavePath(
                    _loc.GetString("Config_FileDialog_SaveConfig"),
                    ConfigFileConstants.FileFilter,
                    ConfigFileConstants.FilePattern,
                    $"Winhance_Config_{DateTime.Now:yyyyMMdd}{ConfigFileConstants.FileExtension}",
                    "winhance");
                if (string.IsNullOrEmpty(path))
                {
                    _log.Log(LogLevel.Info, "Builder config save: no save path chosen");
                    return;
                }
                await _configFiles.WriteAsync(set, _selections.CurrentScope, path);
            }
            else
            {
                path = _picker.PickSavePath(
                    _loc.GetString("AdvancedTools_FileDialog_SaveXml"),
                    "Autounattend XML File",
                    "*.xml",
                    "autounattend.xml",
                    "xml");
                if (string.IsNullOrEmpty(path))
                {
                    _log.Log(LogLevel.Info, "Builder autounattend save: no save path chosen");
                    return;
                }

                await _registry.InitializeAsync();
                var file = ConfigFileMapper.ToFile(set, _registry.GetAll(includeOtherOsVersions: _selections.CurrentScope.IncludeOtherOsVersions));
                await _generator.GenerateFromConfigAsync(file, path);
            }

            _log.Log(LogLevel.Info, $"Builder {target} saved to {path}");
            await _dialogs.ShowInformationAsync(
                _loc.GetString("Config_Export_Success_Message", path),
                _loc.GetString("Config_Export_Success_Title"));
        }
        catch (Exception ex)
        {
            _log.Log(LogLevel.Error, $"Builder Save failed: {ex.Message}");
            await _dialogs.ShowErrorAsync(
                _loc.GetString("Config_Export_Error_Message", ex.Message),
                _loc.GetString("Config_Export_Error_Title"));
        }
    }
}
