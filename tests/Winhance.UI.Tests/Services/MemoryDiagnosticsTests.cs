using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Winhance.UI.Helpers;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class MemoryDiagnosticsTests
{
    private readonly Mock<ILogService> _log = new();

    private MemoryDiagnostics CreateDiagnostics(LaunchOptions launch) =>
        new(_log.Object, launch, new Mock<IApplicationModeService>().Object, new Mock<IConfigReviewModeService>().Object);

    [Fact]
    public void Log_WithoutDiag_LogsNothing()
    {
        CreateDiagnostics(LaunchOptions.None).Log("startup");

        _log.Verify(l => l.LogInformation(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Log_WithDiag_LogsTheMemoryLine()
    {
        CreateDiagnostics(LaunchOptions.None with { Diagnostics = true }).Log("startup");

        _log.Verify(l => l.LogInformation(It.Is<string>(m => m.StartsWith("[mem-gc] startup")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void Log_WithDiag_SplitsManagedFromNative()
    {
        CreateDiagnostics(LaunchOptions.None with { Diagnostics = true }).Log("startup");

        _log.Verify(l => l.LogInformation(
            It.Is<string>(m => m.Contains(" native=") && m.Contains(" loh=") && m.Contains(" frag=") && m.EndsWith("| visual: n/a")),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void Log_WithDiag_BreaksDownTheNativePart()
    {
        CreateDiagnostics(LaunchOptions.None with { Diagnostics = true }).Log("startup");

        _log.Verify(l => l.LogInformation(
            It.Is<string>(m => m.Contains("| split: private=") && m.Contains(" heaps=") && m.Contains(" other=") && m.Contains("| gpu: ") && m.Contains("| new dlls: ")),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void Log_WithDiag_ReportsCpuSinceTheLastLine()
    {
        var diagnostics = CreateDiagnostics(LaunchOptions.None with { Diagnostics = true });

        diagnostics.Log("first");
        diagnostics.Log("second");

        _log.Verify(l => l.LogInformation(
            It.Is<string>(m => m.StartsWith("[mem-gc] second | cpu=+") && m.Contains("% of a core") && m.Contains(" total=")),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void FormatCounts_ListsTheTotalThenTheLargestTypes()
    {
        var counts = new Dictionary<string, int> { ["Grid"] = 40, ["TextBlock"] = 90, ["Border"] = 40, ["Image"] = 3 };

        Assert.Equal("total=173 TextBlock=90 Border=40 Grid=40", MemoryDiagnostics.FormatCounts(counts, 3));
    }

    [Fact]
    public void FormatCounts_WithNoElements_ReportsZero()
    {
        Assert.Equal("total=0", MemoryDiagnostics.FormatCounts(new Dictionary<string, int>(), 8));
    }

    [Fact]
    public void Log_WithDiag_CompactsTheLargeObjectHeapFirst()
    {
        // Every other 1 MB array dies, so a full GC that does not compact the large object heap leaves ~10 MB of holes.
        var arrays = Enumerable.Range(0, 20).Select(_ => new byte[1024 * 1024]).ToArray();
        for (var i = 0; i < arrays.Length; i += 2)
            arrays[i] = null!;
        string? line = null;
        _log.Setup(l => l.LogInformation(It.IsAny<string>(), It.IsAny<string>())).Callback<string, string>((m, _) => line = m);

        CreateDiagnostics(LaunchOptions.None with { Diagnostics = true }).Log("startup");

        GC.KeepAlive(arrays);
        var frag = double.Parse(System.Text.RegularExpressions.Regex.Match(line!, @" frag=([\d.]+)MB").Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(frag < 3, $"frag={frag}MB after the diagnostic GC");
    }
}
