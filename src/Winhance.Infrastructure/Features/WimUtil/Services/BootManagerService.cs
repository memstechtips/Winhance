using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

internal sealed class BootManagerService(
    IWimFileExtractor extractor,
    ISecureBootDatabase secureBootDatabase,
    IFileSystemService fileSystemService,
    ILogService logService) : IBootManagerService
{
    // The newest boot manager security version on Microsoft media, read from Windows 11 25H2 on 2026-10-05.
    // An updated PC stores the SVN it enforces in its firmware and refuses any boot manager below it.
    internal const uint NewestSecurityVersion = 7u << 16;

    // Both of boot.wim's images carry the 2023 files; Make2023BootableMedia.ps1 reads the first.
    private const int BootImageIndex = 1;

    private const string BootManagerPath = @"efi\boot\bootx64.efi";
    private const string Ca2023BootManager = @"\Windows\Boot\EFI_EX\bootmgfw_EX.efi";
    private const string FontsFolder = @"efi\microsoft\boot\fonts";
    private const string StagingFolderName = "BootManager2023";
    private const string ProbeFolderName = "BootManagerProbe";

    // What Make2023BootableMedia.ps1 replaces, taken from boot.wim as it does: the media carries its own
    // 2023 files, so the PC running Winhance needs no update. Keyed by the path on the media.
    private static readonly Dictionary<string, string> Ca2023Files = new(StringComparer.OrdinalIgnoreCase)
    {
        [BootManagerPath] = Ca2023BootManager,
        [@"bootmgr.efi"] = @"\Windows\Boot\EFI_EX\bootmgr_EX.efi",
        [@"efi\microsoft\boot\efisys.bin"] = @"\Windows\Boot\DVD_EX\EFI\en-US\efisys_EX.bin",
    };

    public async Task<BootManagerOptions> InspectAsync(string workingDirectory, CancellationToken cancellationToken)
    {
        var bootManager = fileSystemService.CombinePath(workingDirectory, BootManagerPath);
        if (!fileSystemService.FileExists(bootManager))
        {
            logService.LogInformation($"No {BootManagerPath} in {workingDirectory}; nothing to choose");
            return new BootManagerOptions(false, BootManager.Pca2011, false);
        }

        var current = BootManagerFile.Read(await fileSystemService.ReadAllBytesAsync(bootManager, cancellationToken).ConfigureAwait(false));
        var offers2023 = !current.SignedByCa2023
            && await Task.Run(() => HasCa2023BootManager(workingDirectory), cancellationToken).ConfigureAwait(false);
        var revokes2011 = await Task.Run(ReadRevocation, cancellationToken).ConfigureAwait(false);
        var outdated = current.SecurityVersion < NewestSecurityVersion;

        logService.LogInformation(
            $"Boot manager signed by {(current.SignedByCa2023 ? "Windows UEFI CA 2023" : "PCA 2011")}, SVN {FormatVersion(current.SecurityVersion)}; "
            + $"boot.wim offers 2023: {offers2023}; this PC revokes PCA 2011: {revokes2011?.ToString() ?? "unknown"}");

        // 2011 stays the default while it boots on more PCs than 2023 does. A PC that already refuses it is
        // the one the user is most likely reinstalling, so there the stick has to carry 2023.
        var preferred = offers2023 && revokes2011 == true ? BootManager.Ca2023 : BootManager.Pca2011;
        return new BootManagerOptions(offers2023, preferred, outdated);
    }

    public async Task<BootFileReplacements> PrepareAsync(string workingDirectory, BootManager bootManager, CancellationToken cancellationToken)
    {
        if (bootManager == BootManager.Pca2011)
        {
            return BootFileReplacements.None;
        }

        var staging = EmptyTempFolder(StagingFolderName);
        var mediaPathByImagePath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (mediaPath, imagePath) in Ca2023Files)
        {
            if (fileSystemService.FileExists(fileSystemService.CombinePath(workingDirectory, mediaPath)))
                mediaPathByImagePath[imagePath] = mediaPath;
        }

        // The 2023 boot manager draws its text with its own fonts, named with an _EX suffix inside boot.wim.
        var fonts = fileSystemService.CombinePath(workingDirectory, FontsFolder);
        if (fileSystemService.DirectoryExists(fonts))
        {
            foreach (var font in fileSystemService.GetFiles(fonts, "*.ttf"))
            {
                var name = fileSystemService.GetFileNameWithoutExtension(font);
                mediaPathByImagePath[$@"\Windows\Boot\Fonts_EX\{name}_EX.ttf"] = $@"{FontsFolder}\{name}.ttf";
            }
        }

        var destinationByImagePath = mediaPathByImagePath.ToDictionary(
            entry => entry.Key,
            entry => fileSystemService.CombinePath(staging, entry.Value),
            StringComparer.OrdinalIgnoreCase);
        var bootWim = fileSystemService.CombinePath(workingDirectory, "sources", "boot.wim");
        var extracted = await Task.Run(() => extractor.Extract(bootWim, BootImageIndex, destinationByImagePath), cancellationToken).ConfigureAwait(false);

        if (!extracted.Contains(Ca2023BootManager, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{bootWim} has no 2023-signed boot manager.");
        }

        logService.LogInformation($"Staged {extracted.Count} 2023 boot file(s) in {staging}");
        return new BootFileReplacements(extracted.ToDictionary(
            imagePath => mediaPathByImagePath[imagePath],
            imagePath => destinationByImagePath[imagePath],
            StringComparer.OrdinalIgnoreCase));
    }

    private bool HasCa2023BootManager(string workingDirectory)
    {
        var probe = fileSystemService.CombinePath(EmptyTempFolder(ProbeFolderName), "bootmgfw_EX.efi");
        var bootWim = fileSystemService.CombinePath(workingDirectory, "sources", "boot.wim");
        return fileSystemService.FileExists(bootWim)
            && extractor.Extract(bootWim, BootImageIndex, new Dictionary<string, string> { [Ca2023BootManager] = probe }).Count == 1;
    }

    // Outside the working folder, or the copy would carry it onto the media. Emptied first so a file a
    // previous run staged can never ride along. The probe has a folder of its own, so asking what the
    // media offers leaves the files a BootFileReplacements points at alone.
    private string EmptyTempFolder(string name)
    {
        var folder = fileSystemService.CombinePath(fileSystemService.GetTempPath(), "Winhance", name);
        if (fileSystemService.DirectoryExists(folder))
            fileSystemService.DeleteDirectory(folder, recursive: true);

        fileSystemService.CreateDirectory(folder);
        return folder;
    }

    // The choice still works without it; only the smart default is lost.
    private bool? ReadRevocation()
    {
        try
        {
            return secureBootDatabase.RevokesPca2011();
        }
        catch (Exception ex)
        {
            logService.LogWarning($"Could not read the Secure Boot database: {ex.Message}");
            return null;
        }
    }

    private static string FormatVersion(uint? version) =>
        version is { } v ? $"{v >> 16}.{v & 0xFFFF}" : "none";
}
