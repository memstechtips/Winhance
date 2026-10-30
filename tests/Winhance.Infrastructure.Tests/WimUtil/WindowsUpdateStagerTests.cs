using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.Infrastructure.Features.WimUtil.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.WimUtil;

public class WindowsUpdateStagerTests : IDisposable
{
    private const string UrlBase = "https://catalog.sf.dl.delivery.mp.microsoft.com/filestreamingservice/files/x/public/";

    private static readonly byte[] CheckpointBytes = Encoding.ASCII.GetBytes("checkpoint package");
    private static readonly byte[] TargetBytes = Encoding.ASCII.GetBytes("target package, a little longer");

    private readonly string _root = Path.Combine(Path.GetTempPath(), "WinhanceStager_" + Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string, byte[]> _served = new()
    {
        ["checkpoint.msu"] = CheckpointBytes,
        ["target.msu"] = TargetBytes,
    };
    private readonly List<string> _downloaded = [];
    private readonly Mock<IDirectDownloadService> _downloads = new();
    private readonly Mock<IAuthenticodeVerifier> _signature = new();
    private readonly WindowsUpdateStager _sut;
    private readonly string _folder;

    public WindowsUpdateStagerTests()
    {
        Directory.CreateDirectory(_root);
        _folder = Path.Combine(_root, ScriptPaths.UnattendUpdatesMediaDirectory);

        _downloads
            .Setup(d => d.DownloadFileAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .Returns((string url, string folder, string _, IProgress<TaskProgressDetail>? _, CancellationToken _) =>
            {
                var name = url[(url.LastIndexOf('/') + 1)..];
                if (!_served.TryGetValue(name, out var bytes))
                {
                    return Task.FromResult<string?>(null);
                }

                _downloaded.Add(name);
                var path = Path.Combine(folder, name);
                File.WriteAllBytes(path, bytes);
                return Task.FromResult<string?>(path);
            });
        _signature.Setup(s => s.IsMicrosoftSigned(It.IsAny<string>())).Returns(true);

        var localization = new Mock<ILocalizationService>();
        localization.Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>())).Returns("Verifying");

        _sut = new WindowsUpdateStager(_downloads.Object, _signature.Object, new FileSystemService(), localization.Object, Mock.Of<ILogService>());
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static WindowsUpdateFile Entry(string role, string name, byte[] bytes, string? sha256 = null, long? size = null) =>
        new(role, "KB1", name, UrlBase + name, size ?? bytes.Length, new string('0', 40), sha256 ?? Convert.ToHexStringLower(SHA256.HashData(bytes)));

    private static WindowsUpdate Update(params WindowsUpdateFile[] files) =>
        new("KB2", "Cumulative Update", 100, new DateOnly(2026, 10, 14), "guid", files);

    private static WindowsUpdate GoodUpdate() =>
        Update(Entry("checkpoint", "checkpoint.msu", CheckpointBytes), Entry("target", "target.msu", TargetBytes));

    private string[] StagedNames() =>
        Directory.Exists(_folder) ? Directory.GetFiles(_folder, "*.msu").Select(Path.GetFileName).OfType<string>().Order().ToArray() : [];

    [Fact]
    public async Task StageAsync_GoodUpdate_StagesBothFiles()
    {
        var result = await _sut.StageAsync(_root, GoodUpdate());

        result.Should().Be(UpdateStageResult.Staged);
        StagedNames().Should().Equal("checkpoint.msu", "target.msu");
        File.ReadAllText(_folder + UpdateStaging.TargetRecordSuffix).Should().Be("target.msu");
        _sut.IsStaged(_root).Should().BeTrue();
    }

    [Fact]
    public void IsStaged_MsuFilesWithNoTargetRecord_IsFalse()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllBytes(Path.Combine(_folder, "target.msu"), TargetBytes);

        _sut.IsStaged(_root).Should().BeFalse();
    }

    [Fact]
    public async Task RemoveStaged_AfterStaging_DeletesTheFolderAndTheRecord()
    {
        await _sut.StageAsync(_root, GoodUpdate());

        _sut.RemoveStaged(_root);

        Directory.Exists(_folder).Should().BeFalse();
        File.Exists(_folder + UpdateStaging.TargetRecordSuffix).Should().BeFalse();
        _sut.IsStaged(_root).Should().BeFalse();
    }

    [Fact]
    public async Task StageAsync_SizeMismatch_ReturnsHashMismatchAndLeavesNothing()
    {
        var update = Update(Entry("target", "target.msu", TargetBytes, size: TargetBytes.Length + 1));

        var result = await _sut.StageAsync(_root, update);

        result.Should().Be(UpdateStageResult.HashMismatch);
        StagedNames().Should().BeEmpty();
        _sut.IsStaged(_root).Should().BeFalse();
    }

    [Fact]
    public async Task StageAsync_HashMismatch_ReturnsHashMismatchAndLeavesNothing()
    {
        var update = Update(
            Entry("checkpoint", "checkpoint.msu", CheckpointBytes),
            Entry("target", "target.msu", TargetBytes, sha256: new string('a', 64)));

        var result = await _sut.StageAsync(_root, update);

        result.Should().Be(UpdateStageResult.HashMismatch);
        StagedNames().Should().BeEmpty();
    }

    [Fact]
    public async Task StageAsync_SignatureFails_ReturnsSignatureInvalidAndLeavesNothing()
    {
        _signature.Setup(s => s.IsMicrosoftSigned(It.Is<string>(p => p.EndsWith("target.msu")))).Returns(false);

        var result = await _sut.StageAsync(_root, GoodUpdate());

        result.Should().Be(UpdateStageResult.SignatureInvalid);
        StagedNames().Should().BeEmpty();
    }

    [Fact]
    public async Task StageAsync_DownloadReturnsNull_ReturnsDownloadFailed()
    {
        _served.Remove("target.msu");

        var result = await _sut.StageAsync(_root, GoodUpdate());

        result.Should().Be(UpdateStageResult.DownloadFailed);
        StagedNames().Should().BeEmpty();
    }

    [Fact]
    public async Task StageAsync_FileAlreadyStagedAndCorrect_IsNotDownloadedAgain()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllBytes(Path.Combine(_folder, "checkpoint.msu"), CheckpointBytes);

        var result = await _sut.StageAsync(_root, GoodUpdate());

        result.Should().Be(UpdateStageResult.Staged);
        _downloaded.Should().Equal("target.msu");
    }

    [Fact]
    public async Task StageAsync_StrayMsu_IsRemoved()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "old-update.msu"), "left over");

        var result = await _sut.StageAsync(_root, GoodUpdate());

        result.Should().Be(UpdateStageResult.Staged);
        StagedNames().Should().Equal("checkpoint.msu", "target.msu");
    }

    [Fact]
    public async Task StageAsync_Cancelled_RethrowsAndLeavesNothing()
    {
        using var cts = new CancellationTokenSource();
        _signature.Setup(s => s.IsMicrosoftSigned(It.IsAny<string>())).Returns(() =>
        {
            cts.Cancel();
            return true;
        });

        var act = () => _sut.StageAsync(_root, GoodUpdate(), cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        StagedNames().Should().BeEmpty();
    }

    [Fact]
    public void RemoveStaged_MissingFolder_DoesNothing()
    {
        var act = () => _sut.RemoveStaged(_root);

        act.Should().NotThrow();
    }
}
