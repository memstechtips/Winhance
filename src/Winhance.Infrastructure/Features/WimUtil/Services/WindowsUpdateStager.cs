using System.Security.Cryptography;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

internal sealed class WindowsUpdateStager(
    IDirectDownloadService downloads,
    IAuthenticodeVerifier authenticode,
    IFileSystemService files,
    ILocalizationService localization,
    ILogService log) : IWindowsUpdateStager
{
    private const int HashBufferBytes = 1024 * 1024;

    public async Task<UpdateStageResult> StageAsync(
        string workingDirectory,
        WindowsUpdate update,
        IProgress<TaskProgressDetail>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var folder = UpdateStaging.FolderOf(files, workingDirectory);
        files.CreateDirectory(folder);

        var wanted = update.Files.Select(f => f.FileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var stray in files.GetFiles(folder, "*.msu").Where(p => !wanted.Contains(files.GetFileName(p))))
        {
            log.LogInformation($"Removing {files.GetFileName(stray)} from the update staging folder: it is not part of {update.Kb}");
            files.DeleteFile(stray);
        }

        try
        {
            foreach (var file in update.Files)
            {
                var path = files.CombinePath(folder, file.FileName);

                if (files.FileExists(path) && await HasExpectedContentAsync(path, file, progress, cancellationToken).ConfigureAwait(false))
                {
                    log.LogInformation($"{file.FileName} is already staged and matches the list, skipping the download");
                }
                else
                {
                    var downloaded = await downloads.DownloadFileAsync(file.Url, folder, file.Kb, progress, cancellationToken).ConfigureAwait(false);
                    if (downloaded is null)
                    {
                        return Fail(workingDirectory, UpdateStageResult.DownloadFailed, $"Downloading {file.FileName} failed");
                    }

                    path = downloaded;
                    if (!await HasExpectedContentAsync(path, file, progress, cancellationToken).ConfigureAwait(false))
                    {
                        return Fail(workingDirectory, UpdateStageResult.HashMismatch, $"{file.FileName} does not match the list's size or SHA256");
                    }
                }

                if (!await Task.Run(() => authenticode.IsMicrosoftSigned(path), cancellationToken).ConfigureAwait(false))
                {
                    return Fail(workingDirectory, UpdateStageResult.SignatureInvalid, $"{file.FileName} is not signed by Microsoft");
                }
            }
        }
        catch (OperationCanceledException)
        {
            RemoveStaged(workingDirectory);
            throw;
        }

        // The validator puts the list's one target entry last.
        files.WriteAllText(UpdateStaging.TargetRecordOf(files, workingDirectory), update.Files[^1].FileName);
        log.LogInformation($"{update.Kb} is staged in {folder}");
        return UpdateStageResult.Staged;
    }

    public bool IsStaged(string workingDirectory) => UpdateStaging.IsStaged(files, workingDirectory);

    public void RemoveStaged(string workingDirectory)
    {
        var record = UpdateStaging.TargetRecordOf(files, workingDirectory);
        if (files.FileExists(record))
        {
            files.DeleteFile(record);
        }

        var folder = UpdateStaging.FolderOf(files, workingDirectory);
        if (files.DirectoryExists(folder))
        {
            files.DeleteDirectory(folder, recursive: true);
        }
    }

    private UpdateStageResult Fail(string workingDirectory, UpdateStageResult result, string reason)
    {
        log.LogWarning($"{reason}; removing the staged update files");
        RemoveStaged(workingDirectory);
        return result;
    }

    private async Task<bool> HasExpectedContentAsync(
        string path,
        WindowsUpdateFile file,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        if (files.GetFileSize(path) != file.Size)
        {
            return false;
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, HashBufferBytes, FileOptions.Asynchronous | FileOptions.SequentialScan);

        var buffer = new byte[HashBufferBytes];
        var status = localization.GetString("Progress_VerifyingUpdate", file.Kb);
        long done = 0;
        var lastPercent = -1;
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            hash.AppendData(buffer, 0, read);
            done += read;

            var percent = (int)(done * 100 / Math.Max(file.Size, 1));
            if (percent != lastPercent)
            {
                lastPercent = percent;
                progress?.Report(new TaskProgressDetail
                {
                    Progress = percent,
                    StatusText = status,
                    IsActive = true,
                    IsIndeterminate = false
                });
            }
        }

        return string.Equals(Convert.ToHexStringLower(hash.GetHashAndReset()), file.Sha256, StringComparison.OrdinalIgnoreCase);
    }
}
