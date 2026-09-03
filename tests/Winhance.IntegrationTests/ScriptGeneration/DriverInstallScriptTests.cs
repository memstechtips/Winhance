using FluentAssertions;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.Infrastructure.Features.Common.Utilities;
using Xunit;

namespace Winhance.IntegrationTests.ScriptGeneration;

// Executes the real driver-install script on Windows PowerShell over a temp driver tree, with
// pnputil replaced by a function that returns the exit code chosen per package folder. Pins the
// cleanup rule: a package folder goes only when every INF in it installed, the root only when
// nothing failed, and the script never fails Setup.
[Trait("Category", "Integration")]
public class DriverInstallScriptTests
{
    private sealed class DriverTree : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "winhance-drivers-" + Guid.NewGuid().ToString("N"));
        public string Log { get; } = Path.Combine(Path.GetTempPath(), "winhance-drivers-" + Guid.NewGuid().ToString("N") + ".log");

        public string Package(string name, params string[] infNames)
        {
            var folder = Path.Combine(Root, name);
            Directory.CreateDirectory(folder);
            foreach (var inf in infNames)
            {
                File.WriteAllText(Path.Combine(folder, inf), "[Version]");
                File.WriteAllText(Path.Combine(folder, Path.ChangeExtension(inf, ".sys")), "bin");
            }

            return folder;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
            if (File.Exists(Log))
                File.Delete(Log);
        }
    }

    // exitByFolder maps a package folder name to the exit code the fake pnputil returns for
    // every INF inside it.
    private static async Task<string> RunAsync(DriverTree tree, IReadOnlyDictionary<string, int> exitByFolder)
    {
        var script = DriverInstallStepWriter.InstallScript;
        var redirected = script
            .Replace("'C:\\ProgramData\\Winhance\\Unattend\\Logs\\Winhance-DriverInstall.log'", "'" + tree.Log + "'", StringComparison.Ordinal)
            .Replace("'C:\\Windows\\Drivers'", "'" + tree.Root + "'", StringComparison.Ordinal);
        redirected.Should().Contain(tree.Log).And.Contain(tree.Root, because: "the script's two path constants must still read exactly as the test expects");

        var table = string.Join("; ", exitByFolder.Select(kv => $"'{kv.Key}' = {kv.Value}"));
        var fake = "$exitByFolder = @{ " + table + " }\n" +
                   "function pnputil { param([Parameter(ValueFromRemainingArguments = $true)] $a)\n" +
                   "  $folder = Split-Path (Split-Path $a[1] -Parent) -Leaf\n" +
                   "  $global:LASTEXITCODE = $exitByFolder[$folder]\n" +
                   "  \"fake pnputil $($a -join ' ')\" }\n";

        return await new PowerShellRunner(new FileSystemService()).RunScriptAsync(fake + redirected.ReplaceLineEndings("\n").Trim() + "\n");
    }

    [Fact]
    public async Task AllPackagesInstall_RemovesEveryFolderAndTheRoot()
    {
        using var tree = new DriverTree();
        tree.Package("Audio", "hdaudio.inf");
        tree.Package("Net", "netwtw10.inf", "netwtw08.inf");
        var chipset = tree.Package("Chipset", "chipset.inf");
        File.SetAttributes(Path.Combine(chipset, "chipset.inf"), FileAttributes.Hidden);

        await RunAsync(tree, new Dictionary<string, int> { ["Audio"] = 0, ["Net"] = 3010, ["Chipset"] = 259 });

        Directory.Exists(tree.Root).Should().BeFalse();
        var log = File.ReadAllText(tree.Log);
        log.Should().Contain("3 driver package(s), 4 INF(s)");
        log.Should().Contain("ExitCode 0 for hdaudio.inf");
        log.Should().Contain("ExitCode 3010 for netwtw10.inf");
        log.Should().Contain("ExitCode 3010 for netwtw08.inf");
        log.Should().Contain("ExitCode 259 for chipset.inf");
        log.Should().Contain("Done - 0 failed package(s) kept");
    }

    [Fact]
    public async Task OnePackageFails_KeepsOnlyThatFolderAndTheRoot()
    {
        using var tree = new DriverTree();
        var net = tree.Package("Net", "netwtw10.inf", "netwtw08.inf");
        var audio = tree.Package("Audio", "hdaudio.inf");

        await RunAsync(tree, new Dictionary<string, int> { ["Net"] = 1, ["Audio"] = 0 });

        Directory.Exists(tree.Root).Should().BeTrue();
        Directory.Exists(audio).Should().BeFalse();
        Directory.GetFiles(net).Select(Path.GetFileName).Should().BeEquivalentTo("netwtw10.inf", "netwtw10.sys", "netwtw08.inf", "netwtw08.sys");
        var log = File.ReadAllText(tree.Log);
        log.Should().Contain("2 driver package(s), 3 INF(s)");
        log.Should().Contain("ExitCode 1 for netwtw10.inf");
        log.Should().Contain("Done - 1 failed package(s) kept");
    }

    [Fact]
    public async Task PayloadSubfolder_FailedPackageKeepsItsWholeTree()
    {
        using var tree = new DriverTree();
        var heci = tree.Package("Heci", "heci.inf");
        Directory.CreateDirectory(Path.Combine(heci, "x64"));
        File.WriteAllText(Path.Combine(heci, "x64", "heci.sys"), "bin");
        tree.Package("Audio", "hdaudio.inf");

        await RunAsync(tree, new Dictionary<string, int> { ["Heci"] = 2, ["Audio"] = 0 });

        File.Exists(Path.Combine(heci, "x64", "heci.sys")).Should().BeTrue();
        Directory.Exists(Path.Combine(tree.Root, "Audio")).Should().BeFalse();
        File.ReadAllText(tree.Log).Should().Contain("Done - 1 failed package(s) kept");
    }

    [Fact]
    public async Task NoInfStaged_RemovesTheRootAndSaysSo()
    {
        using var tree = new DriverTree();
        Directory.CreateDirectory(tree.Root);
        File.WriteAllText(Path.Combine(tree.Root, "readme.txt"), "nothing here");

        await RunAsync(tree, new Dictionary<string, int>());

        Directory.Exists(tree.Root).Should().BeFalse();
        File.ReadAllText(tree.Log).Should().Contain("0 driver package(s), 0 INF(s)");
    }

    [Fact]
    public async Task RootAbsent_StillFinishesCleanly()
    {
        using var tree = new DriverTree();

        var act = () => RunAsync(tree, new Dictionary<string, int>());

        await act.Should().NotThrowAsync();
        File.ReadAllText(tree.Log).Should().Contain("0 driver package(s), 0 INF(s)").And.Contain("Done - 0 failed package(s) kept");
    }
}
