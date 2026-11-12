using System.ComponentModel;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.WimUtil.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.WimUtil.WimServices;

public class OfflineHiveLoaderTests
{
    private const string Mount = @"D:\iso\WinhanceMount";

    private readonly Mock<IRegistryHiveNative> _hives = new();
    private readonly Mock<IFileSystemService> _files = new();
    private readonly Mock<ILogService> _log = new();
    private readonly List<string> _calls = [];
    private Func<string, int> _exit = _ => 0;

    public OfflineHiveLoaderTests()
    {
        _files.Setup(f => f.CombinePath(It.IsAny<string[]>())).Returns((string[] parts) => string.Join("\\", parts));
        _files.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
        _hives.Setup(h => h.Load(It.IsAny<string>(), It.IsAny<string>())).Returns((string name, string file) => Record($"load {name} {file}"));
        _hives.Setup(h => h.Unload(It.IsAny<string>())).Returns((string name) => Record($"unload {name}"));
    }

    private int Record(string call)
    {
        _calls.Add(call);
        return _exit(call);
    }

    private OfflineHiveLoader Loader() => new(_hives.Object, _files.Object, _log.Object) { RetryDelay = TimeSpan.Zero };

    [Fact]
    public async Task LoadAsync_LoadsSoftwareSystemAndDefaultUserUnderUniqueNames()
    {
        await using var session = await Loader().LoadAsync(Mount, CancellationToken.None);

        _calls.Should().HaveCount(3);
        _calls[0].Should().StartWith("load WH_SOFTWARE_").And.EndWith($" {Mount}\\Windows\\System32\\config\\SOFTWARE");
        _calls[1].Should().StartWith("load WH_SYSTEM_").And.EndWith($" {Mount}\\Windows\\System32\\config\\SYSTEM");
        _calls[2].Should().StartWith("load WH_NTUSER_").And.EndWith($" {Mount}\\Users\\Default\\NTUSER.DAT");
        session.SoftwareRoot.Should().StartWith(@"HKEY_LOCAL_MACHINE\WH_SOFTWARE_");
        session.SystemRoot.Should().StartWith(@"HKEY_LOCAL_MACHINE\WH_SYSTEM_");
        session.DefaultUserRoot.Should().StartWith(@"HKEY_LOCAL_MACHINE\WH_NTUSER_");
    }

    [Fact]
    public async Task LoadAsync_TwoSessionsDoNotShareNames()
    {
        await using var first = await Loader().LoadAsync(Mount, CancellationToken.None);
        await using var second = await Loader().LoadAsync(Mount, CancellationToken.None);

        first.SoftwareRoot.Should().NotBe(second.SoftwareRoot);
    }

    [Fact]
    public async Task DisposeAsync_UnloadsEveryLoadedHiveOnce()
    {
        var session = await Loader().LoadAsync(Mount, CancellationToken.None);
        _calls.Clear();

        await session.DisposeAsync();
        await session.DisposeAsync();

        _calls.Should().HaveCount(3).And.OnlyContain(c => c.StartsWith("unload WH_"));
        _calls.Select(c => c.Split('_')[1]).Should().Equal("NTUSER", "SYSTEM", "SOFTWARE");
    }

    [Fact]
    public async Task DisposeAsync_RetriesAnUnloadThatIsRefused()
    {
        var session = await Loader().LoadAsync(Mount, CancellationToken.None);
        _calls.Clear();
        var refusals = 0;
        _exit = args => args.StartsWith("unload") && args.Contains("WH_NTUSER") && refusals++ < 2 ? 32 : 0;

        await session.DisposeAsync();

        _calls.Count(c => c.Contains("WH_NTUSER")).Should().Be(3);
        _calls.Count(c => c.Contains("WH_SOFTWARE")).Should().Be(1);
    }

    [Fact]
    public async Task LoadAsync_WhenAHiveWillNotLoad_UnloadsTheOnesAlreadyLoadedAndThrows()
    {
        _exit = args => args.StartsWith("load") && args.Contains("WH_SYSTEM") ? 5 : 0;

        var act = () => Loader().LoadAsync(Mount, CancellationToken.None);

        (await act.Should().ThrowAsync<Win32Exception>().WithMessage("*config\\SYSTEM*")).Which.NativeErrorCode.Should().Be(5);
        _calls.Should().Contain(c => c.StartsWith("unload") && c.Contains("WH_SOFTWARE"));
        _calls.Should().NotContain(c => c.StartsWith("unload") && c.Contains("WH_SYSTEM"));
    }

    [Fact]
    public async Task LoadAsync_WhenTheImageHasNoHive_ThrowsWithoutLoadingAnything()
    {
        _files.Setup(f => f.FileExists(It.Is<string>(p => p.EndsWith("SOFTWARE")))).Returns(false);

        var act = () => Loader().LoadAsync(Mount, CancellationToken.None);

        await act.Should().ThrowAsync<FileNotFoundException>();
        _calls.Should().BeEmpty();
    }

    [Fact]
    public async Task DisposeAsync_WhenAnUnloadThrows_StillUnloadsTheRemainingHives()
    {
        var session = await Loader().LoadAsync(Mount, CancellationToken.None);
        _calls.Clear();
        _hives.Setup(h => h.Unload(It.Is<string>(n => n.Contains("WH_NTUSER")))).Throws(new InvalidOperationException("boom"));

        await session.DisposeAsync();

        _calls.Should().Contain(c => c.Contains("WH_SYSTEM")).And.Contain(c => c.Contains("WH_SOFTWARE"));
        _log.Verify(l => l.LogWarning(It.Is<string>(m => m.Contains("WH_NTUSER")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_WhenAHiveNeverUnloads_StopsAfterFiveTriesAndWarns()
    {
        var session = await Loader().LoadAsync(Mount, CancellationToken.None);
        _calls.Clear();
        _exit = args => args.StartsWith("unload") && args.Contains("WH_NTUSER") ? 32 : 0;

        await session.DisposeAsync();

        _calls.Count(c => c.Contains("WH_NTUSER")).Should().Be(5);
        _log.Verify(l => l.LogWarning(It.Is<string>(m => m.Contains("WH_NTUSER")), It.IsAny<string>()), Times.Once);
    }
}
