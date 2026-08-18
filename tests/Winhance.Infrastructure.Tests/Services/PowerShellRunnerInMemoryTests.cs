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
}
