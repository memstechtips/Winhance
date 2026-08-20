using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

public sealed class SelectionSaveService : ISelectionSaveService
{
    // Windows Setup only picks the answer file up under this exact name.
    private const string AutounattendFileName = "autounattend.xml";

    private readonly ISelectionSetBuilder _selections;
    private readonly IConfigFileWriter _configFiles;
    private readonly IAutounattendWriter _autounattend;
    private readonly ISaveFilePicker _picker;
    private readonly IDialogService _dialogs;
    private readonly ILocalizationService _loc;
    private readonly ILogService _log;

    public SelectionSaveService(
        ISelectionSetBuilder selections,
        IConfigFileWriter configFiles,
        IAutounattendWriter autounattend,
        ISaveFilePicker picker,
        IDialogService dialogs,
        ILocalizationService loc,
        ILogService log)
    {
        _selections = selections;
        _configFiles = configFiles;
        _autounattend = autounattend;
        _picker = picker;
        _dialogs = dialogs;
        _loc = loc;
        _log = log;
    }

    public async Task<string?> SaveAsync(BuilderTarget target, SelectionSet selections, SelectionSaveOptions? options = null)
    {
        options ??= new SelectionSaveOptions();

        if (options.ConfirmEmptyAppSelection
            && selections.WindowsApps.Count == 0
            && !await ConfirmEmptyAppSelectionAsync(target))
        {
            return null;
        }

        string? path = options.FixedPath;
        if (path == null)
        {
            path = PickDestination(target);
            if (string.IsNullOrEmpty(path))
            {
                _log.Log(LogLevel.Info, $"{target} save: no destination chosen");
                return null;
            }

            // A fixed path skips this - the caller named the file itself.
            if (target == BuilderTarget.Autounattend
                && !string.Equals(Path.GetFileName(path), AutounattendFileName, StringComparison.OrdinalIgnoreCase))
            {
                await _dialogs.ShowInformationAsync(
                    _loc.GetString("AdvancedTools_Msg_InvalidFilename"),
                    _loc.GetString("Dialog_Warning"));
                return null;
            }
        }

        string written = await WriteAsync(target, selections, path);
        _log.Log(LogLevel.Info, $"{target} saved to {written}");

        if (options.ReportSuccessInDialog)
            await ReportSuccessAsync(target, written);

        return written;
    }

    private async Task<bool> ConfirmEmptyAppSelectionAsync(BuilderTarget target)
    {
        string message = target == BuilderTarget.Autounattend
            ? _loc.GetString("Dialog_NoAppsSelected_Xml_Message")
            : _loc.GetString("Dialog_NoAppsSelected_Config_Message");

        return (await _dialogs.ShowConfirmationAsync(new ConfirmationRequest
        {
            Message = message,
            Title = _loc.GetString("Dialog_NoAppsSelected_Title"),
            ConfirmButtonText = _loc.GetString("Button_Yes"),
            CancelButtonText = _loc.GetString("Button_No"),
        })).Confirmed;
    }

    private string? PickDestination(BuilderTarget target) =>
        target == BuilderTarget.Autounattend
            ? _picker.PickSavePath(
                _loc.GetString("AdvancedTools_FileDialog_SaveXml"),
                "XML Files",
                "*.xml",
                AutounattendFileName,
                "xml")
            : _picker.PickSavePath(
                _loc.GetString("Config_FileDialog_SaveConfig"),
                ConfigFileConstants.FileFilter,
                ConfigFileConstants.FilePattern,
                $"Winhance_Config_{DateTime.Now:yyyyMMdd}{ConfigFileConstants.FileExtension}",
                "winhance");

    private async Task<string> WriteAsync(BuilderTarget target, SelectionSet selections, string path)
    {
        if (target == BuilderTarget.Autounattend)
            return await _autounattend.WriteAsync(selections, _selections.CurrentScope, path);

        await _configFiles.WriteAsync(selections, _selections.CurrentScope, path);
        return path;
    }

    // The XML message names the file and spells out the steps to WIMUtil; it never offers to go there, so every
    // entry point reports the same save the same way.
    private Task ReportSuccessAsync(BuilderTarget target, string path) =>
        target == BuilderTarget.Autounattend
            ? _dialogs.ShowInformationAsync(
                _loc.GetString("AdvancedTools_Msg_XmlGenSuccess", path),
                _loc.GetString("Dialog_Success"))
            : _dialogs.ShowInformationAsync(
                _loc.GetString("Config_Export_Success_Message", path),
                _loc.GetString("Config_Export_Success_Title"));
}
