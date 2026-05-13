using FluentAssertions;
using System.Management.Automation;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

/// <summary>
/// Spike: verifies that Microsoft.PowerShell.5.ReferenceAssemblies (compile-time)
/// resolves to a working in-process PowerShell host at runtime on .NET 10 + WinUI 3
/// targeting net10.0-windows10.0.19041.0.
///
/// If this test fails with a runtime "Assembly not found" / type-load error, the
/// reference-assemblies package isn't enough on this TFM. Drop it from the csproj
/// and replace with Microsoft.PowerShell.SDK (PS 7 in-process) per the plan in
/// .agent-plans/2026-05-13-ps-detection-framework.md Task 3 Step 1b.
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
