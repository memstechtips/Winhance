using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.AdvancedTools;

// The autounattend script for the parity catalog, frozen the day the engine-backed emitter replaced the hand-written
// ones (after a line-for-line parity run against them). A change here is a change to what every generated XML does;
// make it on purpose, re-capture, and tell Marco.
public class AutounattendScriptBuilderGoldenTests
{
    private readonly Mock<ILogService> _log = new();
    private readonly Mock<IPowerShellRunner> _ps = new();
    private readonly Mock<IWindowsVersionService> _version = new();

    public AutounattendScriptBuilderGoldenTests()
    {
        _version.Setup(v => v.GetWindowsBuildNumber()).Returns(ParityCatalog.Build.Build);
        _version.Setup(v => v.GetWindowsBuildRevision()).Returns(ParityCatalog.Build.Revision);
        _ps.Setup(p => p.ValidateScriptSyntaxAsync(It.IsAny<string>(), default)).Returns(Task.CompletedTask);
    }

    private AutounattendScriptBuilder Builder() => new(_log.Object, _ps.Object, _version.Object);

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
        return new SelectionSet(choices, Array.Empty<AppChoice>(), Array.Empty<AppChoice>(), AutounattendChoices.None);
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

        AssertSameText(Block(script, "    # POWER PLAN & POWERCFG SETTINGS", "    # START MENU LAYOUT"), GoldenSystemPass);
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

    private const string GoldenSystemPass = """
    # POWER PLAN & POWERCFG SETTINGS
    # ============================================================================

    Write-Log "Enabling hidden power settings..." "INFO"
    $PowerSettingsBasePath = "HKLM:\SYSTEM\CurrentControlSet\Control\Power\PowerSettings"
    $hiddenSettings = @(
        @{ Subgroup = "2a737441-1930-4402-8d77-b2bebba308a3"; Setting = "0853a681-27c8-4100-a2fd-82013e970683" },
        @{ Subgroup = "2a737441-1930-4402-8d77-b2bebba308a3"; Setting = "d4e98f31-5ffe-4ce1-be31-1b38b384c009" },
        @{ Subgroup = "4f971e89-eebd-4455-a8de-9e59040e7347"; Setting = "7648efa3-dd9c-4e3e-b566-50f929386280" },
        @{ Subgroup = "4f971e89-eebd-4455-a8de-9e59040e7347"; Setting = "96996bc0-ad50-47ec-923b-6f41874dd9eb" },
        @{ Subgroup = "4f971e89-eebd-4455-a8de-9e59040e7347"; Setting = "5ca83367-6e45-459f-a27b-476b1d01c936" },
        @{ Subgroup = "54533251-82be-4824-96c1-47b60b740d00"; Setting = "94d3a615-a899-4ac5-ae2b-e4d8f634367f" },
        @{ Subgroup = "54533251-82be-4824-96c1-47b60b740d00"; Setting = "be337238-0d82-4146-a960-4f3749d470c7" },
        @{ Subgroup = "54533251-82be-4824-96c1-47b60b740d00"; Setting = "465e1f50-b610-473a-ab58-00d1077dc418" },
        @{ Subgroup = "54533251-82be-4824-96c1-47b60b740d00"; Setting = "40fbefc7-2e9d-4d25-a185-0cfd8574bac6" },
        @{ Subgroup = "54533251-82be-4824-96c1-47b60b740d00"; Setting = "0cc5b647-c1df-4637-891a-dec35c318583" },
        @{ Subgroup = "54533251-82be-4824-96c1-47b60b740d00"; Setting = "ea062031-0e34-4ff1-9b6d-eb1059334028" },
        @{ Subgroup = "54533251-82be-4824-96c1-47b60b740d00"; Setting = "36687f9e-e3a5-4dbf-b1dc-15eb381c6863" },
        @{ Subgroup = "54533251-82be-4824-96c1-47b60b740d00"; Setting = "06cadf0e-64ed-448a-8927-ce7bf90eb35d" },
        @{ Subgroup = "54533251-82be-4824-96c1-47b60b740d00"; Setting = "12a0ab44-fe28-4fa9-b3bd-4b64f44960a6" }
    )

    $enabledCount = 0
    foreach ($item in $hiddenSettings) {
        $regPath = Join-Path $PowerSettingsBasePath "$($item.Subgroup)\$($item.Setting)"
        try {
            if (Test-Path $regPath) {
                Set-ItemProperty -Path $regPath -Name "Attributes" -Value 0 -Type DWord -ErrorAction Stop
                $enabledCount++
            }
        } catch {
        }
    }
    Write-Log "Enabled $enabledCount hidden power settings" "SUCCESS"

    Write-Log "Applying power settings..." "INFO"

    $settings = @(
        @{ S="2a737441-1930-4402-8d77-b2bebba308a3"; G="0853a681-27c8-4100-a2fd-82013e970683"; AC=1; DC=0; N="Powercfg selection description" },
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

    Unlock-RegistryKey -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\ParitySvc' -Description 'Locked key description'
    Set-RegistryValue -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\ParitySvc' -Name 'Start' -Type 'DWord' -Value 4 -Description 'Locked key description'
    Lock-RegistryKey -Path 'HKLM:\SYSTEM\CurrentControlSet\Services\ParitySvc' -Description 'Locked key description'
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
