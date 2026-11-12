using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Utilities;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

// Actually shells out to powershell.exe; Windows only.
public class PowerShellRunnerInMemoryTests
{
    private static PowerShellRunner NewRunner()
    {
        var fs = new Mock<IFileSystemService>();
        // RunScriptInMemoryAsync doesn't touch the file system, so unconfigured mock is fine.
        return new PowerShellRunner(fs.Object);
    }

    [Fact]
    public async Task RunScriptInMemoryAsync_EmptyScript_Throws()
    {
        var runner = NewRunner();
        var act = async () => await runner.RunScriptInMemoryAsync("");
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RunScriptInMemoryAsync_ScriptTooLarge_Throws()
    {
        var runner = NewRunner();
        var huge = new string('x', 30_000);  // 60 KB UTF-16, well over the 24 KB cap
        var script = $"$x = '{huge}'; $x.Length";
        var act = async () => await runner.RunScriptInMemoryAsync(script);
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*supports up to*");
    }

    [Fact]
    public async Task RunScriptInMemoryAsync_SimpleScript_ReturnsStdout()
    {
        var runner = NewRunner();
        var output = await runner.RunScriptInMemoryAsync("Write-Output 'hello from memory'");
        output.Should().Contain("hello from memory");
    }

    [Fact]
    public async Task RunScriptFromStdinAsync_ScriptLargerThanTheEncodedCommandCap_Runs()
    {
        var runner = NewRunner();
        var padding = new string('x', 60_000);
        var output = await runner.RunScriptFromStdinAsync($"$pad = '{padding}'; Write-Output \"ok $($pad.Length)\"");
        output.Should().Contain("ok 60000");
    }

    [Fact]
    public async Task RunScriptFromStdinAsync_NonAsciiInTheScript_SurvivesAsUtf8()
    {
        var runner = NewRunner();
        var output = await runner.RunScriptFromStdinAsync("Write-Output ([int][char]'\u00e9')");
        output.Should().Contain("233");
    }

    [Fact]
    public void StdinStub_DecodesStdinAsUtf8AndRunsItAsAScriptBlock()
    {
        PowerShellRunner.StdinStub.Should().Contain("OpenStandardInput").And.Contain("UTF8.GetString").And.Contain("ScriptBlock]::Create");
    }
}
