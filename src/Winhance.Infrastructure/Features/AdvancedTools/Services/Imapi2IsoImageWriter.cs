using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.AdvancedTools.Services;

internal sealed class Imapi2IsoImageWriter : IIsoImageWriter
{
    // oscdimg -udfver102. Measured supported on the target machine alongside 0x150, 0x200,
    // 0x201 and 0x250; 1.02 is what Windows installation media ships.
    private const int Udf102 = 0x102;

    private const int RequiredBootEntries = 2;

    private const string VolumeLabel = "WINHANCE";

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
        IProgress<TaskProgressDetail>? progress,
        CancellationToken cancellationToken)
    {
        var biosBootImage = _fileSystemService.CombinePath(workingDirectory, "boot", "etfsboot.com");
        var uefiBootImage = _fileSystemService.CombinePath(workingDirectory, "efi", "microsoft", "boot", "efisys.bin");

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
        // for an ISO this size.
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

        // False, never true: true nests the whole tree under a folder named after the working
        // directory, which builds cleanly and then does not boot.
        image.AddTree(workingDirectory, false);

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
}
