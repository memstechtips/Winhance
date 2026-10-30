using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Selections;
using Winhance.Infrastructure.Features.Autounattend.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Autounattend;

public class WinhancementsScriptBuilderTests
{
    private const string InstallWinhanceScript = @"C:\ProgramData\Winhance\Unattend\Scripts\Install-Winhance.ps1";

    private static readonly string[] CortanaPackage = ["Microsoft.549981C3F5F10"];

    private static readonly string[] RegistryHelpers =
    [
        "Set-RegistryValue", "Remove-RegistryValue", "Remove-RegistryKey", "New-RegistryKey", "Set-BinaryBit", "Set-BinaryByte",
        "Set-RegistryCompositeValue", "Set-RegistryStringFlag", "Open-RegistryKeyForAcl", "Unlock-RegistryKey", "Lock-RegistryKey",
    ];

    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IPowerShellRunner> _powerShellRunner = new();
    private readonly Mock<IWindowsVersionService> _windowsVersionService = new();
    private readonly WinhancementsScriptBuilder _sut;

    public WinhancementsScriptBuilderTests()
    {
        _powerShellRunner.Setup(s => s.ValidateScriptSyntaxAsync(It.IsAny<string>(), default))
            .Returns(Task.CompletedTask);

        _sut = new WinhancementsScriptBuilder(
            _logService.Object,
            _powerShellRunner.Object,
            _windowsVersionService.Object,
            new Mock<ILocalizationService>().Object,
            new Mock<IOptionProviderRegistry>().Object);
    }

    private static SelectionSet SetOf(params SettingChoice[] choices) =>
        new(choices, Array.Empty<AppChoice>(), Array.Empty<AppChoice>());

    private static SettingChoice Scripts(string cardId, params (int Type, string Script)[] rows) =>
        new(cardId, new ChoiceValue.List(rows.Select(r => new ChoiceValue.ListRow(
            new Dictionary<string, string> { ["type"] = r.Type.ToString(), ["script"] = r.Script })).ToList()));

    // The SYSTEM side's branch for a user whose customizations are already applied: the sign-in after Setup's restart.
    private static string AlreadyAppliedBranch(string script)
    {
        var start = script.IndexOf("        if ($alreadyApplied) {", StringComparison.Ordinal);
        return script[start..script.IndexOf("        } else {", start, StringComparison.Ordinal)];
    }

    [Fact]
    public async Task BuildAsync_EmptySet_ProducesValidScript()
    {
        var set = SelectionSet.Empty;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task BuildAsync_ContainsHeader()
    {
        var set = SelectionSet.Empty;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain(".SYNOPSIS");
        result.Should().Contain("param(");
    }

    [Fact]
    public async Task BuildAsync_ContainsLoggingSetup()
    {
        var set = SelectionSet.Empty;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain("function Write-Log");
        result.Should().Contain("$LogPath");
    }

    [Fact]
    public async Task BuildAsync_SystemPath_ExitsFirstWhileWindowsUpdatesAreStaged()
    {
        var result = await _sut.BuildAsync(SelectionSet.Empty, new Dictionary<string, IReadOnlyList<Setting>>());

        var guard = result.IndexOf($"if (Test-Path '{ScriptPaths.UnattendUpdatesDirectory}') {{", StringComparison.Ordinal);
        guard.Should().BeGreaterThan(0);
        result.IndexOf("UserCustomizations running as SYSTEM", StringComparison.Ordinal).Should().BeGreaterThan(guard);
        result[guard..result.IndexOf("UserCustomizations running as SYSTEM", StringComparison.Ordinal)]
            .Should().Contain("user customizations run after their restart").And.Contain("exit 0");
    }

    [Fact]
    public async Task BuildAsync_EmptySet_CarriesOnlyTheHelpersTheUserBridgeCalls()
    {
        var result = await _sut.BuildAsync(SelectionSet.Empty, new Dictionary<string, IReadOnlyList<Setting>>());

        result.Should().Contain("function Get-TargetUser {")
            .And.Contain("function Get-UserSID {")
            .And.Contain("function Start-ProcessAsUser {");
        foreach (var helper in RegistryHelpers)
            result.Should().NotContain($"function {helper} {{", helper);
    }

    [Fact]
    public async Task BuildAsync_ARegistryWrite_CarriesSetRegistryValueAndNoOtherRegistryHelper()
    {
        var set = SetOf(new SettingChoice("security-remote-assistance", new ChoiceValue.Toggle(true)));
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>
        {
            { FeatureIds.Privacy, new[] { SettingCatalog.Find("security-remote-assistance")! } }
        };

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain("function Set-RegistryValue {");
        foreach (var helper in RegistryHelpers.Where(h => h != "Set-RegistryValue"))
            result.Should().NotContain($"function {helper} {{", helper);
    }

    [Fact]
    public async Task BuildAsync_ContainsSystemBlock()
    {
        var set = SelectionSet.Empty;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain("if (-not $UserCustomizations)");
    }

    [Fact]
    public async Task BuildAsync_ContainsUserBlock()
    {
        var set = SelectionSet.Empty;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain("if ($UserCustomizations)");
    }

    [Fact]
    public async Task BuildAsync_ContainsCompletionBlock()
    {
        var set = SelectionSet.Empty;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain("Script Completed");
    }

    [Fact]
    public async Task BuildAsync_ContainsCustomScriptPlaceholders()
    {
        var set = SelectionSet.Empty;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain("SYSTEM WIDE");
        result.Should().Contain("USER SPECIFIC");
        result.Should().Contain("# Start here");
        result.Should().Contain("# End here");
    }

    [Fact]
    public async Task BuildAsync_RunsEachUserScriptFileByItsType()
    {
        var set = SetOf(
            Scripts("autounattend-user-script-system", (0, "Write-Host 'mine'"), (3, "Windows Registry Editor Version 5.00")),
            Scripts("autounattend-user-script-user", (1, "echo theirs")));

        var result = await _sut.BuildAsync(set, new Dictionary<string, IReadOnlyList<Setting>>());

        var dir = ScriptPaths.UnattendUserScriptsDirectory;
        result.Should().Contain($"    & \"{dir}\\System-1.ps1\"");
        result.Should().Contain($"    & reg.exe import \"{dir}\\System-2.reg\"");
        result.Should().Contain($"            & cmd.exe /c \"{dir}\\User-1.cmd\"");
        result.Should().NotContain("Write-Host 'mine'", because: "the script travels in its own file");
    }

    [Fact]
    public async Task BuildAsync_WithNoUserScript_LeavesThePlaceholderEmpty()
    {
        var result = await _sut.BuildAsync(SelectionSet.Empty, new Dictionary<string, IReadOnlyList<Setting>>());

        result.Should().NotContain("Running your own script");
    }

    [Fact]
    public async Task BuildAsync_AUserScriptCallingAHelperCarriesThatHelper()
    {
        var set = SetOf(Scripts("autounattend-user-script-system",
            (0, @"Set-RegistryValue -Path 'HKLM:\Software\Mine' -Name 'On' -Type 'DWord' -Value 1")));

        var result = await _sut.BuildAsync(set, new Dictionary<string, IReadOnlyList<Setting>>());

        result.Should().Contain("function Set-RegistryValue");
    }

    [Fact]
    public async Task BuildAsync_SetsUpTheScriptsDirectoryOnlyForAppRemoval()
    {
        var removal = new SelectionSet(
            Array.Empty<SettingChoice>(),
            new[] { new AppChoice("windows-app-cortana", "Cortana", CortanaPackage, null, null, null) },
            Array.Empty<AppChoice>());
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        var withApps = await _sut.BuildAsync(removal, byFeature);
        var without = await _sut.BuildAsync(SelectionSet.Empty, byFeature);

        withApps.Should().Contain("$scriptsDir = ");
        without.Should().NotContain("$scriptsDir");
    }

    [Fact]
    public async Task BuildAsync_WritesTheShortcutOnlyWhenTheCardPicksIt()
    {
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>
        {
            [FeatureIds.Autounattend] = SettingCatalog.ByFeature[FeatureIds.Autounattend],
        };

        var on = await _sut.BuildAsync(SetOf(new SettingChoice("autounattend-desktop-shortcut", new ChoiceValue.Option(1))), byFeature);
        var off = await _sut.BuildAsync(SetOf(new SettingChoice("autounattend-desktop-shortcut", new ChoiceValue.Option(0))), byFeature);

        on.Should().Contain("# AUTOUNATTEND SETTINGS").And.Contain("Install Winhance.lnk");
        off.Should().NotContain("Install Winhance.lnk").And.NotContain("# AUTOUNATTEND SETTINGS");
    }

    [Fact]
    public async Task BuildAsync_HandsTheChosenExternalAppsToTheInstallScript()
    {
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>
        {
            [FeatureIds.Autounattend] = SettingCatalog.ByFeature[FeatureIds.Autounattend],
        };
        var set = new SelectionSet(
            [new SettingChoice("autounattend-desktop-shortcut", new ChoiceValue.Option(1))],
            Array.Empty<AppChoice>(),
            [new AppChoice("google-chrome", "Google Chrome", null, null, null, "Google.Chrome")]);

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain("$apps = 'google-chrome'").And.NotContain("{{external-app-ids}}");
    }

    // With nothing to install, the shortcut runs the web installer rather than Install-Winhance.ps1.
    [Fact]
    public async Task BuildAsync_WithNothingToInstall_WritesTheWebInstallerShortcut()
    {
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>
        {
            [FeatureIds.Autounattend] = SettingCatalog.ByFeature[FeatureIds.Autounattend],
        };

        var result = await _sut.BuildAsync(SetOf(new SettingChoice("autounattend-desktop-shortcut", new ChoiceValue.Option(1))), byFeature);

        result.Should().Contain("irm 'https://get.winhance.net' | iex").And.NotContain("Install-Winhance.ps1");
    }

    [Fact]
    public async Task BuildAsync_UserFoldersByRole_FindsTheDriveAtSetupAndMovesTheFoldersAtSignIn()
    {
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>
        {
            [FeatureIds.Autounattend] = SettingCatalog.ByFeature[FeatureIds.Autounattend],
        };

        var result = await _sut.BuildAsync(SetOf(new SettingChoice("autounattend-user-folders", new ChoiceValue.Option(1))), byFeature);

        // The preamble opens with its own if ($UserCustomizations) guard, so the user block is the last one.
        var systemBlockIndex = result.IndexOf("if (-not $UserCustomizations) {", StringComparison.Ordinal);
        var userBlockIndex = result.LastIndexOf("if ($UserCustomizations) {", StringComparison.Ordinal);
        systemBlockIndex.Should().BeGreaterThan(0);
        result.IndexOf("'Mode', 'Role'", StringComparison.Ordinal).Should().BeInRange(systemBlockIndex, userBlockIndex);
        result.IndexOf("'Letter', 'D'", StringComparison.Ordinal).Should().BeInRange(systemBlockIndex, userBlockIndex);
        result.IndexOf(@"ExpandEnvironmentVariables('Users\%USERNAME%')", StringComparison.Ordinal).Should().BeGreaterThan(userBlockIndex);
        result.Should().Contain("User Shell Folders").And.NotContain("{{value}}");
        result.Should().NotMatchRegex(@"'Mode', 'Role'\)\s*Write-Log", "the scripts log their own outcome");
    }

    [Fact]
    public async Task BuildAsync_UserFoldersByRole_LeavesOutTheLabelTheOtherModeHides()
    {
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>
        {
            [FeatureIds.Autounattend] = SettingCatalog.ByFeature[FeatureIds.Autounattend],
        };

        var result = await _sut.BuildAsync(SetOf(
            new SettingChoice("autounattend-user-folders", new ChoiceValue.Option(1)),
            new SettingChoice("autounattend-user-folders-label-typed", new ChoiceValue.Text("Data"))), byFeature);

        result.Should().Contain("'Mode', 'Role'").And.NotContain("'Label', 'Data'");
    }

    // The answer file carries only what it uses: kept in C:\Users, or a set that never reached the card, writes nothing.
    [Fact]
    public async Task BuildAsync_LeavesTheUserFoldersOutWhenTheyStayOnC()
    {
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>
        {
            [FeatureIds.Autounattend] = SettingCatalog.ByFeature[FeatureIds.Autounattend],
        };

        var kept = await _sut.BuildAsync(SetOf(new SettingChoice("autounattend-user-folders", new ChoiceValue.Option(0))), byFeature);
        var untouched = await _sut.BuildAsync(SelectionSet.Empty, byFeature);

        kept.Should().NotContain("User Shell Folders").And.NotContain(@"Winhance\UserFolders");
        untouched.Should().NotContain("User Shell Folders").And.NotContain(@"Winhance\UserFolders");
    }

    [Fact]
    public async Task BuildAsync_EmptySet_RunsNothingAtTheSignInAfterSetup()
    {
        var result = await _sut.BuildAsync(SelectionSet.Empty, new Dictionary<string, IReadOnlyList<Setting>>());

        AlreadyAppliedBranch(result).Should().NotContain("Start-ProcessAsUser");
        result.Should().NotContain("InstallWinhanceAtSignIn");
    }

    [Fact]
    public async Task BuildAsync_InstallAutomatically_RunsTheInstallScriptAtTheSignInAfterSetup()
    {
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>
        {
            [FeatureIds.Autounattend] = SettingCatalog.ByFeature[FeatureIds.Autounattend],
        };

        var result = await _sut.BuildAsync(SetOf(new SettingChoice("autounattend-desktop-shortcut", new ChoiceValue.Option(2))), byFeature);

        AlreadyAppliedBranch(result).Should().Contain("Start-ProcessAsUser").And.Contain(InstallWinhanceScript);
    }

    // The XML writers fall back to the seed state for a card the set never reached; the script agrees.
    [Fact]
    public async Task BuildAsync_ASetThatNeverReachedThePage_StillWritesTheShortcut()
    {
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>
        {
            [FeatureIds.Autounattend] = SettingCatalog.ByFeature[FeatureIds.Autounattend],
        };

        var result = await _sut.BuildAsync(SelectionSet.Empty, byFeature);

        result.Should().Contain("# AUTOUNATTEND SETTINGS").And.Contain("Install Winhance.lnk");
    }

    [Fact]
    public async Task BuildAsync_NoLongerCarriesTheUnconditionalStartMenuBlock()
    {
        var result = await _sut.BuildAsync(SelectionSet.Empty, new Dictionary<string, IReadOnlyList<Setting>>());

        result.Should().NotContain("START MENU LAYOUT").And.NotContain("ConfigureStartPins");
    }

    [Fact]
    public async Task BuildAsync_NoLongerCarriesTheUnconditionalDesktopBackgroundBlock()
    {
        var result = await _sut.BuildAsync(SelectionSet.Empty, new Dictionary<string, IReadOnlyList<Setting>>());

        result.Should().NotContain("Setting wallpaper based on Windows version");
    }

    [Fact]
    public async Task BuildAsync_ContainsUserCustomizationsTask()
    {
        var set = SelectionSet.Empty;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain("WinhanceUserCustomizations");
    }

    [Fact]
    public async Task BuildAsync_ContainsUserDetectionBridge()
    {
        var set = SelectionSet.Empty;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain("$runningAsSystem");
        result.Should().Contain("S-1-5-18");
        result.Should().Contain("UserCustomizationsApplied");
    }

    [Fact]
    public async Task BuildAsync_CallsValidateScriptSyntax()
    {
        var set = SelectionSet.Empty;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        await _sut.BuildAsync(set, byFeature);

        _powerShellRunner.Verify(r => r.ValidateScriptSyntaxAsync(
            It.IsAny<string>(), default), Times.Once);
    }

    [Fact]
    public async Task BuildAsync_SyntaxValidationFails_Throws()
    {
        _powerShellRunner.Setup(s => s.ValidateScriptSyntaxAsync(It.IsAny<string>(), default))
            .ThrowsAsync(new InvalidOperationException("Syntax error at line 42"));

        var set = SelectionSet.Empty;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        var act = () => _sut.BuildAsync(set, byFeature);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Syntax error*");
    }

    [Fact]
    public async Task BuildAsync_WithWindowsApps_EmitsAppRemoval()
    {
        var set = new SelectionSet(
            Array.Empty<SettingChoice>(),
            new[] { new AppChoice("windows-app-cortana", "Cortana", CortanaPackage, null, null, null) },
            Array.Empty<AppChoice>());
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain("WINDOWS APPS REMOVAL");
        result.Should().Contain("BloatRemoval");
    }

    [Fact]
    public async Task BuildAsync_WithOptimizeFeatures_EmitsHklmRegistryEntries()
    {
        // The pipeline runs on the catalog Setting dict, so the fixture passes the REAL
        // catalog HKLM registry toggle (security-remote-assistance, RegTarget fAllowToGetHelp) directly.
        var set = SetOf(new SettingChoice("security-remote-assistance", new ChoiceValue.Toggle(true)));
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>
        {
            { FeatureIds.Privacy, new[] { SettingCatalog.Find("security-remote-assistance")! } }
        };

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain("Set-RegistryValue");
        result.Should().Contain("fAllowToGetHelp");
    }

    [Fact]
    public async Task BuildAsync_MapsTheHkuDriveForTheSystemPassOnlyWhenItWritesThere()
    {
        var numLock = SettingCatalog.Find("devices-numlock-at-startup")!;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>> { { FeatureIds.Explorer, new[] { numLock } } };

        var withHku = await _sut.BuildAsync(SetOf(new SettingChoice(numLock.Id, new ChoiceValue.Toggle(true))), byFeature);
        var without = await _sut.BuildAsync(SetOf(), byFeature);

        var systemBlock = withHku.IndexOf("if (-not $UserCustomizations)", StringComparison.Ordinal);
        var drive = withHku.IndexOf("New-PSDrive -PSProvider Registry -Name HKU", systemBlock, StringComparison.Ordinal);
        drive.Should().BeLessThan(withHku.IndexOf(@"'HKU:\.DEFAULT", systemBlock, StringComparison.Ordinal));
        without.IndexOf("New-PSDrive", StringComparison.Ordinal)
            .Should().BeGreaterThan(without.IndexOf("if ($UserCustomizations)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BuildAsync_WithCustomizeFeatures_EmitsHkcuInUserBlock()
    {
        // The REAL catalog HKCU toggle (gaming-game-mode, RegTarget AutoGameModeEnabled under
        // HKEY_CURRENT_USER) rides the Setting dict directly. The value name lands only in the HKCU pass,
        // i.e. inside the $UserCustomizations block.
        var set = SetOf(new SettingChoice("gaming-game-mode", new ChoiceValue.Toggle(true)));
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>
        {
            { FeatureIds.GamingPerformance, new[] { SettingCatalog.Find("gaming-game-mode")! } }
        };

        var result = await _sut.BuildAsync(set, byFeature);

        var userBlockIndex = result.IndexOf("if ($UserCustomizations)");
        var custValIndex = result.IndexOf("AutoGameModeEnabled", userBlockIndex);
        custValIndex.Should().BeGreaterThan(userBlockIndex);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_file_that_removes_Game_Bar_turns_Game_DVR_off_whatever_the_card_says(bool cardOn)
    {
        var set = new SelectionSet(
            [new SettingChoice("gaming-xbox-game-dvr", new ChoiceValue.Toggle(cardOn))],
            [new AppChoice("windows-app-xbox-game-bar", "Xbox Game Bar", ["Microsoft.XboxGamingOverlay"], null, null, null)],
            Array.Empty<AppChoice>());
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>
        {
            { FeatureIds.GamingPerformance, new[] { SettingCatalog.Find("gaming-xbox-game-dvr")! } }
        };

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain("-Name 'GameDVR_Enabled' -Type 'DWord' -Value 0");
        result.Should().NotContain("-Name 'GameDVR_Enabled' -Type 'DWord' -Value 1");
    }

    [Fact]
    public async Task The_Winhance_plan_in_a_file_is_a_copy_of_Balanced_like_in_the_app()
    {
        var set = SetOf(new SettingChoice("power-plan-selection",
            new ChoiceValue.Keyed(PowerPlanCatalog.WinhancePowerPlanGuid, "Winhance Power Plan")));
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>
        {
            { FeatureIds.Power, new[] { SettingCatalog.Find("power-plan-selection")! } }
        };

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain($"powercfg /duplicatescheme {PowerPlanCatalog.BalancedGuid} $customPlanGuid");
        result.Should().NotContain("e9a42b02-d5df-448d-aa00-03f14749eb61");
    }

    [Fact]
    public async Task A_file_that_keeps_Game_Bar_leaves_Game_DVR_to_the_card()
    {
        var set = new SelectionSet(
            [new SettingChoice("gaming-xbox-game-dvr", new ChoiceValue.Toggle(true))],
            [new AppChoice("windows-app-cortana", "Cortana", CortanaPackage, null, null, null)],
            Array.Empty<AppChoice>());
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>
        {
            { FeatureIds.GamingPerformance, new[] { SettingCatalog.Find("gaming-xbox-game-dvr")! } }
        };

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain("-Name 'GameDVR_Enabled' -Type 'DWord' -Value 1");
    }

    [Fact]
    public async Task BuildAsync_ValidSyntax_LogsSuccess()
    {
        var set = SelectionSet.Empty;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        await _sut.BuildAsync(set, byFeature);

        _logService.Verify(l => l.Log(
            LogLevel.Info,
            It.Is<string>(s => s.Contains("passed PowerShell syntax validation")),
            null, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task BuildAsync_FailedSyntax_LogsError()
    {
        _powerShellRunner.Setup(s => s.ValidateScriptSyntaxAsync(It.IsAny<string>(), default))
            .ThrowsAsync(new InvalidOperationException("Bad syntax"));

        var set = SelectionSet.Empty;
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>();

        try { await _sut.BuildAsync(set, byFeature); }
        catch { }

        _logService.Verify(l => l.Log(
            LogLevel.Error,
            It.Is<string>(s => s.Contains("failed PowerShell syntax validation")),
            null, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task BuildAsync_SettingDict_RealPowerSetting_EmitsCatalogPowerCfgTargets()
    {
        // A REAL catalog power setting rides the Setting dict end-to-end. A powercfg-only setting never reaches
        // either registry pass: the power section writes one @{ S=..; G=..; AC=..; DC=.. } row per catalog
        // PowerCfgTarget instead.
        var set = SetOf(new SettingChoice("power-display-timeout", new ChoiceValue.AcDcOption(1, 0)));
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>
        {
            { FeatureIds.Power, new[] { SettingCatalog.Find("power-display-timeout")! } }
        };

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain("# POWER PLAN & POWERCFG SETTINGS");
        result.Should().Contain("S=\"7516b95f-f776-4464-8c53-06167f40cc99\"; G=\"3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e\"");
    }

    [Fact]
    public async Task BuildAsync_UnhidesOnlyTheHiddenPowerSettingsItWrites()
    {
        // The USB hub timeout is one Windows hides (its target carries an EnablementKey); the display timeout is not.
        var set = SetOf(
            new SettingChoice("usb-hub-selective-suspend-timeout", new ChoiceValue.AcDcNumber(1000, 1000)),
            new SettingChoice("power-display-timeout", new ChoiceValue.AcDcOption(1, 0)));
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>
        {
            {
                FeatureIds.Power,
                new[]
                {
                    SettingCatalog.Find("usb-hub-selective-suspend-timeout")!,
                    SettingCatalog.Find("power-display-timeout")!,
                }
            }
        };

        var result = await _sut.BuildAsync(set, byFeature);

        var start = result.IndexOf("$hiddenSettings = @(", StringComparison.Ordinal);
        var unhidden = result[start..result.IndexOf("$enabledCount = 0", start, StringComparison.Ordinal)];
        unhidden.Should().Contain("@{ Subgroup = \"2a737441-1930-4402-8d77-b2bebba308a3\"; Setting = \"0853a681-27c8-4100-a2fd-82013e970683\" }");
        unhidden.Split("Subgroup = ").Should().HaveCount(2, "only the USB hub timeout is hidden");
    }

    [Fact]
    public async Task BuildAsync_WithNoHiddenPowerSetting_UnhidesNothing()
    {
        var set = SetOf(new SettingChoice("power-display-timeout", new ChoiceValue.AcDcOption(1, 0)));
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>
        {
            { FeatureIds.Power, new[] { SettingCatalog.Find("power-display-timeout")! } }
        };

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain("# POWER PLAN & POWERCFG SETTINGS").And.NotContain("$hiddenSettings");
    }

    [Fact]
    public async Task BuildAsync_SettingDict_AliasedConfigId_EmitsViaMergedCatalogSetting()
    {
        // A set restored from a Windows-10 export carries the retired "-win10" This PC folder item id, while the
        // Setting dict (fed straight from the catalog registry) carries only the MERGED catalog setting under its
        // canonical id. What this fact PINS red-on-mutation is the emitter's alias-NORMALIZED choice lookup - so
        // the toggle still emits, via the Win10 KeyExists target: the ctor's IWindowsVersionService mock reports
        // build 0, which falls inside BuildRange.Windows10, so the threaded build drops the Win11
        // HiddenByDefault target.
        var set = SetOf(new SettingChoice("explorer-customization-thispc-folder-desktop-win10", new ChoiceValue.Toggle(true)));
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>
        {
            { FeatureIds.Explorer, new[] { SettingCatalog.Find("explorer-customization-thispc-folder-desktop")! } }
        };

        var result = await _sut.BuildAsync(set, byFeature);

        result.Should().Contain("{B4BFCC3A-DB2C-424C-B029-7FE99A87C641}");
        result.Should().NotContain("HiddenByDefault");
    }

    [Fact]
    public async Task BuildAsync_ASettingTheSetDoesNotCarry_IsNotInTheScript()
    {
        // An unticked Builder include box drops the choice from the set; the setting stays in the feature dictionary.
        var set = SetOf(new SettingChoice("privacy-advertising-id", new ChoiceValue.Toggle(true)));
        var byFeature = new Dictionary<string, IReadOnlyList<Setting>>
        {
            {
                FeatureIds.Privacy,
                new[]
                {
                    SettingCatalog.Find("security-remote-assistance")!,
                    SettingCatalog.Find("privacy-advertising-id")!,
                }
            }
        };

        var result = await _sut.BuildAsync(set, byFeature);

        // The Disabled state writes the same value name with 0, so the whole line is asserted, not the name alone.
        result.Should().Contain(@"Set-RegistryValue -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo' -Name 'Enabled' -Type 'DWord' -Value 1");
        result.Should().NotContain("fAllowToGetHelp");
    }
}
