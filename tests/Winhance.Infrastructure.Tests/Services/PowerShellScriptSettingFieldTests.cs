using FluentAssertions;
using Winhance.Core.Features.Common.Models;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class PowerShellScriptSettingFieldTests
{
    [Fact]
    public void DetectionScript_RoundTrips_ViaRecordWith()
    {
        var original = new PowerShellScriptSetting
        {
            EnabledScript = "Enable-X",
            DisabledScript = "Disable-X",
            DetectionScript = "$true",
        };

        var clone = original with { DetectionScript = "$false" };

        original.DetectionScript.Should().Be("$true");
        clone.DetectionScript.Should().Be("$false");
        clone.EnabledScript.Should().Be("Enable-X");
    }
}
