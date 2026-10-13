using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.Infrastructure.Features.Autounattend.Services;
using Xunit;
using Winhance.TestSupport;

namespace Winhance.Infrastructure.Tests.Autounattend;

// The autounattend script for the parity catalog, frozen the day the engine-backed emitter replaced the hand-written
// ones (after a line-for-line parity run against them). A change here is a change to what every generated XML does;
// make it on purpose, and re-capture.
public class WinhancementsScriptBuilderGoldenTests
{
    private readonly Mock<ILogService> _log = new();
    private readonly Mock<IPowerShellRunner> _ps = new();
    private readonly Mock<IWindowsVersionService> _version = new();

    public WinhancementsScriptBuilderGoldenTests()
    {
        _version.Setup(v => v.GetWindowsBuildNumber()).Returns(ParityCatalog.Build.Build);
        _version.Setup(v => v.GetWindowsBuildRevision()).Returns(ParityCatalog.Build.Revision);
        _ps.Setup(p => p.ValidateScriptSyntaxAsync(It.IsAny<string>(), default)).Returns(Task.CompletedTask);
    }

    private WinhancementsScriptBuilder Builder() =>
        new(_log.Object, _ps.Object, _version.Object, new Mock<ILocalizationService>().Object, new Mock<IOptionProviderRegistry>().Object);

    private static readonly string[] PowerCfgIds = ["parity-powercfg-selection", "parity-slider"];

    // Every parity setting at its "on"/first-option value.
    internal static SelectionSet ParitySet(bool withPowerCfg = true)
    {
        var choices = new List<SettingChoice>
        {
            new("parity-toggle-hklm", new ChoiceValue.Toggle(true)),
            new("parity-toggle-hkcu-delete", new ChoiceValue.Toggle(false)),
            new("parity-key-exists", new ChoiceValue.Toggle(true)),
            new("parity-bit", new ChoiceValue.Toggle(true)),
            new("parity-byte", new ChoiceValue.Toggle(true)),
            new("parity-per-subkey", new ChoiceValue.Toggle(true)),
            new("parity-scripts", new ChoiceValue.Toggle(true)),
            new("parity-regcontent", new ChoiceValue.Toggle(true)),
            new("parity-task", new ChoiceValue.Toggle(false)),
            new("parity-selection", new ChoiceValue.Option(2)),
            new("parity-powercfg-selection", new ChoiceValue.AcDcOption(1, 0)),
            new("parity-slider", new ChoiceValue.AcDcNumber(600, 300)),
            new("parity-composite", new ChoiceValue.Toggle(true)),
            new("parity-string-flag", new ChoiceValue.Toggle(true)),
            new("parity-lock", new ChoiceValue.Toggle(true)),
            new("parity-resetset", new ChoiceValue.Toggle(true)),
            new("parity-action", new ChoiceValue.Toggle(true)),
        };
        if (!withPowerCfg) choices.RemoveAll(c => PowerCfgIds.Contains(c.SettingId));
        return new SelectionSet(choices, Array.Empty<AppChoice>(), Array.Empty<AppChoice>());
    }

    private static string Block(string script, string start, string end)
    {
        var s = script.Replace("\r\n", "\n");
        int a = s.IndexOf(start, StringComparison.Ordinal);
        a.Should().BeGreaterThanOrEqualTo(0, $"the script must contain '{start}'");
        int b = s.IndexOf(end, a, StringComparison.Ordinal);
        b.Should().BeGreaterThan(a, $"the script must contain '{end}' after '{start}'");
        return s[a..b].TrimEnd('\n');
    }

    // Line endings are normalized on both sides (the golden is stored with the source file's endings); the first
    // differing character is reported with its neighbourhood because FluentAssertions truncates long strings.
    private static void AssertSameText(string actual, string golden)
    {
        var expected = golden.Replace("\r\n", "\n").TrimEnd('\n');
        if (actual == expected) return;
        int i = 0;
        while (i < actual.Length && i < expected.Length && actual[i] == expected[i]) i++;
        static string Show(string s, int at) => s[Math.Max(0, at - 40)..Math.Min(s.Length, at + 40)].Replace("\r", "<CR>").Replace("\n", "<LF>\n");
        throw new Xunit.Sdk.XunitException(
            $"Script differs from the golden at index {i} (actual length {actual.Length}, golden {expected.Length}).\nACTUAL:\n{Show(actual, i)}\nGOLDEN:\n{Show(expected, i)}");
    }

    [Fact]
    public async Task SystemPass_PowerSectionAndFeatureBlock_MatchTheGolden()
    {
        var script = await Builder().BuildAsync(ParitySet(), ParityCatalog.ByFeature);

        AssertSameText(Block(script, "    # POWER PLAN & POWERCFG SETTINGS", "    # USER CUSTOMIZATIONS SCHEDULED TASK"), GoldenSystemPass);
    }

    [Fact]
    public async Task UserPass_FeatureBlock_MatchesTheGolden()
    {
        var script = await Builder().BuildAsync(ParitySet(), ParityCatalog.ByFeature);

        AssertSameText(Block(script, "            # EXPLORER SETTINGS", "            # ADD YOUR USER SPECIFIC POWERSHELL SCRIPT CONTENTS BELOW"), GoldenUserPass);
    }

    [Fact]
    public async Task WithNoPowerChoices_EmitsNoPowerSection()
    {
        var script = await Builder().BuildAsync(ParitySet(withPowerCfg: false), ParityCatalog.ByFeature);

        script.Should().NotContain("# POWER PLAN & POWERCFG SETTINGS");
    }

    [Fact]
    public async Task ValidatesTheScriptSyntax_AndRethrowsOnFailure()
    {
        _ps.Setup(p => p.ValidateScriptSyntaxAsync(It.IsAny<string>(), default)).ThrowsAsync(new InvalidOperationException("bad"));

        var act = () => Builder().BuildAsync(ParitySet(), ParityCatalog.ByFeature);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task PowerPlanChoice_EmitsPlanCreationAndActivation()
    {
        var plan = new Setting { Id = "power-plan-selection", Display = new() { Name = TestKeys.Of("plan"), Description = TestKeys.Of("plan") }, Options = new(OptionSource.PowerPlans) };
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>(ParityCatalog.ByFeature) { [FeatureIds.Power] = new[] { plan } };
        var choices = ParitySet().Settings.Append(new SettingChoice(plan.Id, new ChoiceValue.Keyed("11111111-2222-3333-4444-555555555555", "Winhance Power Plan"))).ToList();

        var script = await Builder().BuildAsync(new SelectionSet(choices, Array.Empty<AppChoice>(), Array.Empty<AppChoice>()), byFeature);

        script.Should().Contain("$customPlanGuid = \"11111111-2222-3333-4444-555555555555\"");
        script.Should().Contain("powercfg /changename $customPlanGuid \"Winhance Power Plan\"");
        script.Should().Contain("$targetPlanGuid = $customPlanGuid");
        script.Should().Contain("powercfg /setactive $customPlanGuid 2>$null");
    }

    [Fact]
    public async Task PowerPlanChoice_WithNoPowerSettings_StillActivatesThePlan()
    {
        var plan = new Setting { Id = "power-plan-selection", Display = new() { Name = TestKeys.Of("plan"), Description = TestKeys.Of("plan") }, Options = new(OptionSource.PowerPlans) };
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>> { [FeatureIds.Power] = new[] { plan } };
        var choices = new[] { new SettingChoice(plan.Id, new ChoiceValue.Keyed("11111111-2222-3333-4444-555555555555", "High Performance")) };

        var script = await Builder().BuildAsync(new SelectionSet(choices, Array.Empty<AppChoice>(), Array.Empty<AppChoice>()), byFeature);

        script.Should().NotContain("Applying power settings...");
        script.Should().Contain("powercfg /setactive $customPlanGuid 2>$null");
        // /query also succeeds on a hidden template (Ultimate Performance), which then never gets created or activated.
        script.Should().NotContain("powercfg /query");
        script.Should().Contain("powercfg /list | Select-String -SimpleMatch $customPlanGuid");
    }

    private const string GoldenSystemPass = """
    # POWER PLAN & POWERCFG SETTINGS
    # ============================================================================

    Write-Log "Applying power settings..." "INFO"

    $settings = @(
        @{ S="2a737441-1930-4402-8d77-b2bebba308a3"; G="0853a681-27c8-4100-a2fd-82013e970683"; AC=900; DC=300; N="Powercfg selection description" },
        @{ S="7516b95f-f776-4464-8c53-06167f40cc99"; G="3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e"; AC=600; DC=300; N="Powercfg slider description" }
    )

    $appliedCount = 0
    $targetPlanGuid = "SCHEME_CURRENT"
    foreach ($setting in $settings) {
        try {
            powercfg /setacvalueindex $targetPlanGuid $setting.S $setting.G $setting.AC 2>$null
            if ($LASTEXITCODE -eq 0) {
                powercfg /setdcvalueindex $targetPlanGuid $setting.S $setting.G $setting.DC 2>$null
                if ($LASTEXITCODE -eq 0) {
                    $appliedCount++
                }
            }
        } catch {
        }
    }
    Write-Log "Applied $appliedCount power settings" "SUCCESS"


    # ============================================================================
    # EXPLORER SETTINGS
    # ============================================================================

    Set-RegistryValue -Path 'HKLM:\SOFTWARE\Parity' -Name 'V' -Type 'DWord' -Value 1 -Description 'Toggle HKLM description'
    Get-ChildItem -Path 'HKLM:\SYSTEM\Parity\Interfaces' -ErrorAction SilentlyContinue | ForEach-Object {
        Set-RegistryValue -Path $_.PSPath -Name 'N' -Type 'DWord' -Value 1 -Description 'Per subkey description'
    }
    Set-RegistryValue -Path 'HKLM:\SOFTWARE\ParityScripts' -Name 'S' -Type 'DWord' -Value 1 -Description 'Scripts description'

    # PowerShell script for: Scripts
    try {
        Write-Host 'system side'
        Write-Log "Scripts description" "SUCCESS"
    } catch {
        Write-Log "Failed: Scripts description - $($_.Exception.Message)" "ERROR"
    }

    if (Test-Path 'HKLM:\SYSTEM\CurrentControlSet\Services\ParitySvc') {
        Unlock-RegistryKey -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\ParitySvc' -Description 'Locked key description'
    }
    if (Test-Path 'HKLM:\SYSTEM\CurrentControlSet\Services\ParitySvc') {
        Set-RegistryValue -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\ParitySvc' -Name 'Start' -Type 'DWord' -Value 4 -Description 'Locked key description'
    } else {
        Write-Log 'Locked key description | skipped, this PC has no ParitySvc service' "INFO"
    }
    if (Test-Path 'HKLM:\SYSTEM\CurrentControlSet\Services\ParitySvc') {
        Lock-RegistryKey -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\ParitySvc' -Description 'Locked key description'
    }
    Set-RegistryValue -Path 'HKLM:\SOFTWARE\ParityAction' -Name 'Ran' -Type 'DWord' -Value 1 -Description 'Action description'

    # PowerShell script for: Action
    try {
        Write-Host 'action script'
        Write-Log "Action description" "SUCCESS"
    } catch {
        Write-Log "Failed: Action description - $($_.Exception.Message)" "ERROR"
    }


    $scheduledTasks = @(
        @{ TN="\Microsoft\Windows\Parity\Task"; Action="/Disable"; Desc="Scheduled task description" }
    )

    Write-Log "Applying scheduled task settings..." "INFO"
    $processedCount = 0
    foreach ($task in $scheduledTasks) {
        try {
            $null = & cmd.exe /c "schtasks /Query /TN `"$($task.TN)`"" 2>&1
            if ($LASTEXITCODE -ne 0) {
                Write-Log "$($task.Desc) | skipped, this PC has no $($task.TN) task" "INFO"
                continue
            }
            $result = & cmd.exe /c "schtasks /Change /TN `"$($task.TN)`" $($task.Action)" 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-Log "$($task.Desc)" "SUCCESS"
                $processedCount++
            } else {
                Write-Log "Task command failed for: $($task.Desc)" "WARNING"
            }
        } catch {
            Write-Log "Failed to process task: $($task.Desc) - $($_.Exception.Message)" "ERROR"
        }
    }
    Write-Log "Processed $processedCount scheduled task settings" "SUCCESS"


    # ============================================================================
""";

    private const string GoldenUserPass = """
            # EXPLORER SETTINGS
            # ============================================================================

            Remove-RegistryValue -Path 'HKCU:\Software\Parity' -Name 'V' -Description 'Toggle HKCU delete description'
            New-RegistryKey -Path 'HKCU:\Software\Classes\CLSID\{PARITY}' -Description 'Key existence description'
            Set-BinaryBit -Path 'HKCU:\Control Panel\Desktop' -Name 'UserPreferencesMask' -ByteIndex 4 -BitMask 0x20 -SetBit $True -Description 'Binary bit description'
            Set-BinaryByte -Path 'HKCU:\Control Panel\Desktop' -Name 'MenuShowDelay' -ByteIndex 0 -ByteValue 0x03 -Description 'Binary byte description'

            # PowerShell script for: Scripts
            try {
                Write-Host 'user side'
                Write-Log "Scripts description" "SUCCESS"
            } catch {
                Write-Log "Failed: Scripts description - $($_.Exception.Message)" "ERROR"
            }

            try {
                $regContent_parity_regcontent = @'
Windows Registry Editor Version 5.00

[HKEY_CURRENT_USER\Software\ParityReg]
"Imported"=dword:00000001

'@
                $tempRegFile = Join-Path $env:TEMP "winhance_parity-regcontent_$((Get-Date).Ticks).reg"
                $regContent_parity_regcontent | Out-File -FilePath $tempRegFile -Encoding Unicode -Force
                reg import "$tempRegFile" 2>&1 | Out-Null
                if ($LASTEXITCODE -eq 0) {
                    Write-Log "Reg content description" "SUCCESS"
                } else {
                    Write-Log "Failed to import registry content for Reg content description" "ERROR"
                }
                Remove-Item $tempRegFile -Force -ErrorAction SilentlyContinue
            } catch {
                Write-Log "Error processing registry content for Reg content description: $($_.Exception.Message)" "ERROR"
            }

            Set-RegistryValue -Path 'HKCU:\Software\ParitySel' -Name 'Mode' -Type 'DWord' -Value 2 -Description 'Selection description'
            Set-RegistryCompositeValue -Path 'HKCU:\Software\Microsoft\DirectX\UserGpuPreferences' -Name 'DirectXUserGlobalSettings' -Key 'SwapEffectUpgradeEnable' -SubValue '1' -Description 'Composite string description'
            Set-RegistryStringFlag -Path 'HKCU:\Control Panel\Accessibility\MouseKeys' -Name 'Flags' -FlagMask 4 -AbsentBase 62 -Set $True -Description 'String flag description'
            Remove-RegistryValue -Path 'HKCU:\Software\ParityReset' -Name 'E' -Description 'Reset set description'

            # ============================================================================
""";
}
