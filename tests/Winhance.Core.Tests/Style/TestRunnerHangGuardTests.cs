using FluentAssertions;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Core.Tests.Style;

// The gates run unattended over SSH. A test that hangs without a hang timeout stalls the gate with no end and no
// test name, so every test run in the runner script carries one.
public class TestRunnerHangGuardTests
{
    private static readonly string Runner = File.ReadAllText(
        Path.Combine(RepoPaths.SolutionDir(), "extras", "run-winhance-tests.ps1"));

    [Fact]
    public void DotnetTestRuns_HaveAHangTimeout()
    {
        Runner.Should().Contain("dotnet test").And.Contain("--blame-hang-timeout");
    }

    [Fact]
    public void VstestRuns_HaveAHangTimeout()
    {
        Runner.Should().Contain("vstest.console").And.Contain("/Blame:CollectHangDump;TestTimeout=");
    }
}
