using System.Xml.Linq;
using Winhance.Infrastructure.Features.Autounattend.Helpers;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

internal class WimCustomizationService : IWimCustomizationService
{
    private readonly IFileSystemService _fileSystemService;
    private readonly ILogService _logService;
    private readonly ILocalizationService _localization;
    private readonly IDriverCategorizer _driverCategorizer;
    private readonly IDismProcessRunner _dismProcessRunner;
    private readonly IDriverInstallStepWriter _driverInstallStep;
    private readonly IUpdateInstallStepWriter _updateInstallStep;

    public WimCustomizationService(
        IFileSystemService fileSystemService,
        ILogService logService,
        ILocalizationService localization,
        IDriverCategorizer driverCategorizer,
        IDismProcessRunner dismProcessRunner,
        IDriverInstallStepWriter driverInstallStep,
        IUpdateInstallStepWriter updateInstallStep)
    {
        _fileSystemService = fileSystemService;
        _logService = logService;
        _localization = localization;
        _driverCategorizer = driverCategorizer;
        _dismProcessRunner = dismProcessRunner;
        _driverInstallStep = driverInstallStep;
        _updateInstallStep = updateInstallStep;
    }

    public bool AreDriversOnMedia(string workingDirectory) => DriverStaging.HasAnyOnMedia(_fileSystemService, workingDirectory);

    public async Task<bool> AddDriversAsync(
        string workingDirectory,
        string? driverSourcePath = null,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var winpeDriverPath = DriverStaging.WinpeFolderOf(_fileSystemService, workingDirectory);
            var oemDriverPath = DriverStaging.FolderOf(_fileSystemService, workingDirectory);

            if (string.IsNullOrEmpty(driverSourcePath))
            {
                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_ExportingDrivers"),
                    TerminalOutput = "This may take several minutes"
                });

                _fileSystemService.CreateDirectory(oemDriverPath);

                // dism.exe, not the DISM API: DismGetDrivers only enumerates drivers already
                // inside a mounted image, so nothing in the API harvests them from the running
                // machine. This shell-out is permanent. It writes one complete package folder at
                // a time straight into the OEM staging folder, so the set is written once and a
                // nonzero exit keeps every package that landed.
                var arguments = $"/Online /Export-Driver /Destination:\"{oemDriverPath}\"";
                var (exitCode, _) = await _dismProcessRunner.RunProcessWithProgressAsync("dism.exe", arguments, progress, cancellationToken).ConfigureAwait(false);
                if (exitCode != 0)
                    _logService.LogWarning($"DISM Export-Driver exited with {exitCode}; keeping the packages that were exported");
            }
            else
            {
                progress?.Report(new TaskProgressDetail
                {
                    StatusText = _localization.GetString("Progress_ValidatingDrivers"),
                    TerminalOutput = driverSourcePath
                });

                if (!_fileSystemService.DirectoryExists(driverSourcePath))
                {
                    _logService.LogError($"Driver source path does not exist: {driverSourcePath}");
                    return false;
                }
            }

            progress?.Report(new TaskProgressDetail
            {
                StatusText = _localization.GetString("Progress_CategorizingDrivers"),
                TerminalOutput = "Separating storage and post-install drivers"
            });

            var copiedCount = string.IsNullOrEmpty(driverSourcePath)
                ? await Task.Run(() => _driverCategorizer.MoveStorageDrivers(oemDriverPath, winpeDriverPath), cancellationToken).ConfigureAwait(false)
                : await Task.Run(() => _driverCategorizer.CategorizeAndCopyDrivers(
                    driverSourcePath,
                    winpeDriverPath,
                    oemDriverPath,
                    workingDirectory
                ), cancellationToken).ConfigureAwait(false);

            if (copiedCount == 0)
            {
                _logService.LogWarning("No driver packages were staged");
                return false;
            }

            progress?.Report(new TaskProgressDetail
            {
                StatusText = _localization.GetString("Progress_CreatingDriverScript"),
                TerminalOutput = "Adding driver install step to autounattend.xml"
            });

            // Step 4 re-checks this; a locked or odd autounattend.xml must not fail the copy
            // that already succeeded.
            try
            {
                await _driverInstallStep.EnsureAsync(workingDirectory, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logService.LogWarning($"Could not add the driver install step to autounattend.xml: {ex.Message}");
            }

            _logService.LogInformation($"{copiedCount} driver package(s) staged - WinPE: {winpeDriverPath}, OEM: {oemDriverPath}");
            return true;
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error adding drivers: {ex.Message}", ex);
            progress?.Report(new TaskProgressDetail
            {
                StatusText = _localization.GetString("Progress_DriverAdditionFailed"),
                TerminalOutput = ex.Message
            });
            return false;
        }
    }

    public async Task<OperationResult> AddXmlToImageAsync(string xmlPath, string workingDirectory)
    {
        try
        {
            if (!_fileSystemService.FileExists(xmlPath))
            {
                var message = $"XML file not found: {xmlPath}";
                _logService.LogError(message);
                return OperationResult.Failed(message);
            }

            if (!_fileSystemService.DirectoryExists(workingDirectory))
            {
                var message = $"Working directory not found: {workingDirectory}";
                _logService.LogError(message);
                return OperationResult.Failed(message);
            }

            var destPath = _fileSystemService.CombinePath(workingDirectory, "autounattend.xml");

            MediaPayloads.CheckNotRedirected(_fileSystemService, xmlPath);
            string sourceFolder = Path.GetDirectoryName(Path.GetFullPath(xmlPath))!;
            XDocument doc;
            using (var input = _fileSystemService.OpenRead(xmlPath))
                doc = XDocument.Load(input);
            var manifest = MediaPayloads.Manifest(doc);
            if (string.Equals(Path.GetFullPath(xmlPath), Path.GetFullPath(destPath), StringComparison.OrdinalIgnoreCase))
            {
                foreach (var entry in manifest)
                    await MediaPayloads.ReadAsync(_fileSystemService, sourceFolder, entry).ConfigureAwait(false);
                _logService.LogInformation($"autounattend.xml is already in the image: {destPath}");
                await EnsureInstallStepsAsync(destPath, workingDirectory).ConfigureAwait(false);
            }
            else
            {
                var scratch = Path.Combine(workingDirectory, ".winhance-media-" + Guid.NewGuid().ToString("N"));
                var stagedXml = Path.Combine(scratch, "autounattend.xml");
                MediaPayloads.CheckNotRedirected(_fileSystemService, scratch);
                _fileSystemService.CreateDirectory(scratch);
                bool keepRecovery = false;
                try
                {
                    // Keep the original XML encoding; importing must not rewrite the user's declaration.
                    _fileSystemService.CopyFile(xmlPath, stagedXml);
                    _fileSystemService.SetFileAttributes(stagedXml, FileAttributes.Normal);
                    foreach (var entry in manifest)
                    {
                        var bytes = await MediaPayloads.ReadAsync(_fileSystemService, sourceFolder, entry).ConfigureAwait(false);
                        var path = MediaPayloads.MediaPath(scratch, (string?)entry.Attribute("path") ?? string.Empty)!;
                        _fileSystemService.CreateDirectory(Path.GetDirectoryName(path)!);
                        await _fileSystemService.WriteAllBytesAsync(path, bytes).ConfigureAwait(false);
                    }
                    await EnsureInstallStepsAsync(stagedXml, workingDirectory).ConfigureAwait(false);
                    await MediaPayloads.PublishAsync(_fileSystemService, stagedXml, destPath, workingDirectory, scratch, CancellationToken.None).ConfigureAwait(false);
                }
                catch (MediaRollbackException)
                {
                    keepRecovery = true;
                    throw;
                }
                finally
                {
                    if (!keepRecovery)
                        _fileSystemService.DeleteDirectory(scratch, recursive: true);
                }
                _logService.LogInformation($"Added autounattend.xml to image: {destPath}");
            }

            return OperationResult.Succeeded();
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error adding XML to image: {ex.Message}", ex);
            return OperationResult.Failed(ex.Message, ex);
        }
    }

    private async Task EnsureInstallStepsAsync(string xmlPath, string workingDirectory)
    {
        try
        {
            await _driverInstallStep.EnsureAsync(xmlPath, workingDirectory).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Could not add the driver install step to autounattend.xml: {ex.Message}");
        }

        try
        {
            await _updateInstallStep.EnsureAsync(xmlPath, workingDirectory).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logService.LogWarning($"Could not add the update install step to autounattend.xml: {ex.Message}");
        }
    }

    public Task<DriverInstallStepResult> EnsureDriverInstallStepAsync(string workingDirectory, CancellationToken cancellationToken = default) =>
        _driverInstallStep.EnsureAsync(workingDirectory, cancellationToken);

    public Task<DriverInstallStepResult> EnsureDriverInstallStepAsync(string xmlPath, string workingDirectory, CancellationToken cancellationToken = default) =>
        _driverInstallStep.EnsureAsync(xmlPath, workingDirectory, cancellationToken);

    public Task<UpdateInstallStepResult> EnsureUpdateInstallStepAsync(string workingDirectory, CancellationToken cancellationToken = default) =>
        _updateInstallStep.EnsureAsync(workingDirectory, cancellationToken);

    public Task<UpdateInstallStepResult> EnsureUpdateInstallStepAsync(string xmlPath, string workingDirectory, CancellationToken cancellationToken = default) =>
        _updateInstallStep.EnsureAsync(xmlPath, workingDirectory, cancellationToken);
}
