using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.AdvancedTools.Models;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.AdvancedTools.Services;

internal class WimCustomizationService : IWimCustomizationService
{
    private readonly IFileSystemService _fileSystemService;
    private readonly ILogService _logService;
    private readonly HttpClient _httpClient;
    private readonly ILocalizationService _localization;
    private readonly IDriverCategorizer _driverCategorizer;
    private readonly IDismProcessRunner _dismProcessRunner;
    private readonly IDriverInstallStepWriter _driverInstallStep;

    private const string UnattendedWinstallXmlUrl = "https://raw.githubusercontent.com/memstechtips/UnattendedWinstall/main/autounattend.xml";

    public WimCustomizationService(
        IFileSystemService fileSystemService,
        ILogService logService,
        HttpClient httpClient,
        ILocalizationService localization,
        IDriverCategorizer driverCategorizer,
        IDismProcessRunner dismProcessRunner,
        IDriverInstallStepWriter driverInstallStep)
    {
        _fileSystemService = fileSystemService;
        _logService = logService;
        _httpClient = httpClient;
        _localization = localization;
        _driverCategorizer = driverCategorizer;
        _dismProcessRunner = dismProcessRunner;
        _driverInstallStep = driverInstallStep;
    }

    public async Task<bool> AddDriversAsync(
        string workingDirectory,
        string? driverSourcePath = null,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // The media ROOT, not sources\. KB 2686316 publishes the paths Setup actually scans and
            // every one is a drive root (C:\$WinPEDriver$, D:\, E:\, X:\) - Setup enumerates drive
            // letters and probes each root. Nothing documents a sources\ variant, so the storage
            // drivers sat somewhere Setup never looked.
            var winpeDriverPath = _fileSystemService.CombinePath(workingDirectory, "$WinpeDriver$");
            var oemDriverPath = _fileSystemService.CombinePath(workingDirectory, "sources", "$OEM$", "$$", "Drivers");

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

    public async Task<bool> AddXmlToImageAsync(string xmlPath, string workingDirectory)
    {
        try
        {
            if (!_fileSystemService.FileExists(xmlPath))
            {
                _logService.LogError($"XML file not found: {xmlPath}");
                return false;
            }

            if (!_fileSystemService.DirectoryExists(workingDirectory))
            {
                _logService.LogError($"Working directory not found: {workingDirectory}");
                return false;
            }

            var destPath = _fileSystemService.CombinePath(workingDirectory, "autounattend.xml");

            // A byte copy, not a read-then-write: the user's file may declare windows-1252 or
            // utf-16, and writing the decoded string back as UTF-8 leaves that declaration lying
            // about the bytes underneath it.
            _fileSystemService.CopyFile(xmlPath, destPath, overwrite: true);

            _logService.LogInformation($"Added autounattend.xml to image: {destPath}");
            try
            {
                await _driverInstallStep.EnsureAsync(workingDirectory).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Could not add the driver install step to autounattend.xml: {ex.Message}");
            }
            return true;
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error adding XML to image: {ex.Message}", ex);
            return false;
        }
    }

    public async Task<string> DownloadUnattendedWinstallXmlAsync(
        string destinationPath,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(destinationPath))
            throw new ArgumentException("Destination path cannot be empty.", nameof(destinationPath));

        var destinationDir = _fileSystemService.GetDirectoryName(destinationPath);
        if (string.IsNullOrEmpty(destinationDir))
            throw new ArgumentException("Destination path must include a directory.", nameof(destinationPath));

        try
        {
            progress?.Report(new TaskProgressDetail
            {
                StatusText = _localization.GetString("Progress_DownloadingXml"),
                TerminalOutput = UnattendedWinstallXmlUrl
            });

            var xmlContent = await _httpClient.GetStringAsync(UnattendedWinstallXmlUrl, cancellationToken).ConfigureAwait(false);

            _fileSystemService.CreateDirectory(destinationDir);
            await _fileSystemService.WriteAllTextAsync(destinationPath, xmlContent, cancellationToken).ConfigureAwait(false);

            progress?.Report(new TaskProgressDetail
            {
                StatusText = _localization.GetString("Progress_XmlDownloaded"),
                TerminalOutput = $"Saved to: {destinationPath}"
            });

            _logService.LogInformation($"Downloaded UnattendedWinstall XML to: {destinationPath}");
            return destinationPath;
        }
        catch (Exception ex)
        {
            _logService.LogError($"Error downloading UnattendedWinstall XML: {ex.Message}", ex);
            throw;
        }
    }

    public Task<DriverInstallStepResult> EnsureDriverInstallStepAsync(string workingDirectory, CancellationToken cancellationToken = default) =>
        _driverInstallStep.EnsureAsync(workingDirectory, cancellationToken);
}
