using FluentAssertions;
using System.Management.Automation;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

/// <summary>
/// Smoke test for the in-process PowerShell host that backs the upcoming
/// IPowerShellDetectionService. Uses Microsoft.PowerShell.SDK (PS 7, app-bundled).
///
/// History: an earlier revision of this spike referenced
/// Microsoft.PowerShell.5.ReferenceAssemblies 1.1.0, which built cleanly but
/// threw InvalidProgramException at runtime on .NET 10 + WinUI 3 — the package's
/// .NETFramework-only metadata doesn't bind on the modern runtime. Switching to
/// the SDK package (which carries its own PS 7 runtime) is what made these
/// tests pass.
///
/// Delete this file once Task 3 lands the production IPowerShellDetectionService
/// (which has full test coverage of the same surface).
/// </summary>
public class PowerShellHostingSpike
{
    [Fact]
    public void CanHostPowerShellInProcess_AndReadBackTrue()
    {
        using var ps = PowerShell.Create();
        ps.AddScript("$true");
        var result = ps.Invoke<bool>();
        result.Should().ContainSingle().Which.Should().BeTrue();
    }

    [Fact]
    public void CanHostPowerShellInProcess_AndReadBackFalse()
    {
        using var ps = PowerShell.Create();
        ps.AddScript("$false");
        var result = ps.Invoke<bool>();
        result.Should().ContainSingle().Which.Should().BeFalse();
    }

    [Fact]
    public void CanCallGetCimInstance_FromInProcessHost()
    {
        // Real-world detection-script shape: query CIM. If this hangs, throws, or
        // returns nothing, the host isn't viable for our detection use case even
        // if simple `$true`/`$false` works.
        using var ps = PowerShell.Create();
        ps.AddScript("(Get-CimInstance Win32_OperatingSystem -ErrorAction Stop).Caption -ne $null");
        var result = ps.Invoke<bool>();
        result.Should().ContainSingle().Which.Should().BeTrue();
    }
}
