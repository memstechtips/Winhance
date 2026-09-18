using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.Infrastructure.Features.Autounattend.Helpers;
using Winhance.TestSupport;
using Xunit;

using Winhance.Core.Features.Common.Localization;
namespace Winhance.Infrastructure.Tests.Autounattend;

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
        _sut = new ApplyOpScriptEmitter(_log.Object, new FakeOptionProviderRegistry());
    }

    private EmitResult EmitOne(string id, ChoiceValue value) =>
        Emit(ParityCatalog.ByFeature, new SettingChoice(id, value));

    private EmitResult Emit(IReadOnlyDictionary<string, IReadOnlyList<Setting>> byFeature, params SettingChoice[] choices) =>
        _sut.Emit(
            new SelectionSet(choices, NoApps, NoApps),
            byFeature,
            ParityCatalog.Build,
            SystemIndent,
            UserIndent);

    private static string SystemText(EmitResult result) => string.Join("\n", result.SystemPassByFeature.Values);

    private static string UserText(EmitResult result) => string.Join("\n", result.UserPassByFeature.Values);

    private const string AlbumScript = "$album = '{{value}}'";

    private static Setting AlbumSetting() => new()
    {
        Id = "theme-wallpaper-album",
        Display = new() { Name = TestKeys.Of("Album"), Description = TestKeys.Of("The folder the slideshow plays") },
        Targets = new Target[] { new DesktopSlideshowTarget("album") },
        TextBox = new(new TextRule("^.{1,}$", UpperCase: false, TestKeys.Of("Anything.")), Picker: PickerKind.Folder),
        CustomStateScripts = [new ScriptEffect(AlbumScript, RunContext.User)],
    };

    private EmitResult EmitAlbum(string folder)
    {
        var setting = AlbumSetting();
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>> { [FeatureIds.WindowsTheme] = new[] { setting } };
        return Emit(byFeature, new SettingChoice(setting.Id, new ChoiceValue.Text(folder)));
    }

    // Windows' folder id can only be built on the PC that has the folder, so the album is a script, not a registry row.
    [Fact]
    public void A_slideshow_folder_emits_the_settings_script_with_the_folder_substituted()
    {
        var result = EmitAlbum(@"C:\Windows\Web\Wallpaper\Winhance\Holiday");

        result.Warnings.Should().BeEmpty();
        SystemText(result).Should().BeEmpty();
        UserText(result).Should().Contain($"{UserIndent}    $album = 'C:\\Windows\\Web\\Wallpaper\\Winhance\\Holiday'");
        UserText(result).Should().NotContain("{{value}}");
    }

    // An HKCU write emitted into the system pass would land in the wrong hive.
    [Fact]
    public void A_slideshow_script_goes_to_the_pass_its_run_context_names()
    {
        var result = EmitAlbum(@"D:\Albums\Trip");

        result.UserPassByFeature.Should().ContainKey(FeatureIds.WindowsTheme);
        result.SystemPassByFeature.Should().BeEmpty();
    }

    private static Setting IntervalSetting() => new()
    {
        Id = "theme-wallpaper-interval",
        Display = new() { Name = TestKeys.Of("Interval"), Description = TestKeys.Of("How often the picture changes") },
        Targets = new Target[] { new RegTarget("Interval", SlideshowPath, "Interval", RegistryValueKind.DWord) },
        States = new[]
        {
            new SettingState { Label = TestKeys.Of("1 minute"), Set = new Dictionary<string, StateValue> { ["Interval"] = StateValue.Of(60000) } },
            new SettingState { Label = TestKeys.Of("30 minutes"), Set = new Dictionary<string, StateValue> { ["Interval"] = StateValue.Of(1800000) } },
            new SettingState { Label = TestKeys.Of("1 day"), Set = new Dictionary<string, StateValue> { ["Interval"] = StateValue.Of(86400000) } },
        },
    };

    private static Setting ShuffleSetting() => new()
    {
        Id = "theme-wallpaper-shuffle",
        Display = new() { Name = TestKeys.Of("Shuffle"), Description = TestKeys.Of("Play the pictures in a random order") },
        Targets = new Target[] { new RegTarget("Shuffle", SlideshowPath, "Shuffle", RegistryValueKind.DWord) },
        States = new[]
        {
            new SettingState { Label = LocKey.Common.Enabled, Set = new Dictionary<string, StateValue> { ["Shuffle"] = StateValue.Of(1) } },
            new SettingState { Label = LocKey.Common.Disabled, Set = new Dictionary<string, StateValue> { ["Shuffle"] = StateValue.Of(0) } },
        },
    };

    [Fact]
    public void The_slideshow_script_comes_after_the_rows_it_reads()
    {
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>
        {
            [FeatureIds.WindowsTheme] = new[] { AlbumSetting(), IntervalSetting(), ShuffleSetting() },
        };

        var result = Emit(
            byFeature,
            new SettingChoice("theme-wallpaper-album", new ChoiceValue.Text(@"D:\Albums\Trip")),
            new SettingChoice("theme-wallpaper-interval", new ChoiceValue.Option(1)),
            new SettingChoice("theme-wallpaper-shuffle", new ChoiceValue.Toggle(true)));

        var user = UserText(result);
        int interval = user.IndexOf("-Name 'Interval'", StringComparison.Ordinal);
        int shuffle = user.IndexOf("-Name 'Shuffle'", StringComparison.Ordinal);
        int script = user.IndexOf("$album =", StringComparison.Ordinal);

        result.Warnings.Should().BeEmpty();
        interval.Should().BeGreaterThan(-1);
        shuffle.Should().BeGreaterThan(-1);
        script.Should().BeGreaterThan(interval).And.BeGreaterThan(shuffle);
    }

    [Fact]
    public void An_album_folder_with_an_apostrophe_is_doubled_for_the_literal()
    {
        var result = EmitAlbum(@"D:\Marco's Pictures\Trip");

        result.Warnings.Should().BeEmpty();
        UserText(result).Should().Contain(@"$album = 'D:\Marco''s Pictures\Trip'");
    }

    [Fact]
    public void An_album_folder_with_a_typographic_apostrophe_is_doubled_for_the_literal()
    {
        var result = EmitAlbum("D:\\Mom\u2019s photos");

        result.Warnings.Should().BeEmpty();
        UserText(result).Should().Contain("$album = 'D:\\Mom\u2019\u2019s photos'");
    }

    [Fact]
    public void An_empty_album_resolves_to_no_plan_and_is_reported()
    {
        var result = EmitAlbum(string.Empty);

        result.Warnings.Should().ContainSingle().Which.Should().Contain("theme-wallpaper-album");
        UserText(result).Should().BeEmpty();
    }

    [Fact]
    public void An_answer_file_only_choice_is_skipped_without_a_warning()
    {
        var setting = new Setting
        {
            Id = "setup-hide-eula",
            Display = new() { Name = TestKeys.Of("n"), Description = TestKeys.Of("d") },
            Targets = new Target[] { new AutounattendElement("K", "oobeSystem", "Microsoft-Windows-Shell-Setup", "OOBE/HideEULAPage") },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Roles = new[] { StateRole.WindowsDefault },
                    Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Absent },
                },
                new SettingState
                {
                    Label = LocKey.Common.Disabled,
                    Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Of("true") },
                },
            },
        };
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>> { [FeatureIds.Autounattend] = new[] { setting } };

        var result = Emit(byFeature, new SettingChoice(setting.Id, new ChoiceValue.Toggle(false)));

        result.Warnings.Should().BeEmpty();
        SystemText(result).Should().BeEmpty();
        UserText(result).Should().BeEmpty();
    }

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

    // gaming-dns-server's shape: no registry target, so its CustomStateScripts are the only thing the XML can
    // write, and a machine on a non-preset DNS must not ship them with the placeholders still in the text.
    [Fact]
    public void ScriptOnlyCustomState_RendersTheSubstitutedScript()
    {
        var setting = new Setting
        {
            Id = "script-custom",
            Display = new Display { Name = TestKeys.Of("Script custom"), Description = TestKeys.Of("Script custom description") },
            Detector = new NullDetector(),
            States = new[]
            {
                new SettingState { Label = TestKeys.Of("Automatic"), Effects = new Effect[] { new ScriptEffect("Reset-Dns", RunContext.User) } },
                new SettingState { Label = TestKeys.Of("Cloudflare"), Effects = new Effect[] { new ScriptEffect("Set-Dns 1.1.1.1", RunContext.User) } },
            },
            CustomStateScripts = new[]
            {
                new ScriptEffect("Set-Dns @('{{primary}}','{{secondary}}')", RunContext.User),
                new ScriptEffect("$t = '{{dohtemplate}}'; netsh add {{primary}} {{secondary}}", RunContext.User),
            },
        };
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>> { [ParityCatalog.FeatureId] = new[] { setting } };
        var custom = new Dictionary<string, object> { ["DetectedIndex"] = -1, ["primary"] = "10.5.0.1", ["secondary"] = "10.5.0.2" };

        var r = Emit(byFeature, new SettingChoice("script-custom", new ChoiceValue.CustomValues(custom)));

        var t = UserText(r);
        t.Should().Contain("Set-Dns @('10.5.0.1','10.5.0.2')");
        t.Should().Contain("netsh add 10.5.0.1 10.5.0.2");
        // The DoH script tests for the literal itself, so an unmatched placeholder is left standing.
        t.Should().Contain("$t = '{{dohtemplate}}'");
        SystemText(r).Should().BeEmpty();
        r.Warnings.Should().BeEmpty();
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
    public void Hibernation_ScriptIsEmittedInTheSystemPass()
    {
        var setting = SettingCatalog.ById["power-hibernation-enable"];
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>> { [FeatureIds.Power] = new[] { setting } };

        var result = Emit(byFeature, new SettingChoice(setting.Id, new ChoiceValue.Toggle(false)));

        result.Warnings.Should().BeEmpty();
        UserText(result).Should().BeEmpty();

        var text = SystemText(result);
        int write = text.IndexOf("-Name 'HibernateEnabled' -Type 'DWord' -Value 0", StringComparison.Ordinal);
        int powercfg = text.IndexOf("powercfg /hibernate off", StringComparison.Ordinal);

        write.Should().BeGreaterThanOrEqualTo(0);
        powercfg.Should().BeGreaterThan(write);
    }

    [Fact]
    public void PowerPlanChoice_IsReturnedForThePowerSection()
    {
        const string guid = "381b4222-f694-41f0-9685-ff5bb260df2e";
        var setting = new Setting
        {
            Id = "parity-power-plan",
            Display = new Display { Name = TestKeys.Of("Power plan"), Description = TestKeys.Of("Power plan description") },
            Options = new(OptionSource.PowerPlans),
        };

        var result = Emit(FeatureOf(setting), new SettingChoice("parity-power-plan", new ChoiceValue.Keyed(guid, "Balanced")));

        result.PowerPlan.Should().Be(new PowerPlanChoice(guid, "Balanced"));
        SystemText(result).Should().BeEmpty();
        UserText(result).Should().BeEmpty();
    }

    [Fact]
    public void PowerPlanChoice_NormalisesABracedUpperCaseGuid()
    {
        var setting = new Setting
        {
            Id = "parity-power-plan",
            Display = new Display { Name = TestKeys.Of("Power plan"), Description = TestKeys.Of("Power plan description") },
            Options = new(OptionSource.PowerPlans),
        };

        var result = Emit(
            FeatureOf(setting),
            new SettingChoice("parity-power-plan", new ChoiceValue.Keyed("{381B4222-F694-41F0-9685-FF5BB260DF2E}", "Balanced")));

        result.PowerPlan.Should().Be(new PowerPlanChoice("381b4222-f694-41f0-9685-ff5bb260df2e", "Balanced"));
    }

    [Fact]
    public void UnknownSettingId_IsWarnedAndSkipped()
    {
        var result = EmitOne("nope", new ChoiceValue.Toggle(true));

        result.Warnings.Should().ContainSingle(w => w.Contains("nope"));
        SystemText(result).Should().BeEmpty();
        _log.Verify(l => l.Log(LogLevel.Warning, It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void RegContent_MixedHives_Throws()
    {
        var setting = new Setting
        {
            Id = "parity-mixed-regcontent",
            Display = new Display { Name = TestKeys.Of("Mixed hives"), Description = TestKeys.Of("Mixed hives description") },
            States = new[]
            {
                new SettingState { Label = LocKey.Common.Enabled, Effects = new Effect[] { new RegContentEffect(MixedHiveContent) } },
                new SettingState { Label = LocKey.Common.Disabled },
            },
        };

        Action act = () => Emit(FeatureOf(setting), new SettingChoice("parity-mixed-regcontent", new ChoiceValue.Toggle(true)));

        act.Should().Throw<InvalidOperationException>();
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<Setting>> FeatureOf(params Setting[] settings) =>
        new Dictionary<string, IReadOnlyList<Setting>> { [ParityCatalog.FeatureId] = settings };

    // updates-policy-mode is special-handled in the live app and declined by the resolver; its handler's registry
    // half is the chosen state's plan, which the autounattend writes too.
    [Fact]
    public void DetectorSelection_EmitsTheChosenStatesWrites_LikeTheSpecialHandler()
    {
        var setting = new Setting
        {
            Id = "detector-selection",
            Display = new Display { Name = TestKeys.Of("Detector"), Description = TestKeys.Of("Detector description") },
            Detector = new NullDetector(),
            Targets = new Target[] { new RegTarget("M", DetectorPath, "Mode", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState { Label = TestKeys.Of("Normal"), Set = new Dictionary<string, StateValue> { ["M"] = StateValue.Absent } },
                new SettingState { Label = TestKeys.Of("Security"), Set = new Dictionary<string, StateValue> { ["M"] = StateValue.Of(2) } },
            },
        };
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>> { [ParityCatalog.FeatureId] = new[] { setting } };

        var r = _sut.Emit(new SelectionSet(new[] { new SettingChoice("detector-selection", new ChoiceValue.Option(1)) }, Array.Empty<AppChoice>(), Array.Empty<AppChoice>()),
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
            Display = new Display { Name = TestKeys.Of("Composite"), Description = TestKeys.Of("Composite remove description") },
            Targets = new Target[] { new RegTarget("C", CompositePath, "DirectXUserGlobalSettings", RegistryValueKind.String) { CompositeStringKey = "AutoHDREnable" } },
            States = new[]
            {
                new SettingState { Label = LocKey.Common.Enabled, Set = new Dictionary<string, StateValue> { ["C"] = StateValue.Of("1") } },
                new SettingState { Label = LocKey.Common.Disabled, Set = new Dictionary<string, StateValue> { ["C"] = StateValue.Absent } },
            },
        };
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>> { [ParityCatalog.FeatureId] = new[] { setting } };

        var r = _sut.Emit(new SelectionSet(new[] { new SettingChoice("composite-remove", new ChoiceValue.Toggle(false)) }, Array.Empty<AppChoice>(), Array.Empty<AppChoice>()),
                          byFeature, ParityCatalog.Build, "    ", "            ");

        UserText(r).Should().Contain("Set-RegistryCompositeValue -Path 'HKCU:\\Software\\Microsoft\\DirectX\\UserGpuPreferences' -Name 'DirectXUserGlobalSettings' -Key 'AutoHDREnable' -Remove -Description 'Composite remove description'");
    }

    [Fact]
    public void KeyExistence_WithEmptyStringPayload_EmitsNewKeyAndDefaultValue()
    {
        var setting = new Setting
        {
            Id = "key-default",
            Display = new Display { Name = TestKeys.Of("Key default"), Description = TestKeys.Of("Key default description") },
            Targets = new Target[] { new RegTarget("K", KeyDefaultPath, null, RegistryValueKind.String) },
            States = new[]
            {
                new SettingState { Label = LocKey.Common.Enabled, Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Of("") } },
                new SettingState { Label = LocKey.Common.Disabled, Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Absent } },
            },
        };
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>> { [ParityCatalog.FeatureId] = new[] { setting } };

        var r = _sut.Emit(new SelectionSet(new[] { new SettingChoice("key-default", new ChoiceValue.Toggle(true)) }, Array.Empty<AppChoice>(), Array.Empty<AppChoice>()),
                          byFeature, ParityCatalog.Build, "    ", "            ");

        var t = UserText(r);
        t.Should().Contain("New-RegistryKey -Path 'HKCU:\\Software\\Classes\\CLSID\\{KEYDEFAULT}' -Description 'Key default description'");
        t.Should().Contain("Set-RegistryValue -Path 'HKCU:\\Software\\Classes\\CLSID\\{KEYDEFAULT}' -Name '(Default)' -Type 'String' -Value '' -Description 'Key default description'");
    }

    // A description quoting an option name ("Show all icons") or holding a $ reaches two quoting contexts: the
    // single-quoted -Description and the double-quoted Write-Log each need their own escaping.
    [Fact]
    public void Description_WithQuotesAndDollar_IsEscapedForEachQuotingContext()
    {
        var setting = new Setting
        {
            Id = "quoted",
            Display = new Display { Name = TestKeys.Of("Quoted"), Description = TestKeys.Of("Pick \"Show all\" or it's $5") },
            Targets = new Target[] { new RegTarget("V", QuotedPath, "V", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(1) },
                    Effects = new Effect[] { new ScriptEffect("Write-Host 'x'", RunContext.System) },
                },
                new SettingState { Label = LocKey.Common.Disabled, Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(0) } },
            },
        };
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>> { [ParityCatalog.FeatureId] = new[] { setting } };

        var r = _sut.Emit(new SelectionSet(new[] { new SettingChoice("quoted", new ChoiceValue.Toggle(true)) }, Array.Empty<AppChoice>(), Array.Empty<AppChoice>()),
                          byFeature, ParityCatalog.Build, "    ", "            ");

        var t = SystemText(r);
        t.Should().Contain("-Description 'Pick \"Show all\" or it''s $5'");
        t.Should().Contain("Write-Log \"Pick `\"Show all`\" or it's `$5\" \"SUCCESS\"");
    }

    private EmitResult EmitScriptsSetting(Mock<ILocalizationService> localization) =>
        new ApplyOpScriptEmitter(_log.Object, new FakeOptionProviderRegistry(), localization.Object).Emit(
            new SelectionSet([new SettingChoice("parity-scripts", new ChoiceValue.Toggle(true))], NoApps, NoApps),
            ParityCatalog.ByFeature,
            ParityCatalog.Build,
            SystemIndent,
            UserIndent);

    [Fact]
    public void A_localized_description_is_escaped_for_the_string_it_lands_in()
    {
        var localization = new Mock<ILocalizationService>()
            .PresentKey("Scripts description", "l\u2019Explorateur \u201CShow all icons\u201D");

        var text = SystemText(EmitScriptsSetting(localization));

        text.Should().Contain("-Description 'l\u2019\u2019Explorateur \u201CShow all icons\u201D'");
        text.Should().Contain("Write-Log \"l\u2019Explorateur `\u201CShow all icons`\u201D\" \"SUCCESS\"");
    }

    [Fact]
    public void A_script_block_is_headed_by_the_localized_name()
    {
        var localization = new Mock<ILocalizationService>().PresentKey("Scripts", "Skripte");

        SystemText(EmitScriptsSetting(localization)).Should().Contain("# PowerShell script for: Skripte");
    }

    [Fact]
    public void A_line_break_in_the_localized_name_stays_inside_the_comment()
    {
        var localization = new Mock<ILocalizationService>().PresentKey("Scripts", "Skripte\r\nund Aufgaben");

        SystemText(EmitScriptsSetting(localization)).Should().Contain("# PowerShell script for: Skripte und Aufgaben");
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
            Display = new Display { Name = TestKeys.Of("Heredoc"), Description = TestKeys.Of("Writes a layout file") },
            Targets = new Target[] { new RegTarget("V", HereStringPath, "V", RegistryValueKind.DWord) },
            States = new[]
            {
                new SettingState
                {
                    Label = LocKey.Common.Enabled,
                    Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(1) },
                    Effects = new Effect[] { new ScriptEffect(script, RunContext.System) },
                },
                new SettingState { Label = LocKey.Common.Disabled, Set = new Dictionary<string, StateValue> { ["V"] = StateValue.Of(0) } },
            },
        };
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>> { [ParityCatalog.FeatureId] = new[] { setting } };

        var r = _sut.Emit(new SelectionSet(new[] { new SettingChoice("heredoc", new ChoiceValue.Toggle(true)) }, NoApps, NoApps),
                          byFeature, ParityCatalog.Build, SystemIndent, UserIndent);

        var lines = SystemText(r).Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        lines.Should().Contain("        $xml = @'");
        lines.Should().Contain("<Layout>");
        lines.Should().Contain("    <Item />");
        lines.Should().Contain("'@");
        lines.Should().Contain("        Set-Content -Value $xml");
    }

    [Fact]
    public void KeyedSelection_Hkcu_EmitsTheProvidersWriteInTheUserPass()
    {
        var setting = FakeOptionProvider.SettingFor("parity-keyed");
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>> { [ParityCatalog.FeatureId] = new[] { setting } };
        var result = Emit(byFeature, new SettingChoice("parity-keyed", new ChoiceValue.Keyed("beta", "Beta zone")));

        UserText(result).Should().Contain(
            @"Set-RegistryValue -Path 'HKCU:\Software\Winhance\Keyed' -Name 'Selected' -Type 'String' -Value 'beta'");
        SystemText(result).Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void KeyedSelection_WithAKeyTheProviderWillNotAccept_IsWarnedAndEmitsNothing()
    {
        var setting = FakeOptionProvider.SettingFor("parity-keyed");
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>> { [ParityCatalog.FeatureId] = new[] { setting } };
        var result = Emit(byFeature, new SettingChoice("parity-keyed", new ChoiceValue.Keyed("delta", "Delta zone")));

        UserText(result).Should().BeEmpty();
        result.Warnings.Should().ContainSingle(w => w.Contains("parity-keyed"));
    }

    [Fact]
    public void KeyedSelection_WhoseWriteCarriesSingleQuotes_DoublesThemInTheValue()
    {
        var setting = FakeOptionProvider.SettingFor("parity-keyed") with { Targets = QuotedLongDateTarget };
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>> { [ParityCatalog.FeatureId] = new[] { setting } };
        var provider = new Mock<IOptionProvider>();
        provider.Setup(p => p.Accepts(It.IsAny<Setting>(), QuotedKey)).Returns(true);
        provider.Setup(p => p.ValueFor(OptionValue.LongDate, QuotedKey)).Returns("dddd, d 'de' MMMM 'de' yyyy");
        var emitter = new ApplyOpScriptEmitter(_log.Object, new FakeOptionProviderRegistry(provider.Object));

        var result = emitter.Emit(
            new SelectionSet([new SettingChoice("parity-keyed", new ChoiceValue.Keyed(QuotedKey, "Spanish (Spain)"))], NoApps, NoApps),
            byFeature, ParityCatalog.Build, SystemIndent, UserIndent);

        UserText(result).Should().Contain(
            @"Set-RegistryValue -Path 'HKCU:\Software\Winhance\Keyed' -Name 'Selected' -Type 'String' -Value 'dddd, d ''de'' MMMM ''de'' yyyy'");
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void AnswerFileOnlySetting_EmitsItsChosenStatesScriptAndNothingElse()
    {
        var setting = new Setting
        {
            Id = "answer-file-script",
            Display = new Display { Name = TestKeys.Of("Answer file script"), Description = TestKeys.Of("Runs a script on the new install") },
            States = new[]
            {
                new SettingState { Label = LocKey.Common.Enabled, Effects = new Effect[] { new ScriptEffect("New-Item 'C:\\on'", RunContext.System) } },
                new SettingState { Label = LocKey.Common.Disabled, Effects = new Effect[] { new ScriptEffect("New-Item 'C:\\off'", RunContext.User) } },
            },
        };
        setting.IsAnswerFileOnly.Should().BeTrue();
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>> { [ParityCatalog.FeatureId] = new[] { setting } };

        var on = _sut.Emit(new SelectionSet(new[] { new SettingChoice("answer-file-script", new ChoiceValue.Toggle(true)) }, NoApps, NoApps),
                           byFeature, ParityCatalog.Build, SystemIndent, UserIndent);
        var off = _sut.Emit(new SelectionSet(new[] { new SettingChoice("answer-file-script", new ChoiceValue.Toggle(false)) }, NoApps, NoApps),
                            byFeature, ParityCatalog.Build, SystemIndent, UserIndent);

        SystemText(on).Should().Contain("New-Item 'C:\\on'").And.NotContain("C:\\off");
        UserText(on).Should().NotContain("New-Item");
        UserText(off).Should().Contain("New-Item 'C:\\off'");
        SystemText(off).Should().NotContain("New-Item");
        on.Warnings.Should().BeEmpty();
        off.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void AnswerFileOnlySetting_WithOnlyElements_EmitsNothingAndWarnsNothing()
    {
        var setting = new Setting
        {
            Id = "answer-file-element",
            Display = new Display { Name = TestKeys.Of("Element"), Description = TestKeys.Of("An element") },
            Targets = new Target[] { new AutounattendElement("e", "oobeSystem", "Microsoft-Windows-Shell-Setup", "OOBE/HideEULAPage") },
            States = new[]
            {
                new SettingState { Label = LocKey.Common.Enabled, Set = new Dictionary<string, StateValue> { ["e"] = StateValue.Absent } },
                new SettingState { Label = LocKey.Common.Disabled, Set = new Dictionary<string, StateValue> { ["e"] = StateValue.Of("true") } },
            },
        };
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>> { [ParityCatalog.FeatureId] = new[] { setting } };

        var r = _sut.Emit(new SelectionSet(new[] { new SettingChoice("answer-file-element", new ChoiceValue.Toggle(false)) }, NoApps, NoApps),
                          byFeature, ParityCatalog.Build, SystemIndent, UserIndent);

        r.SystemPassByFeature.Should().BeEmpty();
        r.UserPassByFeature.Should().BeEmpty();
        r.Warnings.Should().BeEmpty();
    }

    private static readonly string[] HereStringPath = [@"HKEY_LOCAL_MACHINE\SOFTWARE\ParityHereString"];

    private static readonly string[] SlideshowPath = [@"HKEY_CURRENT_USER\Control Panel\Personalization\Desktop Slideshow"];

    private static readonly string[] QuotedPath = [@"HKEY_LOCAL_MACHINE\SOFTWARE\ParityQuoted"];

    private static readonly string[] CompositePath = [@"HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences"];
    private static readonly string[] KeyDefaultPath = [@"HKEY_CURRENT_USER\Software\Classes\CLSID\{KEYDEFAULT}"];

    private static readonly string[] DetectorPath = [@"HKEY_LOCAL_MACHINE\SOFTWARE\ParityDetector"];

    private sealed class NullDetector : IStateDetector
    {
        public string? Detect(Setting setting, IDetectionContext context) => null;
    }

    private const string QuotedKey = "es-ES";

    private static readonly string[] KeyedPath = [FakeOptionProvider.ValuePath];

    private static readonly Target[] QuotedLongDateTarget =
    [
        new RegTarget(FakeOptionProvider.TargetKey, KeyedPath, FakeOptionProvider.ValueName, RegistryValueKind.String)
        {
            From = OptionValue.LongDate,
        },
    ];
}
