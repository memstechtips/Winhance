using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.Infrastructure.Features.AdvancedTools.Helpers;
using Xunit;

namespace Winhance.Infrastructure.Tests.AdvancedTools;

public class ApplyOpScriptEmitterTests
{
    // The indents the autounattend passes sit at: the system pass inside one `if`, the user pass nested inside
    // the per-user loop.
    private const string SystemIndent = "    ";
    private const string UserIndent = "            ";

    private const string MixedHiveContent =
        "Windows Registry Editor Version 5.00\r\n\r\n[HKEY_CURRENT_USER\\Software\\Parity]\r\n\"A\"=dword:00000001\r\n"
        + "\r\n[HKEY_LOCAL_MACHINE\\SOFTWARE\\Parity]\r\n\"B\"=dword:00000001\r\n";

    private static readonly AppChoice[] NoApps = Array.Empty<AppChoice>();

    private readonly Mock<ILogService> _log = new();
    private readonly ApplyOpScriptEmitter _sut;

    public ApplyOpScriptEmitterTests()
    {
        _sut = new ApplyOpScriptEmitter(_log.Object);
    }

    private EmitResult EmitOne(string id, ChoiceValue value) =>
        Emit(ParityCatalog.ByFeature, new SettingChoice(id, value));

    private EmitResult Emit(IReadOnlyDictionary<string, IReadOnlyList<Setting>> byFeature, params SettingChoice[] choices) =>
        _sut.Emit(
            new SelectionSet(choices, NoApps, NoApps, AutounattendChoices.None),
            byFeature,
            ParityCatalog.Build,
            SystemIndent,
            UserIndent);

    private static string SystemText(EmitResult result) => string.Join("\n", result.SystemPassByFeature.Values);

    private static string UserText(EmitResult result) => string.Join("\n", result.UserPassByFeature.Values);

    [Fact]
    public void ToggleOn_Hklm_EmitsSetRegistryValueInSystemPass()
    {
        var result = EmitOne("parity-toggle-hklm", new ChoiceValue.Toggle(true));

        SystemText(result).Should().Contain(
            @"Set-RegistryValue -Path 'HKLM:\SOFTWARE\Parity' -Name 'V' -Type 'DWord' -Value 1 -Description 'Toggle HKLM description'");
        UserText(result).Should().NotContain("Parity'");
    }

    [Fact]
    public void ToggleOff_Hkcu_Absent_EmitsRemoveRegistryValueInUserPass()
    {
        var result = EmitOne("parity-toggle-hkcu-delete", new ChoiceValue.Toggle(false));

        UserText(result).Should().Contain(
            @"Remove-RegistryValue -Path 'HKCU:\Software\Parity' -Name 'V' -Description 'Toggle HKCU delete description'");
        SystemText(result).Should().BeEmpty();
    }

    [Fact]
    public void KeyExistsOn_EmitsNewRegistryKey()
    {
        var result = EmitOne("parity-key-exists", new ChoiceValue.Toggle(true));

        UserText(result).Should().Contain(@"New-RegistryKey -Path 'HKCU:\Software\Classes\CLSID\");
        UserText(result).Should().Contain(@"' -Description 'Key existence description'");
    }

    [Fact]
    public void KeyExistsOff_EmitsRemoveRegistryKey()
    {
        var result = EmitOne("parity-key-exists", new ChoiceValue.Toggle(false));

        UserText(result).Should().Contain(@"Remove-RegistryKey -Path 'HKCU:\Software\Classes\CLSID\");
    }

    [Fact]
    public void Bit_EmitsSetBinaryBit()
    {
        var result = EmitOne("parity-bit", new ChoiceValue.Toggle(true));

        UserText(result).Should().Contain(
            @"Set-BinaryBit -Path 'HKCU:\Control Panel\Desktop' -Name 'UserPreferencesMask' -ByteIndex 4 -BitMask 0x20 -SetBit $True");
    }

    [Fact]
    public void Byte_EmitsSetBinaryByte()
    {
        var result = EmitOne("parity-byte", new ChoiceValue.Toggle(true));

        UserText(result).Should().Contain(
            @"Set-BinaryByte -Path 'HKCU:\Control Panel\Desktop' -Name 'MenuShowDelay' -ByteIndex 0 -ByteValue 0x03");
    }

    [Fact]
    public void PerSubkey_WrapsInForEachObject()
    {
        var result = EmitOne("parity-per-subkey", new ChoiceValue.Toggle(true));

        SystemText(result).Should().Contain(
            @"Get-ChildItem -Path 'HKLM:\SYSTEM\Parity\Interfaces' -ErrorAction SilentlyContinue | ForEach-Object {");
        SystemText(result).Should().Contain(
            "Set-RegistryValue -Path $_.PSPath -Name 'N' -Type 'DWord' -Value 1");
    }

    [Fact]
    public void Scripts_RouteByRunContext()
    {
        var result = EmitOne("parity-scripts", new ChoiceValue.Toggle(true));

        SystemText(result).Should().Contain("Write-Host 'system side'");
        SystemText(result).Should().NotContain("Write-Host 'user side'");
        UserText(result).Should().Contain("Write-Host 'user side'");
        UserText(result).Should().NotContain("Write-Host 'system side'");
    }

    [Fact]
    public void RegContent_Hkcu_EmitsRegImportInUserPass()
    {
        var result = EmitOne("parity-regcontent", new ChoiceValue.Toggle(true));

        UserText(result).Should().Contain("reg import \"$tempRegFile\"");
        SystemText(result).Should().NotContain("reg import \"$tempRegFile\"");
    }

    [Fact]
    public void Task_EmitsSchtasksBatchInSystemPass()
    {
        var result = EmitOne("parity-task", new ChoiceValue.Toggle(false));

        SystemText(result).Should().Contain("@{ TN=\"\\Microsoft\\Windows\\Parity\\Task\"; Action=\"/Disable\"");
        SystemText(result).Should().Contain("schtasks /Change");
    }

    [Fact]
    public void SelectionOption_WritesThatStatesPayload()
    {
        var result = EmitOne("parity-selection", new ChoiceValue.Option(2));

        UserText(result).Should().Contain("-Name 'Mode' -Type 'DWord' -Value 2");
    }

    [Fact]
    public void SelectionCustom_WritesRegistryOnly_NoScripts()
    {
        var result = EmitOne("parity-selection", new ChoiceValue.CustomValues(new Dictionary<string, object> { ["Mode"] = 9 }));

        UserText(result).Should().Contain("-Value 9");
        UserText(result).Should().NotContain("# PowerShell script for");
        SystemText(result).Should().NotContain("# PowerShell script for");
    }

    [Fact]
    public void PowerCfgSelection_ProducesOnePowerRow_NoRegistryText()
    {
        var result = EmitOne("parity-powercfg-selection", new ChoiceValue.AcDcOption(1, 0));

        result.PowerRows.Should().ContainSingle();
        result.PowerRows[0].SettingGuid.Should().Be("0853a681-27c8-4100-a2fd-82013e970683");
        result.PowerRows[0].Ac.Should().Be(900);
        result.PowerRows[0].Dc.Should().Be(300);
        SystemText(result).Should().NotContain("powercfg");
        UserText(result).Should().NotContain("powercfg");
    }

    // The choice holds seconds, the slider is authored in minutes, and the row has to come back out in the
    // seconds powercfg stores - so the display-units round trip has to cancel out exactly.
    [Fact]
    public void Slider_ConvertsSystemToDisplayForTheResolver_AndBackToSystemInTheRow()
    {
        var result = EmitOne("parity-slider", new ChoiceValue.AcDcNumber(600, 300));

        result.PowerRows.Should().ContainSingle();
        result.PowerRows[0].Ac.Should().Be(600);
        result.PowerRows[0].Dc.Should().Be(300);
    }

    [Fact]
    public void Composite_EmitsSetRegistryCompositeValue()
    {
        var result = EmitOne("parity-composite", new ChoiceValue.Toggle(true));

        UserText(result).Should().Contain(
            @"Set-RegistryCompositeValue -Path 'HKCU:\Software\Microsoft\DirectX\UserGpuPreferences' -Name 'DirectXUserGlobalSettings' -Key 'SwapEffectUpgradeEnable' -SubValue '1'");
    }

    [Fact]
    public void StringFlag_EmitsSetRegistryStringFlag()
    {
        var result = EmitOne("parity-string-flag", new ChoiceValue.Toggle(true));

        UserText(result).Should().Contain("Set-RegistryStringFlag -Path 'HKCU:\\Control Panel\\Accessibility\\MouseKeys'");
        UserText(result).Should().Contain("-FlagMask 4 -AbsentBase 62 -Set $True");
    }

    [Fact]
    public void Lock_EmitsUnlockWriteLock()
    {
        var text = SystemText(EmitOne("parity-lock", new ChoiceValue.Toggle(true)));

        int unlock = text.IndexOf("Unlock-RegistryKey -Path", StringComparison.Ordinal);
        int write = text.IndexOf("-Name 'Start' -Type 'DWord' -Value 4", StringComparison.Ordinal);
        int relock = text.IndexOf("Lock-RegistryKey -Path", StringComparison.Ordinal);

        unlock.Should().BeGreaterThanOrEqualTo(0);
        write.Should().BeGreaterThan(unlock);
        relock.Should().BeGreaterThan(write);
    }

    // The Enabled state IS this build's Windows default, so its ResetSet applies on a plain apply too.
    [Fact]
    public void ResetSet_OnWindowsDefaultState_Deletes()
    {
        var result = EmitOne("parity-resetset", new ChoiceValue.Toggle(true));

        UserText(result).Should().Contain(@"Remove-RegistryValue -Path 'HKCU:\Software\ParityReset' -Name 'E'");
    }

    [Fact]
    public void Action_EmitsWriteAndScript_OnlyWhenOn()
    {
        var on = EmitOne("parity-action", new ChoiceValue.Toggle(true));

        SystemText(on).Should().Contain("-Name 'Ran' -Type 'DWord' -Value 1");
        SystemText(on).Should().Contain("Write-Host 'action script'");

        var off = EmitOne("parity-action", new ChoiceValue.Toggle(false));

        SystemText(off).Should().BeEmpty();
        UserText(off).Should().BeEmpty();
    }

    [Fact]
    public void Hibernate_IsEmittedAfterTheFeaturesOtherLines()
    {
        var byFeature = FeatureOf(
            ToggleSetting("power-hibernation-enable", @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Power", "HibernateEnabled"),
            ToggleSetting("parity-hibernate-peer", @"HKEY_LOCAL_MACHINE\SOFTWARE\ParityHibernatePeer", "P"));

        var result = Emit(
            byFeature,
            new SettingChoice("power-hibernation-enable", new ChoiceValue.Toggle(true)),
            new SettingChoice("parity-hibernate-peer", new ChoiceValue.Toggle(true)));

        var text = SystemText(result);
        int peer = text.IndexOf(@"Set-RegistryValue -Path 'HKLM:\SOFTWARE\ParityHibernatePeer'", StringComparison.Ordinal);
        int hibernate = text.IndexOf("powercfg /hibernate on", StringComparison.Ordinal);

        peer.Should().BeGreaterThanOrEqualTo(0);
        hibernate.Should().BeGreaterThan(peer);
    }

    [Fact]
    public void PowerPlanChoice_IsReturnedForThePowerSection()
    {
        var setting = new Setting
        {
            Id = "parity-power-plan",
            Display = new Display { Name = "Power plan", Description = "Power plan description" },
            OptionSource = new StubOptionSource(),
        };

        var result = Emit(FeatureOf(setting), new SettingChoice("parity-power-plan", new ChoiceValue.PowerPlan("g", "n")));

        result.PowerPlan.Should().Be(new ChoiceValue.PowerPlan("g", "n"));
        SystemText(result).Should().BeEmpty();
        UserText(result).Should().BeEmpty();
    }

    [Fact]
    public void UnknownSettingId_IsWarnedAndSkipped()
    {
        var result = EmitOne("nope", new ChoiceValue.Toggle(true));

        result.Warnings.Should().ContainSingle(w => w.Contains("nope"));
        SystemText(result).Should().BeEmpty();
        _log.Verify(l => l.Log(LogLevel.Warning, It.IsAny<string>(), It.IsAny<Exception>()), Times.Once);
    }

    [Fact]
    public void RegContent_MixedHives_Throws()
    {
        var setting = new Setting
        {
            Id = "parity-mixed-regcontent",
            Display = new Display { Name = "Mixed hives", Description = "Mixed hives description" },
            States = new[]
            {
                new SettingState { Label = "Enabled", Effects = new Effect[] { new RegContentEffect(MixedHiveContent) } },
                new SettingState { Label = "Disabled" },
            },
        };

        Action act = () => Emit(FeatureOf(setting), new SettingChoice("parity-mixed-regcontent", new ChoiceValue.Toggle(true)));

        act.Should().Throw<InvalidOperationException>();
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<Setting>> FeatureOf(params Setting[] settings) =>
        new Dictionary<string, IReadOnlyList<Setting>> { [ParityCatalog.FeatureId] = settings };

    private static Setting ToggleSetting(string id, string path, string valueName) => new()
    {
        Id = id,
        Display = new Display { Name = id, Description = $"{id} description" },
        Targets = new Target[] { new RegTarget("V", new[] { path }, valueName, RegistryValueKind.DWord) },
        States = new[]
        {
            new SettingState { Label = "Enabled", Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(1) } },
            new SettingState { Label = "Disabled", Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(0) } },
        },
    };

    // updates-policy-mode is special-handled in the live app and declined by the resolver; its handler's registry
    // half is the chosen state's plan, which the autounattend has always written (and must keep writing).
    [Fact]
    public void DetectorSelection_EmitsTheChosenStatesWrites_LikeTheSpecialHandler()
    {
        var setting = new Setting
        {
            Id = "detector-selection",
            Display = new Display { Name = "Detector", Description = "Detector description" },
            Detector = new NullDetector(),
            Targets = new Target[] { new RegTarget("M", DetectorPath, "Mode", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState { Label = "Normal", Set = new Dictionary<string, StateValue> { ["M"] = StateValue.Absent } },
                new SettingState { Label = "Security", Set = new Dictionary<string, StateValue> { ["M"] = StateValue.Of(2) } },
            },
        };
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>> { [ParityCatalog.FeatureId] = new[] { setting } };

        var r = _sut.Emit(new SelectionSet(new[] { new SettingChoice("detector-selection", new ChoiceValue.Option(1)) }, Array.Empty<AppChoice>(), Array.Empty<AppChoice>(), AutounattendChoices.None),
                          byFeature, ParityCatalog.Build, "    ", "            ");

        SystemText(r).Should().Contain("Set-RegistryValue -Path 'HKLM:\\SOFTWARE\\ParityDetector' -Name 'Mode' -Type 'DWord' -Value 2 -Description 'Detector description'");
        r.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void PerSubkeyDelete_WrapsRemoveInForEachObject()
    {
        var t = SystemText(EmitOne("parity-per-subkey", new ChoiceValue.Toggle(false)));
        t.Should().Contain("Get-ChildItem -Path 'HKLM:\\SYSTEM\\Parity\\Interfaces' -ErrorAction SilentlyContinue | ForEach-Object {");
        t.Should().Contain("Remove-RegistryValue -Path $_.PSPath -Name 'N' -Description 'Per subkey description'");
    }

    [Fact]
    public void TaskOn_EmitsEnable()
    {
        SystemText(EmitOne("parity-task", new ChoiceValue.Toggle(true))).Should().Contain("@{ TN=\"\\Microsoft\\Windows\\Parity\\Task\"; Action=\"/Enable\"");
    }

    [Fact]
    public void Slider_RowKeepsTheSystemValue_NotTheDisplayRoundTrip()
    {
        // 90 s on a minutes slider would round-trip as 60 s through the resolver; powercfg wants the exact value.
        var r = EmitOne("parity-slider", new ChoiceValue.AcDcNumber(90, 45));
        r.PowerRows.Should().ContainSingle(row => row.Ac == 90 && row.Dc == 45);
    }

    [Fact]
    public void CompositeOff_WithAbsentPayload_EmitsRemove()
    {
        var setting = new Setting
        {
            Id = "composite-remove",
            Display = new Display { Name = "Composite", Description = "Composite remove description" },
            Targets = new Target[] { new RegTarget("C", CompositePath, "DirectXUserGlobalSettings", RegistryValueKind.String) { CompositeStringKey = "AutoHDREnable" } },
            States = new[]
            {
                new SettingState { Label = "Enabled", Set = new Dictionary<string, StateValue> { ["C"] = StateValue.Of("1") } },
                new SettingState { Label = "Disabled", Set = new Dictionary<string, StateValue> { ["C"] = StateValue.Absent } },
            },
        };
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>> { [ParityCatalog.FeatureId] = new[] { setting } };

        var r = _sut.Emit(new SelectionSet(new[] { new SettingChoice("composite-remove", new ChoiceValue.Toggle(false)) }, Array.Empty<AppChoice>(), Array.Empty<AppChoice>(), AutounattendChoices.None),
                          byFeature, ParityCatalog.Build, "    ", "            ");

        UserText(r).Should().Contain("Set-RegistryCompositeValue -Path 'HKCU:\\Software\\Microsoft\\DirectX\\UserGpuPreferences' -Name 'DirectXUserGlobalSettings' -Key 'AutoHDREnable' -Remove -Description 'Composite remove description'");
    }

    [Fact]
    public void KeyExistence_WithEmptyStringPayload_EmitsNewKeyAndDefaultValue()
    {
        var setting = new Setting
        {
            Id = "key-default",
            Display = new Display { Name = "Key default", Description = "Key default description" },
            Targets = new Target[] { new RegTarget("K", KeyDefaultPath, null, RegistryValueKind.String) },
            States = new[]
            {
                new SettingState { Label = "Enabled", Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Of("") } },
                new SettingState { Label = "Disabled", Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Absent } },
            },
        };
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>> { [ParityCatalog.FeatureId] = new[] { setting } };

        var r = _sut.Emit(new SelectionSet(new[] { new SettingChoice("key-default", new ChoiceValue.Toggle(true)) }, Array.Empty<AppChoice>(), Array.Empty<AppChoice>(), AutounattendChoices.None),
                          byFeature, ParityCatalog.Build, "    ", "            ");

        var t = UserText(r);
        t.Should().Contain("New-RegistryKey -Path 'HKCU:\\Software\\Classes\\CLSID\\{KEYDEFAULT}' -Description 'Key default description'");
        t.Should().Contain("Set-RegistryValue -Path 'HKCU:\\Software\\Classes\\CLSID\\{KEYDEFAULT}' -Name '(Default)' -Type 'String' -Value '' -Description 'Key default description'");
    }

    // A description quoting an option name ("Show all icons") or holding a $ used to break the generated script: the
    // single-quoted -Description and the double-quoted Write-Log each need their own escaping.
    [Fact]
    public void Description_WithQuotesAndDollar_IsEscapedForEachQuotingContext()
    {
        var setting = new Setting
        {
            Id = "quoted",
            Display = new Display { Name = "Quoted", Description = "Pick \"Show all\" or it's $5" },
            Targets = new Target[] { new RegTarget("V", QuotedPath, "V", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(1) },
                    Effects = new Effect[] { new ScriptEffect("Write-Host 'x'", RunContext.System) },
                },
                new SettingState { Label = "Disabled", Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(0) } },
            },
        };
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>> { [ParityCatalog.FeatureId] = new[] { setting } };

        var r = _sut.Emit(new SelectionSet(new[] { new SettingChoice("quoted", new ChoiceValue.Toggle(true)) }, Array.Empty<AppChoice>(), Array.Empty<AppChoice>(), AutounattendChoices.None),
                          byFeature, ParityCatalog.Build, "    ", "            ");

        var t = SystemText(r);
        t.Should().Contain("-Description 'Pick \"Show all\" or it''s $5'");
        t.Should().Contain("Write-Log \"Pick `\"Show all`\" or it's `$5\" \"SUCCESS\"");
    }

    // start-menu-clean-10 carries a here-string. Indenting its '@ terminator makes PowerShell read the rest of the
    // file as string content, so the whole generated script stops parsing.
    [Fact]
    public void ScriptEffect_WithHereString_KeepsTheBodyAndTerminatorUnindented()
    {
        var script = "$xml = @'\n<Layout>\n    <Item />\n</Layout>\n'@\nSet-Content -Value $xml";
        var setting = new Setting
        {
            Id = "heredoc",
            Display = new Display { Name = "Heredoc", Description = "Writes a layout file" },
            Targets = new Target[] { new RegTarget("V", HereStringPath, "V", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = "Enabled",
                    Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(1) },
                    Effects = new Effect[] { new ScriptEffect(script, RunContext.System) },
                },
                new SettingState { Label = "Disabled", Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(0) } },
            },
        };
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>> { [ParityCatalog.FeatureId] = new[] { setting } };

        var r = _sut.Emit(new SelectionSet(new[] { new SettingChoice("heredoc", new ChoiceValue.Toggle(true)) }, NoApps, NoApps, AutounattendChoices.None),
                          byFeature, ParityCatalog.Build, SystemIndent, UserIndent);

        var lines = SystemText(r).Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        lines.Should().Contain("        $xml = @'");
        lines.Should().Contain("<Layout>");
        lines.Should().Contain("    <Item />");
        lines.Should().Contain("'@");
        lines.Should().Contain("        Set-Content -Value $xml");
    }

    private static readonly string[] HereStringPath = [@"HKEY_LOCAL_MACHINE\SOFTWARE\ParityHereString"];

    private static readonly string[] QuotedPath = [@"HKEY_LOCAL_MACHINE\SOFTWARE\ParityQuoted"];

    private static readonly string[] CompositePath = [@"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences"];
    private static readonly string[] KeyDefaultPath = [@"HKEY_CURRENT_USER\Software\Classes\CLSID\{KEYDEFAULT}"];

    private static readonly string[] DetectorPath = [@"HKEY_LOCAL_MACHINE\SOFTWARE\ParityDetector"];

    private sealed class NullDetector : IStateDetector
    {
        public string? Detect(Setting setting, IDetectionContext context) => null;
    }

    private sealed class StubOptionSource : IDynamicOptionSource
    {
        public IReadOnlyList<DynamicOption> EnumerateOptions(IDetectionContext context) => Array.Empty<DynamicOption>();

        public string? CurrentSelection(IDetectionContext context) => null;
    }
}
