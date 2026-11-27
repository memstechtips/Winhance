using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.WimUtil.Models;

namespace Winhance.Infrastructure.Features.WimUtil.Services;

internal sealed class Imapi2IsoImageWriter : IIsoImageWriter
{
    // oscdimg -udfver102. Measured supported on the target machine alongside 0x150, 0x200,
    // 0x201 and 0x250; 1.02 is what Windows installation media ships.
    private const int Udf102 = 0x102;

    private const int RequiredBootEntries = 2;

    private const string VolumeLabel = "WINHANCE";

    private const string UefiBootImage = @"efi\microsoft\boot\efisys.bin";

    private readonly Func<IFileSystemImageWrapper> _imageFactory;
    private readonly IFileSystemService _fileSystemService;
    private readonly ILocalizationService _localization;
    private readonly ILogService _logService;
    private readonly TimeProvider _timeProvider;

    public Imapi2IsoImageWriter(
        Func<IFileSystemImageWrapper> imageFactory,
        IFileSystemService fileSystemService,
        ILocalizationService localization,
        ILogService logService,
        TimeProvider timeProvider)
    {
        _imageFactory = imageFactory;
        _fileSystemService = fileSystemService;
        _localization = localization;
        _logService = logService;
        _timeProvider = timeProvider;
    }

    public void Write(
        string workingDirectory,
        string outputPath,
        BootFileReplacements replacements,
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        var biosBootImage = _fileSystemService.CombinePath(workingDirectory, "boot", "etfsboot.com");

        // efisys.bin is a small FAT image holding its own copy of the UEFI boot manager, and it is what a
        // PC boots from the ISO, so a 2023 boot manager has to arrive through it.
        var uefiBootImage = replacements.Files.TryGetValue(UefiBootImage, out var replacedBootImage)
            ? replacedBootImage
            : _fileSystemService.CombinePath(workingDirectory, UefiBootImage);

        // Cheaper and clearer than letting IMAPI2 fail on a missing stream later.
        if (!_fileSystemService.FileExists(biosBootImage))
            throw new FileNotFoundException($"Boot file not found: {biosBootImage}");

        if (!_fileSystemService.FileExists(uefiBootImage))
            throw new FileNotFoundException($"UEFI boot file not found: {uefiBootImage}");

        using var image = _imageFactory();

        // An untouched IFileSystemImage caps itself at 332,800 blocks - a 650 MB disc - and a
        // multi-GB tree then fails with IMAPI_E_IMAGE_TOO_BIG. Clearing it has to happen before
        // the tree goes in, and ChooseImageDefaults* would re-impose a cap, so neither is called.
        image.FreeMediaBlocks = 0;
        image.FileSystemsToCreate = IsoFileSystems.Udf;
        image.UdfRevision = Udf102;
        image.VolumeName = VolumeLabel;

        // True would copy the whole payload to a temp location first - another 8 GB of scratch
        // for an ISO this size. False alone is not enough: see AddWorkingTree.
        image.StageFiles = false;

        image.SetBootImageOptions(
        [
            new BootEntry(BootPlatform.BiosX86, biosBootImage),
            new BootEntry(BootPlatform.Uefi, uefiBootImage),
        ]);

        // An assignment that does not throw is not proof it took: the shape IMAPI2 rejects comes
        // back as E_NOINTERFACE from the put, but a partially-accepted array would not.
        var assigned = image.BootImageEntryCount;
        if (assigned != RequiredBootEntries)
        {
            throw new InvalidOperationException(
                $"IMAPI2 accepted the boot catalog but reads back {assigned} entries, not {RequiredBootEntries}.");
        }

        AddWorkingTree(image, workingDirectory, replacements);

        using var result = image.CreateResultImage();
        _logService.LogInformation($"IMAPI2 built a {result.TotalBytes:N0} byte image for {outputPath}");

        var reporter = new ByteProgressReporter(
            progress,
            new TransferRate(_timeProvider),
            percent => _localization.GetString("Progress_WritingIsoPercent", percent.ToString()));
        var outputName = _fileSystemService.GetFileName(outputPath);
        result.WriteTo(
            outputPath,
            (written, total) => reporter.Report(written, total, outputName),
            cancellationToken);
    }

    // File by file, never AddTree: IMAPI2 stages every file it opens from a path on a USB or network drive,
    // whatever StageFiles says (documented on put_StageFiles, measured 2026-10-05 as a full copy of
    // install.wim in %TEMP%). A stream it is handed is read in place while the result image is written.
    private void AddWorkingTree(IFileSystemImageWrapper image, string workingDirectory, BootFileReplacements replacements)
    {
        // Ordinal order puts every folder after its parent.
        var directories = _fileSystemService.GetDirectories(workingDirectory, "*", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in directories)
        {
            image.AddDirectory(@"\" + Path.GetRelativePath(workingDirectory, directory));
        }

        foreach (var file in _fileSystemService.GetFiles(workingDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(workingDirectory, file);
            var source = replacements.Files.TryGetValue(relativePath, out var replacement) ? replacement : file;
            image.AddFile(@"\" + relativePath, source);
        }
    }
}
