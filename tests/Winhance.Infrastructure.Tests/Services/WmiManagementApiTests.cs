using FluentAssertions;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

// These facts run against the machine's real WMI service, so what they pin is this adapter's
// translation, not the hardware under it.
public class WmiManagementApiTests
{
    private const string RegistryScope = @"root\default";
    private const string CurrentVersionKeyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";

    // StdRegProv takes the hive as a numeric handle; 0x80000002 is HKEY_LOCAL_MACHINE.
    private const uint HkeyLocalMachine = 0x80000002;

    private readonly WmiManagementApi _api = new();

    [Fact]
    public void Query_Win32OperatingSystem_ReturnsOneInstanceWhoseCaptionNamesWindows()
    {
        var instances = _api.Query(WmiScope.Cimv2, "Win32_OperatingSystem", null);

        instances.Should().ContainSingle();

        using var os = instances[0];
        var caption = os.Get("Caption") as string;

        caption.Should().NotBeNull();
        caption.Should().Contain("Windows");
    }

    [Fact]
    public void Query_WithACondition_FiltersRows()
    {
        // The machine has one OS and it is the primary one, so a condition that reached WMI intact
        // still returns it, and one that arrived malformed throws instead.
        var instances = _api.Query(WmiScope.Cimv2, "Win32_OperatingSystem", "Primary = TRUE");

        instances.Should().ContainSingle();
        instances[0].Dispose();
    }

    [Fact]
    public void Get_MissingProperty_ReturnsNull()
    {
        // WMI raises NotFound for a property the class does not declare, rather than handing back
        // null, so the adapter's catch is the only reason a caller can probe for one.
        var instances = _api.Query(WmiScope.Cimv2, "Win32_OperatingSystem", null);
        instances.Should().NotBeEmpty();

        using var os = instances[0];

        os.Get("NoSuchProperty2026").Should().BeNull();
    }

    [Fact]
    public void InvokeClassMethod_StdRegProvGetStringValue_ReturnsZeroAndTheValue()
    {
        using var result = _api.InvokeClassMethod(
            RegistryScope, "StdRegProv", "GetStringValue", ProductNameParameters());

        var productName = result.Output.Get("sValue") as string;

        result.ReturnValue.Should().Be(0u);
        productName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetRelated_OnAMethodOutput_Throws()
    {
        // A method's out-parameters arrive as an embedded object with no path back to the provider,
        // so an association query has nothing to walk from.
        using var result = _api.InvokeClassMethod(
            RegistryScope, "StdRegProv", "GetStringValue", ProductNameParameters());

        var act = () => result.Output.GetRelated("Win32_ComputerSystem");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ResultOf_NoOutputObject_ReadsAsZeroWithAnEmptyOutput()
    {
        // The one fact here that needs no WMI: System.Management returns null, not an empty
        // object, when the provider hands back no out-parameters.
        using var result = WmiManagementApi.ResultOf(null);

        result.ReturnValue.Should().Be(0u);
        result.Output.Get("ReturnValue").Should().BeNull();

        var act = () => result.Output.GetRelated("Win32_ComputerSystem");

        act.Should().Throw<InvalidOperationException>();
    }

    private static Dictionary<string, object> ProductNameParameters() =>
        new()
        {
            ["hDefKey"] = HkeyLocalMachine,
            ["sSubKeyName"] = CurrentVersionKeyPath,
            ["sValueName"] = "ProductName",
        };
}
