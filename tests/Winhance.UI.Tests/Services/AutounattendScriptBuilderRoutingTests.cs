using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Interfaces;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Xunit;

namespace Winhance.UI.Tests.Services;

/// <summary>
/// Covers the SYSTEM-vs-user pass routing and placeholder substitution in
/// AutounattendScriptBuilder. Directly exercises BuildWinhancementsScriptAsync and inspects
/// the generated PowerShell to assert which block (SYSTEM or user) each payload lands in.
/// </summary>
public class AutounattendScriptBuilderRoutingTests
{
    // Each opener is emitted at column 0 preceded by a newline — anchor on that to avoid matching
    // embedded occurrences inside helper-function bodies or comments.
    private const string SystemBlockOpen = "\nif (-not $UserCustomizations) {";
    private const string UserBlockOpen = "\nif ($UserCustomizations) {";

    private static AutounattendScriptBuilder CreateBuilder(out Mock<ILogService> log)
    {
        log = new Mock<ILogService>();
        var runner = new Mock<IPowerShellRunner>();
        runner
            .Setup(r => r.ValidateScriptSyntaxAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // PowerSettingsScriptSection dereferences the active power plan; return a stub.
        var powerQuery = new Mock<IPowerSettingsQueryService>();
        powerQuery
            .Setup(p => p.GetActivePowerPlanAsync())
            .ReturnsAsync(new PowerPlan { Name = "Balanced", Guid = "SCHEME_CURRENT" });
        powerQuery
            .Setup(p => p.GetAllPowerSettingsACDCAsync(It.IsAny<string>()))
            .ReturnsAsync(new Dictionary<string, (int? acValue, int? dcValue)>());

        return new AutounattendScriptBuilder(
            powerQuery.Object,
            new Mock<IHardwareDetectionService>().Object,
            log.Object,
            new Mock<IComboBoxResolver>().Object,
            runner.Object);
    }

    private static UnifiedConfigurationFile ConfigWithOptimize(string featureId, params ConfigurationItem[] items)
    {
        return new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                IsIncluded = true,
                Features = new Dictionary<string, ConfigSection>
                {
                    [featureId] = new ConfigSection { IsIncluded = true, Items = items },
                },
            },
        };
    }

    private static Dictionary<string, IEnumerable<SettingDefinition>> SingleSetting(string featureId, SettingDefinition def)
        => new() { [featureId] = new[] { def } };

    // --- Helpers to locate content within SYSTEM vs user blocks -------------------------------

    private static (string systemBlock, string userBlock) SplitPasses(string script)
    {
        // `if (-not $UserCustomizations) {` is unique to the outer guard. `if ($UserCustomizations) {`
        // also appears inside the preamble's MODE log, so take the LAST occurrence for the outer
        // opener.
        int systemIdx = script.IndexOf(SystemBlockOpen);
        int userIdx = script.LastIndexOf(UserBlockOpen);
        systemIdx.Should().BeGreaterOrEqualTo(0, $"systemIdx sentinel should be found. Got {systemIdx}. Script head:\n{script.Substring(0, System.Math.Min(500, script.Length))}");
        userIdx.Should().BeGreaterThan(systemIdx, $"userIdx ({userIdx}) should be after systemIdx ({systemIdx}). Context around userIdx:\n{script.Substring(System.Math.Max(0, userIdx - 30), System.Math.Min(200, script.Length - System.Math.Max(0, userIdx - 30)))}");
        return (script.Substring(systemIdx, userIdx - systemIdx), script.Substring(userIdx));
    }

    // ------------------------------------------------------------------------------------------
    // PS script routing
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task PowerShellScript_MarkedUser_LandsInUserPassOnly()
    {
        var def = new SettingDefinition
        {
            Id = "test-hkcu-script",
            Name = "HKCU script",
            Description = "Writes to HKCU",
            InputType = InputType.Toggle,
            PowerShellScripts = new List<PowerShellScriptSetting>
            {
                new() { EnabledScript = "Set-ItemProperty 'HKCU:\\Foo' -Name Bar -Value 1", RunContext = RunContext.User },
            },
        };
        var item = new ConfigurationItem { Id = def.Id, InputType = InputType.Toggle, IsSelected = true };
        var builder = CreateBuilder(out _);

        var script = await builder.BuildWinhancementsScriptAsync(
            ConfigWithOptimize("test-feature", item),
            SingleSetting("test-feature", def));

        var (system, user) = SplitPasses(script);
        system.Should().NotContain("HKCU:\\\\Foo").And.NotContain("Set-ItemProperty 'HKCU");
        user.Should().Contain("Set-ItemProperty 'HKCU:\\Foo'");
    }

    [Fact]
    public async Task PowerShellScript_MarkedSystem_LandsInSystemPassOnly()
    {
        var def = new SettingDefinition
        {
            Id = "test-hklm-script",
            Name = "System-wide script",
            Description = "Writes system-wide",
            InputType = InputType.Toggle,
            PowerShellScripts = new List<PowerShellScriptSetting>
            {
                new() { EnabledScript = "Rename-Item 'C:\\System\\file.exe' 'file.old.exe'", RunContext = RunContext.System },
            },
        };
        var item = new ConfigurationItem { Id = def.Id, InputType = InputType.Toggle, IsSelected = true };
        var builder = CreateBuilder(out _);

        var script = await builder.BuildWinhancementsScriptAsync(
            ConfigWithOptimize("test-feature", item),
            SingleSetting("test-feature", def));

        var (system, user) = SplitPasses(script);
        system.Should().Contain("Rename-Item 'C:\\System\\file.exe'");
        user.Should().NotContain("Rename-Item 'C:\\System\\file.exe'");
    }

    [Fact]
    public async Task PowerShellScript_DefaultsToSystemRunContext()
    {
        var def = new SettingDefinition
        {
            Id = "test-default-script",
            Name = "No RunContext specified",
            Description = "Default",
            InputType = InputType.Toggle,
            PowerShellScripts = new List<PowerShellScriptSetting>
            {
                // RunContext intentionally omitted — should default to System
                new() { EnabledScript = "Write-Host 'default-ctx'" },
            },
        };
        var item = new ConfigurationItem { Id = def.Id, InputType = InputType.Toggle, IsSelected = true };
        var builder = CreateBuilder(out _);

        var script = await builder.BuildWinhancementsScriptAsync(
            ConfigWithOptimize("test-feature", item),
            SingleSetting("test-feature", def));

        var (system, user) = SplitPasses(script);
        system.Should().Contain("default-ctx");
        user.Should().NotContain("default-ctx");
    }

    // ------------------------------------------------------------------------------------------
    // DNS custom state substitution (#582 bug 1)
    // ------------------------------------------------------------------------------------------

    private static SettingDefinition DnsServerDefinition() => new()
    {
        Id = "test-dns",
        Name = "DNS Server",
        Description = "DNS test",
        InputType = InputType.Selection,
        ComboBox = new ComboBoxMetadata
        {
            Options = new[]
            {
                new ComboBoxOption { DisplayName = "Automatic", Script = ScriptOption.Disabled, IsDefault = true },
                new ComboBoxOption
                {
                    DisplayName = "Cloudflare",
                    Script = ScriptOption.Enabled,
                    ScriptVariables = new Dictionary<string, string> { ["primary"] = "1.1.1.1", ["secondary"] = "1.0.0.1" },
                },
            },
        },
        PowerShellScripts = new List<PowerShellScriptSetting>
        {
            new()
            {
                EnabledScript = "Set-DnsClientServerAddress -ServerAddresses @('{{primary}}','{{secondary}}')",
                DisabledScript = "Set-DnsClientServerAddress -ResetServerAddresses",
                RunContext = RunContext.User,
            },
        },
    };

    [Fact]
    public async Task DnsPreset_SubstitutesScriptVariables_IntoUserPass()
    {
        var def = DnsServerDefinition();
        var item = new ConfigurationItem { Id = def.Id, InputType = InputType.Selection, SelectedIndex = 1 };
        var builder = CreateBuilder(out _);

        var script = await builder.BuildWinhancementsScriptAsync(
            ConfigWithOptimize("gaming", item),
            SingleSetting("gaming", def));

        var (system, user) = SplitPasses(script);
        user.Should().Contain("Set-DnsClientServerAddress -ServerAddresses @('1.1.1.1','1.0.0.1')");
        user.Should().NotContain("-ResetServerAddresses");
        system.Should().NotContain("Set-DnsClientServerAddress");
    }

    [Fact]
    public async Task DnsCustomState_SubstitutesFromCustomStateValues_AndDoesNotEmitReset()
    {
        var def = DnsServerDefinition();
        var item = new ConfigurationItem
        {
            Id = def.Id,
            InputType = InputType.Selection,
            // SelectedIndex may point past Options for the "Custom" pseudo-option; leave unset
            CustomStateValues = new Dictionary<string, object>
            {
                ["primary"] = "9.9.9.9",
                ["secondary"] = "149.112.112.112",
            },
        };
        var builder = CreateBuilder(out _);

        var script = await builder.BuildWinhancementsScriptAsync(
            ConfigWithOptimize("gaming", item),
            SingleSetting("gaming", def));

        var (_, user) = SplitPasses(script);
        user.Should().Contain("Set-DnsClientServerAddress -ServerAddresses @('9.9.9.9','149.112.112.112')");
        user.Should().NotContain("-ResetServerAddresses");
        user.Should().NotContain("{{primary}}");
    }

    [Fact]
    public async Task DnsCustomState_OverridesOptionScriptVariables()
    {
        var def = DnsServerDefinition();
        // SelectedIndex=1 (Cloudflare preset) but user also provided custom values — custom wins.
        var item = new ConfigurationItem
        {
            Id = def.Id,
            InputType = InputType.Selection,
            SelectedIndex = 1,
            CustomStateValues = new Dictionary<string, object>
            {
                ["primary"] = "8.8.8.8",
                ["secondary"] = "8.8.4.4",
            },
        };
        var builder = CreateBuilder(out _);

        var script = await builder.BuildWinhancementsScriptAsync(
            ConfigWithOptimize("gaming", item),
            SingleSetting("gaming", def));

        var (_, user) = SplitPasses(script);
        user.Should().Contain("@('8.8.8.8','8.8.4.4')");
        user.Should().NotContain("1.1.1.1");
    }

    // ------------------------------------------------------------------------------------------
    // Registry routing unchanged — sanity check that HKLM regs still land in system pass
    // ------------------------------------------------------------------------------------------

    // ------------------------------------------------------------------------------------------
    // RegContents routing and mixed-hive rejection
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task RegContents_HkcuMentionedInComment_StillRoutesToSystemPass()
    {
        // The old detector treated any content containing the substring "HKCU" as user-pass,
        // even in a comment or REG_SZ value. The tightened detector inspects section headers only.
        var def = new SettingDefinition
        {
            Id = "test-regcontents-comment",
            Name = "HKCR with HKCU comment",
            Description = "Comment mentions HKCU but all headers are HKCR",
            InputType = InputType.Toggle,
            // Feature emission is gated on a RegistrySetting (or other payload) existing at all.
            // Real RegContents settings always pair with at least one RegistrySetting — keeping
            // this test aligned with that reality.
            RegistrySettings = new List<RegistrySetting>
            {
                new()
                {
                    KeyPath = @"HKEY_CLASSES_ROOT\Winhance.Test",
                    ValueName = "",
                    EnabledValue = ["test"],
                    DisabledValue = [null],
                    DefaultValue = null,
                    RecommendedValue = null,
                    ValueType = RegistryValueKind.String,
                },
            },
            RegContents = new List<RegContentSetting>
            {
                new()
                {
                    EnabledContent =
                        "Windows Registry Editor Version 5.00\r\n" +
                        "; note: HKCU does not need this entry\r\n" +
                        "[HKEY_CLASSES_ROOT\\Winhance.Test]\r\n" +
                        "@=\"test\"\r\n",
                    DisabledContent = "Windows Registry Editor Version 5.00\r\n[-HKEY_CLASSES_ROOT\\Winhance.Test]\r\n",
                },
            },
        };
        var item = new ConfigurationItem { Id = def.Id, InputType = InputType.Toggle, IsSelected = true };
        var builder = CreateBuilder(out _);

        var script = await builder.BuildWinhancementsScriptAsync(
            ConfigWithOptimize("test-feature", item),
            SingleSetting("test-feature", def));

        var (system, user) = SplitPasses(script);
        system.Should().Contain(@"HKEY_CLASSES_ROOT\Winhance.Test").And.Contain("reg import");
        user.Should().NotContain(@"HKEY_CLASSES_ROOT\Winhance.Test");
    }

    [Fact]
    public async Task RegContents_MixedHiveBlock_ThrowsOnBuild()
    {
        var def = new SettingDefinition
        {
            Id = "test-regcontents-mixed",
            Name = "Mixed-hive block",
            Description = "Author error",
            InputType = InputType.Toggle,
            RegistrySettings = new List<RegistrySetting>
            {
                new()
                {
                    KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Winhance\MixedTest",
                    ValueName = "Flag",
                    EnabledValue = [1],
                    DisabledValue = [0],
                    RecommendedValue = 1,
                    DefaultValue = 0,
                    ValueType = RegistryValueKind.DWord,
                },
            },
            RegContents = new List<RegContentSetting>
            {
                new()
                {
                    EnabledContent =
                        "Windows Registry Editor Version 5.00\r\n" +
                        "[HKEY_LOCAL_MACHINE\\SOFTWARE\\Foo]\r\n" +
                        "\"A\"=dword:00000001\r\n" +
                        "[HKEY_CURRENT_USER\\SOFTWARE\\Foo]\r\n" +
                        "\"B\"=dword:00000001\r\n",
                    DisabledContent = "Windows Registry Editor Version 5.00\r\n",
                },
            },
        };
        var item = new ConfigurationItem { Id = def.Id, InputType = InputType.Toggle, IsSelected = true };
        var builder = CreateBuilder(out _);

        var act = async () => await builder.BuildWinhancementsScriptAsync(
            ConfigWithOptimize("test-feature", item),
            SingleSetting("test-feature", def));

        await act.Should().ThrowAsync<System.InvalidOperationException>()
            .WithMessage("*mixes HKEY_CURRENT_USER and system-hive*");
    }

    [Fact]
    public async Task HklmRegistryToggle_LandsInSystemPassOnly()
    {
        var def = new SettingDefinition
        {
            Id = "test-hklm-toggle",
            Name = "HKLM toggle",
            Description = "HKLM test",
            InputType = InputType.Toggle,
            RegistrySettings = new List<RegistrySetting>
            {
                new()
                {
                    KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Winhance\TestKey",
                    ValueName = "Flag",
                    EnabledValue = [1],
                    DisabledValue = [0],
                    RecommendedValue = 1,
                    DefaultValue = 0,
                    ValueType = RegistryValueKind.DWord,
                },
            },
        };
        var item = new ConfigurationItem { Id = def.Id, InputType = InputType.Toggle, IsSelected = true };
        var builder = CreateBuilder(out _);

        var script = await builder.BuildWinhancementsScriptAsync(
            ConfigWithOptimize("test-feature", item),
            SingleSetting("test-feature", def));

        var (system, user) = SplitPasses(script);
        system.Should().Contain(@"HKLM:\SOFTWARE\Winhance\TestKey");
        user.Should().NotContain(@"HKLM:\SOFTWARE\Winhance\TestKey");
    }

    // ------------------------------------------------------------------------------------------
    // PS-only settings emit Enable/Disable into the SYSTEM block
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task PowerShellOnly_Setting_EmitsEnabledScript_IntoSystemBlock()
    {
        var def = new SettingDefinition
        {
            Id = "ps-only",
            Name = "ps-only",
            Description = "PS-only setting",
            InputType = InputType.Toggle,
            DetectionType = DetectionType.SystemRestore,
            PowerShellScripts = new List<PowerShellScriptSetting>
            {
                new()
                {
                    EnabledScript = "Enable-ComputerRestore -Drive 'C:\\'",
                    DisabledScript = "Disable-ComputerRestore -Drive 'C:\\'",
                    RunContext = RunContext.System,
                },
            },
        };
        var item = new ConfigurationItem { Id = def.Id, InputType = InputType.Toggle, IsSelected = true };
        var builder = CreateBuilder(out _);

        var script = await builder.BuildWinhancementsScriptAsync(
            ConfigWithOptimize("GamingAndPerformance", item),
            SingleSetting("GamingAndPerformance", def));

        var (system, _) = SplitPasses(script);
        system.Should().Contain("Enable-ComputerRestore");
    }
}
