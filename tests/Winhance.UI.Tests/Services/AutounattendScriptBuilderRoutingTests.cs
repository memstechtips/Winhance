using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
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
/// Slice 7e-4b: the script-gen presence gates are catalog-only (an unpaired id contributes no presence
/// and its feature section is skipped), so every fixture pairs to a REAL catalog id. Each def fixture
/// carries only the payload its exercised path still reads: nothing but the id for catalog-driven
/// emits (INERT id-carrier), or the setting's REAL PowerShellScripts for the surviving def-fallback
/// (a Selection with no SelectedIndex).
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
            runner.Object,
            new Mock<IWindowsVersionService>().Object);
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
        // Slice 7e-4b: pairs to the REAL catalog toggle explorer-customization-legacy-notepad - the one
        // production toggle with a RunContext.User script (the HKCU App Paths cleanup). The paired script
        // pass reads the CATALOG ScriptEffects, so the def is an INERT id-carrier and the asserted text
        // below is the real catalog script body.
        var def = new SettingDefinition
        {
            Id = "explorer-customization-legacy-notepad",
            Name = "Use Legacy Notepad for text files",
            Description = "Legacy Notepad file handler",
            InputType = InputType.Toggle,
        };
        var item = new ConfigurationItem { Id = def.Id, InputType = InputType.Toggle, IsSelected = true };
        var builder = CreateBuilder(out _);

        var script = await builder.BuildWinhancementsScriptAsync(
            ConfigWithOptimize("test-feature", item),
            SingleSetting("test-feature", def));

        var (system, user) = SplitPasses(script);
        system.Should().NotContain("App Paths");
        user.Should().Contain(@"App Paths\notepad.exe");
    }

    [Fact]
    public async Task PowerShellScript_MarkedSystem_LandsInSystemPassOnly()
    {
        // Slice 7e-4b: pairs to the REAL catalog selection gaming-touch-keyboard-service. SelectedIndex 0
        // (Disabled, recommended) resolves catalog state 0, whose ScriptEffect is marked RunContext.System
        // (the TextInputHost rename/stop) - the def is an INERT id-carrier on this catalog script path.
        var def = new SettingDefinition
        {
            Id = "gaming-touch-keyboard-service",
            Name = "Touch Keyboard and Handwriting Panel Service",
            Description = "Touch keyboard service",
            InputType = InputType.Selection,
        };
        var item = new ConfigurationItem { Id = def.Id, InputType = InputType.Selection, SelectedIndex = 0 };
        var builder = CreateBuilder(out _);

        var script = await builder.BuildWinhancementsScriptAsync(
            ConfigWithOptimize("test-feature", item),
            SingleSetting("test-feature", def));

        var (system, user) = SplitPasses(script);
        system.Should().Contain("Stop-Process -Name TextInputHost");
        user.Should().NotContain("TextInputHost");
    }

    [Fact]
    public async Task PowerShellScript_DefaultsToSystemRunContext()
    {
        // The def-fallback script pass is still live for a Selection with no SelectedIndex (no catalog
        // state to resolve), so this pairs to the REAL catalog selection gaming-touch-keyboard-service and
        // the def carries that setting's REAL EnabledScript - whose PowerShellScriptSetting omits
        // RunContext in production, exercising the defaults-to-System routing on real payload. IsSelected
        // routes the old emitter to the EnabledScript; the DisabledScript is not read on this path and is
        // omitted per the minimal-fixture rule.
        var def = new SettingDefinition
        {
            Id = "gaming-touch-keyboard-service",
            Name = "Touch Keyboard and Handwriting Panel Service",
            Description = "Touch keyboard service",
            InputType = InputType.Selection,
            PowerShellScripts = new List<PowerShellScriptSetting>
            {
                // RunContext intentionally omitted, mirroring the production def - defaults to System.
                new() { EnabledScript = @"$f='C:\Windows\SystemApps\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\TextInputHost.exe'; $o=$f-replace'\.exe$','.old.exe'; if(Test-Path $o){if(Test-Path $f){Remove-Item $f -Force}; Rename-Item $o $f -Force}; Start-Process $f -ErrorAction SilentlyContinue" },
            },
        };
        var item = new ConfigurationItem { Id = def.Id, InputType = InputType.Selection, IsSelected = true };
        var builder = CreateBuilder(out _);

        var script = await builder.BuildWinhancementsScriptAsync(
            ConfigWithOptimize("test-feature", item),
            SingleSetting("test-feature", def));

        var (system, user) = SplitPasses(script);
        system.Should().Contain("Start-Process $f -ErrorAction SilentlyContinue");
        user.Should().NotContain("TextInputHost");
    }

    // ------------------------------------------------------------------------------------------
    // DNS custom state substitution (#582 bug 1)
    // ------------------------------------------------------------------------------------------

    private static SettingDefinition DnsServerDefinition() => new()
    {
        // Slice 7e-4b: the REAL gaming-dns-server def payload. PowerShellScripts is the one field the
        // surviving def-fallback path (a Selection with no SelectedIndex) still reads, so it carries the
        // production scripts verbatim; the old ComboBox presets live converter-baked in the catalog States
        // and no exercised path reads a def ComboBox anymore.
        Id = "gaming-dns-server",
        Name = "DNS Server",
        Description = "DNS test",
        InputType = InputType.Selection,
        PowerShellScripts = new List<PowerShellScriptSetting>
        {
            new()
            {
                EnabledScript = @"Get-NetAdapter | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.InterfaceIndex -ServerAddresses @('{{primary}}','{{secondary}}') }",
                DisabledScript = @"Get-NetAdapter | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex $_.InterfaceIndex -ResetServerAddresses }",
                RequiresElevation = true,
                RunContext = RunContext.User,
            },
            new()
            {
                EnabledScript = @"$known = @('1.1.1.1','1.0.0.1','8.8.8.8','8.8.4.4','9.9.9.9','149.112.112.112'); foreach ($s in $known) { netsh dns delete encryption server=$s 2>$null | Out-Null }; $t = '{{dohtemplate}}'; if ($t -and $t -notmatch '^\{\{') { netsh dns add encryption server={{primary}} dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null; netsh dns add encryption server={{secondary}} dohtemplate=$t autoupgrade=yes udpfallback=no | Out-Null }",
                DisabledScript = @"$known = @('1.1.1.1','1.0.0.1','8.8.8.8','8.8.4.4','9.9.9.9','149.112.112.112'); foreach ($s in $known) { netsh dns delete encryption server=$s 2>$null | Out-Null }",
                RequiresElevation = true,
                RunContext = RunContext.User,
            },
        },
    };

    [Fact]
    public async Task DnsPreset_SubstitutesScriptVariables_IntoUserPass()
    {
        // SelectedIndex 1 = the Cloudflare preset. A Selection WITH an index routes to the catalog script
        // emitter, whose state-1 ScriptEffect carries the preset IPs converter-BAKED into the script body,
        // so asserting the real baked text pins preset substitution surviving through the builder.
        var def = DnsServerDefinition();
        var item = new ConfigurationItem { Id = def.Id, InputType = InputType.Selection, SelectedIndex = 1 };
        var builder = CreateBuilder(out _);

        var script = await builder.BuildWinhancementsScriptAsync(
            ConfigWithOptimize("gaming", item),
            SingleSetting("gaming", def));

        var (system, user) = SplitPasses(script);
        user.Should().Contain("Set-DnsClientServerAddress -InterfaceIndex $_.InterfaceIndex -ServerAddresses @('1.1.1.1','1.0.0.1')");
        user.Should().NotContain("-ResetServerAddresses");
        system.Should().NotContain("Set-DnsClientServerAddress");
    }

    [Fact]
    public async Task DnsCustomState_SubstitutesFromCustomStateValues_AndDoesNotEmitReset()
    {
        // A "Custom" DNS entry matches no preset option, so SelectedIndex stays null and the script pass
        // takes the def-fallback (old emitter): the fixture's REAL EnabledScripts are emitted with
        // CustomStateValues substituted. The second real script's {{dohtemplate}} placeholder
        // intentionally survives substitution (its runtime guard treats a literal {{...}} as absent), so
        // only {{primary}} is asserted substituted - no blanket NotContain on "{{".
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
        user.Should().Contain("-ServerAddresses @('9.9.9.9','149.112.112.112')");
        user.Should().NotContain("-ResetServerAddresses");
        user.Should().NotContain("{{primary}}");
    }

    [Fact]
    public async Task SelectionOption_WithScriptNone_EmitsNoScript()
    {
        // Slice 7e-4b: pairs to the REAL catalog selection taskbar-system-tray-icons-11, whose "Custom"
        // option is the production ScriptOption.None case - modeled in the catalog as state 2 with NO
        // ScriptEffects. A control build on state 1 ("Hide all icons", a script-bearing state) proves the
        // pipeline emits this setting's script at all, so the None build's emptiness is the suppression
        // working rather than a skipped-section vacuity. The def is an INERT id-carrier on both builds.
        var def = new SettingDefinition
        {
            Id = "taskbar-system-tray-icons-11",
            Name = "System tray icons",
            Description = "System tray icons",
            InputType = InputType.Selection,
        };
        var builder = CreateBuilder(out _);

        var control = await builder.BuildWinhancementsScriptAsync(
            ConfigWithOptimize("taskbar", new ConfigurationItem { Id = def.Id, InputType = InputType.Selection, SelectedIndex = 1 }),
            SingleSetting("taskbar", def));
        var (_, controlUser) = SplitPasses(control);
        controlUser.Should().Contain("SystemTrayChevronVisibility");

        // SelectedIndex = 2 -> "Custom" -> ScriptOption.None -> no script emitted anywhere.
        var script = await builder.BuildWinhancementsScriptAsync(
            ConfigWithOptimize("taskbar", new ConfigurationItem { Id = def.Id, InputType = InputType.Selection, SelectedIndex = 2 }),
            SingleSetting("taskbar", def));
        script.Should().NotContain("SystemTrayChevronVisibility");
    }

    // ------------------------------------------------------------------------------------------
    // Registry routing unchanged — sanity check that HKLM regs still land in system pass
    // ------------------------------------------------------------------------------------------

    // ------------------------------------------------------------------------------------------
    // RegContents routing and mixed-hive rejection
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task RegContents_SystemHiveContent_RoutesToSystemPassOnly()
    {
        // Slice 7e-4b: pairs to the REAL catalog toggle explorer-take-ownership, whose Enabled state
        // carries the production HKCR .reg content (comment lines plus [HKEY_CLASSES_ROOT\...] headers).
        // The paired RegContents emit reads the CATALOG RegContentEffect, so the def is an INERT
        // id-carrier. Header-only hive detection must route the import into the SYSTEM pass and keep it
        // out of the user pass. (The old synthetic "comment mentions HKCU" foil cannot be constructed
        // against the static catalog; the header-only detector lives at
        // RegistryCommandEmitter.s_hkcuHeaderRegex.)
        var def = new SettingDefinition
        {
            Id = "explorer-take-ownership",
            Name = "Add 'Take Ownership' to Context Menu",
            Description = "Take ownership context menu",
            InputType = InputType.Toggle,
        };
        var item = new ConfigurationItem { Id = def.Id, InputType = InputType.Toggle, IsSelected = true };
        var builder = CreateBuilder(out _);

        var script = await builder.BuildWinhancementsScriptAsync(
            ConfigWithOptimize("test-feature", item),
            SingleSetting("test-feature", def));

        var (system, user) = SplitPasses(script);
        system.Should().Contain(@"[HKEY_CLASSES_ROOT\*\shell\TakeOwnership]").And.Contain("reg import");
        user.Should().NotContain("reg import").And.NotContain(@"HKEY_CLASSES_ROOT\*\shell\TakeOwnership");
    }

    [Fact]
    public async Task HklmRegistryToggle_LandsInSystemPassOnly()
    {
        // Slice 7e-4b: pairs to the REAL catalog toggle security-remote-assistance (HKLM DWORD
        // fAllowToGetHelp). The paired toggle emit reads the CATALOG RegTarget and state values, so the
        // def is an INERT id-carrier and the asserted path/value name below come from the catalog.
        var def = new SettingDefinition
        {
            Id = "security-remote-assistance",
            Name = "Remote Assistance",
            Description = "Remote assistance",
            InputType = InputType.Toggle,
        };
        var item = new ConfigurationItem { Id = def.Id, InputType = InputType.Toggle, IsSelected = true };
        var builder = CreateBuilder(out _);

        var script = await builder.BuildWinhancementsScriptAsync(
            ConfigWithOptimize("test-feature", item),
            SingleSetting("test-feature", def));

        var (system, user) = SplitPasses(script);
        system.Should().Contain(@"HKLM:\SYSTEM\CurrentControlSet\Control\Remote Assistance").And.Contain("fAllowToGetHelp");
        user.Should().NotContain("fAllowToGetHelp");
    }

    // ------------------------------------------------------------------------------------------
    // PS-only settings emit Enable/Disable into the SYSTEM block
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task PowerShellOnly_Setting_EmitsEnabledScript_IntoSystemBlock()
    {
        // Slice 7e-4b: pairs to the REAL catalog toggle system-restore-protection - the production
        // PS-only setting the old synthetic def copied. Script presence alone (catalog ScriptEffect,
        // Run = System) must open the feature section in the SYSTEM pass and emit the Enabled script;
        // the def is an INERT id-carrier.
        var def = new SettingDefinition
        {
            Id = "system-restore-protection",
            Name = "System Protection (Restore Points)",
            Description = "PS-only setting",
            InputType = InputType.Toggle,
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
