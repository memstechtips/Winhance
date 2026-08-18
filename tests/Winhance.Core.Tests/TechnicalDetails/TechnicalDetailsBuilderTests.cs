using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.TechnicalDetails;
using Xunit;
using Winhance.TestSupport;

namespace Winhance.Core.Tests.TechnicalDetails;

public class TechnicalDetailsBuilderTests
{
    private static readonly WinBuild Build = new(26100);

    // Returns the key itself, so an assertion can prove which key produced a string.
    private static ILocalizationService Loc()
    {
        var mock = new Mock<ILocalizationService>();
        mock.Setup(l => l.GetString(It.IsAny<string>())).Returns((string k) => k);
        mock.MirrorTryGetString();
        return mock.Object;
    }

    // No-setup mock: TryGetString reports every key missing, so every lookup falls back to its English default.
    private static ILocalizationService FallbackLoc() => new Mock<ILocalizationService>().Object;

    private static Display Show(string name = "Test") => new() { Name = name, Description = "d" };

    private static SettingStateSnapshot Snap(
        InputType inputType = InputType.Toggle,
        bool isSelected = false,
        int? selectedIndex = null,
        params string[] optionLabels) => new()
    {
        InputType = inputType,
        IsSelected = isSelected,
        SelectedIndex = selectedIndex,
        Options = optionLabels.Select(l => new ComboBoxDisplayOption(l, 0)).ToList(),
    };

    // Scheduled tasks — the catalog authors Of(true)/Of(false), so DeleteOnWrite is false for BOTH
    // states. Reading DeleteOnWrite made every task setting report "On" for current/recommended/default.

    private static Setting TaskSetting() => new()
    {
        Id = "task-setting",
        Display = Show(),
        Targets = [new TaskTarget("Task", @"\Microsoft\Windows\Foo\Bar")],
        States =
        [
            new SettingState
            {
                Label = "Enabled",
                Roles = [StateRole.WindowsDefault],
                Set = new Dictionary<string, StateValue> { ["Task"] = StateValue.Of(true) },
            },
            new SettingState
            {
                Label = "Disabled",
                Roles = [StateRole.Recommended],
                Set = new Dictionary<string, StateValue> { ["Task"] = StateValue.Of(false) },
            },
        ],
    };

    // Script labels — a Selection whose states aren't literally named "Enabled"/"Disabled" used to
    // render every script block as "On Enable" because the label was matched against English text.

    private static Setting ThreeStateScriptSetting() => new()
    {
        Id = "service-setting",
        Display = Show(),
        Targets = [new RegTarget("Start", [@"HKEY_LOCAL_MACHINE\SYSTEM\Svc"], "Start", RegistryValueKind.DWord)],
        States =
        [
            new SettingState
            {
                Label = "ServiceOption_DisabledRecommended",
                Roles = [StateRole.Recommended],
                Effects = [new ScriptEffect("disable-script", RunContext.System)],
                Set = new Dictionary<string, StateValue> { ["Start"] = StateValue.Of(4) },
            },
            new SettingState
            {
                Label = "ServiceOption_Manual",
                Roles = [StateRole.WindowsDefault],
                Effects = [new ScriptEffect("manual-script", RunContext.System)],
                Set = new Dictionary<string, StateValue> { ["Start"] = StateValue.Of(3) },
            },
            new SettingState
            {
                Label = "ServiceOption_Automatic",
                Effects = [new ScriptEffect("auto-script", RunContext.System)],
                Set = new Dictionary<string, StateValue> { ["Start"] = StateValue.Of(2) },
            },
        ],
    };

    [Fact]
    public void ScriptRows_AreLabelledByTheirOwnOption_NotAllOnEnable()
    {
        var snapshot = Snap(InputType.Selection, selectedIndex: 0,
            optionLabels: ["Disabled (Recommended)", "Manual", "Automatic"]);

        var sections = TechnicalDetailsBuilder.Build(ThreeStateScriptSetting(), snapshot, FallbackLoc(), Build);

        var labels = MatrixOf(sections).CodeBlocks.Select(b => b.Label).ToList();
        labels.Should().Equal(
            "When set to Disabled (Recommended)",
            "When set to Manual",
            "When set to Automatic");
        labels.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void ScriptRows_CarryTheirOwnBody()
    {
        var snapshot = Snap(InputType.Selection, selectedIndex: 0,
            optionLabels: ["Disabled (Recommended)", "Manual", "Automatic"]);

        var sections = TechnicalDetailsBuilder.Build(ThreeStateScriptSetting(), snapshot, FallbackLoc(), Build);

        var blocks = MatrixOf(sections).CodeBlocks;
        blocks.Select(b => b.Body).Should().Equal("disable-script", "manual-script", "auto-script");
        blocks.Should().OnlyContain(b => b.Kind == CodeKind.PowerShell);
    }

    private static Setting TwoKeySelection() => new()
    {
        Id = "uac",
        Display = Show(),
        Targets =
        [
            new RegTarget("Consent", [@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies"], "ConsentPromptBehaviorAdmin", RegistryValueKind.DWord),
            new RegTarget("Secure", [@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies"], "PromptOnSecureDesktop", RegistryValueKind.DWord),
        ],
        States =
        [
            new SettingState
            {
                Label = "NotifyDim",
                Roles = [StateRole.WindowsDefault],
                Set = new Dictionary<string, StateValue> { ["Consent"] = StateValue.Of(5), ["Secure"] = StateValue.Of(1) },
            },
            new SettingState
            {
                Label = "NeverNotify",
                Roles = [StateRole.Recommended],
                Set = new Dictionary<string, StateValue> { ["Consent"] = StateValue.Of(0), ["Secure"] = StateValue.Of(0) },
            },
        ],
    };

    // .reg-driven settings — the RegTarget is a detection probe, not the change

    private static Setting RegContentSetting() => new()
    {
        Id = "take-ownership",
        Display = Show(),
        Targets = [new RegTarget("K", [@"HKEY_CLASSES_ROOT\*\shell\TakeOwnership"], null, RegistryValueKind.String)],
        States =
        [
            new SettingState
            {
                Label = "Enabled",
                Roles = [StateRole.Recommended],
                Effects = [new RegContentEffect("Windows Registry Editor Version 5.00\n\n[HKEY_CLASSES_ROOT\\*\\shell\\TakeOwnership]\n@=\"Take Ownership\"")],
                Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Of("Take Ownership") },
            },
            new SettingState
            {
                Label = "Disabled",
                Roles = [StateRole.WindowsDefault],
                Effects = [new RegContentEffect("Windows Registry Editor Version 5.00\n\n[-HKEY_CLASSES_ROOT\\*\\shell\\TakeOwnership]")],
                Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Absent },
            },
        ],
    };

    [Fact]
    public void RegContentPayloads_AreLabelledByOptionAndTaggedAsRegFiles()
    {
        var sections = TechnicalDetailsBuilder.Build(RegContentSetting(), Snap(isSelected: true), FallbackLoc(), Build);

        var code = MatrixOf(sections).CodeBlocks;
        code.Should().HaveCount(2);
        code.Should().OnlyContain(b => b.Kind == CodeKind.RegFile);
        code.Select(b => b.Label).Should().Equal("When set to On", "When set to Off");
    }

    private static OptionMatrix MatrixOf(OptionMatrix? matrix) =>
        matrix.Should().NotBeNull().And.Subject.As<OptionMatrix>();

    [Fact]
    public void Matrix_HasOneColumnPerTarget_NamedAndTyped()
    {
        var snapshot = Snap(InputType.Selection, selectedIndex: 1, optionLabels: ["Notify", "Never notify"]);

        var matrix = MatrixOf(TechnicalDetailsBuilder.Build(TwoKeySelection(), snapshot, FallbackLoc(), Build));

        matrix.Columns.Select(c => (c.Header, c.TypeName)).Should().Equal(
            ("ConsentPromptBehaviorAdmin", "DWord"),
            ("PromptOnSecureDesktop", "DWord"));
    }

    [Fact]
    public void Matrix_CellsAreAlignedWithColumns()
    {
        var snapshot = Snap(InputType.Selection, selectedIndex: 1, optionLabels: ["Notify", "Never notify"]);

        var matrix = MatrixOf(TechnicalDetailsBuilder.Build(TwoKeySelection(), snapshot, FallbackLoc(), Build));

        matrix.Options.Should().HaveCount(2);
        matrix.Options.Should().OnlyContain(o => o.Cells.Count == matrix.Columns.Count);
        matrix.Options[0].Cells.Select(c => c.Text).Should().Equal("5", "1");
        matrix.Options[1].Cells.Select(c => c.Text).Should().Equal("0", "0");
    }

    [Fact]
    public void Matrix_MarksCurrentRecommendedAndDefaultOnTheOptionRow()
    {
        var snapshot = Snap(InputType.Selection, selectedIndex: 1, optionLabels: ["Notify", "Never notify"]);

        var matrix = MatrixOf(TechnicalDetailsBuilder.Build(TwoKeySelection(), snapshot, FallbackLoc(), Build));

        matrix.Options[0].Label.Should().Be("Notify");
        matrix.Options[0].IsWindowsDefault.Should().BeTrue();
        matrix.Options[0].IsCurrent.Should().BeFalse();

        matrix.Options[1].Label.Should().Be("Never notify");
        matrix.Options[1].IsRecommended.Should().BeTrue();
        matrix.Options[1].IsCurrent.Should().BeTrue();
    }

    [Fact]
    public void Matrix_ScalesToASettingWithManyTargets_WithoutTruncating()
    {
        var targets = Enumerable.Range(0, 22)
            .Select(i => new RegTarget($"k{i}", [@"HKEY_LOCAL_MACHINE\SOFTWARE\U"], $"Value{i}", RegistryValueKind.DWord))
            .Cast<Target>().ToArray();
        var setting = new Setting
        {
            Id = "many-keys",
            Display = Show(),
            Targets = targets,
            States =
            [
                new SettingState { Label = "A", Set = targets.ToDictionary(t => t.Key, _ => StateValue.Of(1)) },
                new SettingState { Label = "B", Set = targets.ToDictionary(t => t.Key, _ => StateValue.Of(2)) },
            ],
        };

        var matrix = MatrixOf(TechnicalDetailsBuilder.Build(
            setting, Snap(InputType.Selection, selectedIndex: 0, optionLabels: ["A", "B"]), FallbackLoc(), Build));

        // Every value is present; the view scrolls sideways rather than the model dropping any.
        matrix.Columns.Should().HaveCount(22);
        matrix.Options.Should().OnlyContain(o => o.Cells.Count == 22);
    }

    [Fact]
    public void ToggleOptions_AreLabelledOnAndOff()
    {
        var setting = new Setting
        {
            Id = "toggle",
            Display = Show(),
            Targets = [new RegTarget("K", [@"HKEY_CURRENT_USER\SOFTWARE\X"], "V", RegistryValueKind.DWord)],
            States =
            [
                new SettingState { Label = "Enabled", Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Of(1) } },
                new SettingState
                {
                    Label = "Disabled",
                    Roles = [StateRole.Recommended],
                    Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Of(0) },
                },
            ],
        };

        var matrix = MatrixOf(TechnicalDetailsBuilder.Build(setting, Snap(isSelected: false), FallbackLoc(), Build));

        matrix.Options.Select(o => o.Label).Should().Equal("On", "Off");
        matrix.Options[1].IsCurrent.Should().BeTrue();
    }

    [Fact]
    public void TaskSetting_ColumnReadsEnabledOrDisabled_NotTrueFalse()
    {
        var matrix = MatrixOf(TechnicalDetailsBuilder.Build(TaskSetting(), Snap(isSelected: false), FallbackLoc(), Build));

        matrix.Options[0].Cells.Select(c => c.Text).Should().Equal("Enabled");     // On  -> task stays enabled
        matrix.Options[1].Cells.Select(c => c.Text).Should().Equal("Disabled");    // Off -> task disabled
        matrix.Options[1].IsCurrent.Should().BeTrue();
        matrix.Options[1].IsRecommended.Should().BeTrue();
    }

    [Fact]
    public void Matrix_FlagsTheOptionsThatAlsoRunAScript()
    {
        var snapshot = Snap(InputType.Selection, selectedIndex: 0,
            optionLabels: ["Disabled (Recommended)", "Manual", "Automatic"]);

        var matrix = MatrixOf(TechnicalDetailsBuilder.Build(ThreeStateScriptSetting(), snapshot, FallbackLoc(), Build));

        matrix.Columns.Should().Contain(c => c.Kind == MatrixColumnKind.Script);
        var scriptColumn = matrix.Columns.Select((c, i) => (c, i)).First(x => x.c.Kind == MatrixColumnKind.Script).i;
        matrix.Options.Should().OnlyContain(o => o.Cells[scriptColumn].IsCheck);
    }

    [Fact]
    public void Reading_IsAbsentWhenDetectionResolvedToAnOption()
    {
        var snapshot = Snap(InputType.Selection, selectedIndex: 1, optionLabels: ["Notify", "Never notify"]);

        var matrix = MatrixOf(TechnicalDetailsBuilder.Build(TwoKeySelection(), snapshot, FallbackLoc(), Build));

        matrix.HasReading.Should().BeFalse("the current marker already says what is on the system");
    }

    [Fact]
    public void Reading_ReportsLiveValuesWhenNoOptionMatched()
    {
        var snapshot = Snap(InputType.Selection, optionLabels: ["Notify", "Never notify"]) with
        {
            Outcome = SettingDetectionOutcome.Custom,
            Readings = new Dictionary<string, object>
            {
                ["ConsentPromptBehaviorAdmin"] = 3,
                ["PromptOnSecureDesktop"] = 1,
            },
        };

        var matrix = MatrixOf(TechnicalDetailsBuilder.Build(TwoKeySelection(), snapshot, FallbackLoc(), Build));

        matrix.HasReading.Should().BeTrue();
        matrix.ReadingLabel.Should().Be("On your system now (matches no option)");
        matrix.ReadingCells.Select(c => c.Text).Should().Equal("3", "1");
        matrix.Options.Should().OnlyContain(o => !o.IsCurrent, "nothing matched, so no option is current");
    }

    [Fact]
    public void Reading_SaysSoWhenDetectionCouldNotRead()
    {
        var snapshot = Snap(InputType.Selection, optionLabels: ["Notify", "Never notify"]) with
        {
            Outcome = SettingDetectionOutcome.Undetermined,
        };

        var matrix = MatrixOf(TechnicalDetailsBuilder.Build(TwoKeySelection(), snapshot, FallbackLoc(), Build));

        matrix.ReadingLabel.Should().Be("Winhance could not read this");
        matrix.ReadingCells.Select(c => c.Text).Should().Equal("unknown", "unknown");
    }

    [Fact]
    public void Group_CarriesTheMechanismAndThePath()
    {
        var snapshot = Snap(InputType.Selection, selectedIndex: 0, optionLabels: ["Notify", "Never notify"]);

        var matrix = MatrixOf(TechnicalDetailsBuilder.Build(TwoKeySelection(), snapshot, FallbackLoc(), Build));

        var group = matrix.Groups.Should().ContainSingle("both values share one path").Subject;
        group.Label.Should().Be("Registry", "a reader who doesn't know DWord means registry needs telling");
        group.Kind.Should().Be(MatrixGroupKind.Registry);
        group.Paths.Should().ContainSingle().Which.Full.Should().Be(@"HKEY_LOCAL_MACHINE\SOFTWARE\Policies");
        group.StartColumn.Should().Be(0);
        group.ColumnSpan.Should().Be(2, "the header spans the columns it owns");
    }

    [Fact]
    public void Group_SplitsWhenValuesLiveInDifferentPaths()
    {
        var setting = new Setting
        {
            Id = "two-hives",
            Display = Show(),
            Targets =
            [
                new RegTarget("A", [@"HKEY_LOCAL_MACHINE\SYSTEM\Svc"], "Start", RegistryValueKind.DWord),
                new RegTarget("B", [@"HKEY_CURRENT_USER\Software\X"], "Preload", RegistryValueKind.DWord),
            ],
            States =
            [
                new SettingState { Label = "Enabled", Set = new Dictionary<string, StateValue> { ["A"] = StateValue.Of(1), ["B"] = StateValue.Of(1) } },
                new SettingState { Label = "Disabled", Set = new Dictionary<string, StateValue> { ["A"] = StateValue.Of(0), ["B"] = StateValue.Of(0) } },
            ],
        };

        var matrix = MatrixOf(TechnicalDetailsBuilder.Build(setting, Snap(isSelected: true), FallbackLoc(), Build));

        matrix.Groups.Should().HaveCount(2);
        matrix.Groups.Select(g => g.ColumnSpan).Should().AllBeEquivalentTo(1);
        matrix.Groups.Select(g => g.Paths[0].Full).Should().Equal(
            @"HKEY_LOCAL_MACHINE\SYSTEM\Svc", @"HKEY_CURRENT_USER\Software\X");
    }

    [Fact]
    public void Column_CarriesItsOwnChips_EachWithATooltip()
    {
        var setting = new Setting
        {
            Id = "gp",
            Display = Show(),
            Targets =
            [
                new RegTarget("K", [@"HKEY_CURRENT_USER\SOFTWARE\A", @"HKEY_LOCAL_MACHINE\SOFTWARE\A"], "V", RegistryValueKind.DWord)
                    { IsGroupPolicy = true },
            ],
            States =
            [
                new SettingState { Label = "Enabled", Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Of(1) } },
                new SettingState { Label = "Disabled", Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Of(0) } },
            ],
        };

        var column = MatrixOf(TechnicalDetailsBuilder.Build(setting, Snap(isSelected: true), FallbackLoc(), Build))
            .Columns.Should().ContainSingle().Subject;

        column.Chips.Select(c => c.Text).Should().Contain("Group Policy")
            .And.NotContain("mirrored", "the group header lists both paths now, each with its own button");

        // The mirror is stated by showing the places, not by a chip saying there is more than one.
        var group = MatrixOf(TechnicalDetailsBuilder.Build(setting, Snap(isSelected: true), FallbackLoc(), Build))
            .Groups.Should().ContainSingle().Subject;
        group.Paths.Select(p => p.Full).Should().Equal(
            @"HKEY_CURRENT_USER\SOFTWARE\A", @"HKEY_LOCAL_MACHINE\SOFTWARE\A");
        group.Paths.Select(p => p.Display).Should().Equal(@"HKCU\SOFTWARE\A", @"HKLM\SOFTWARE\A");
        column.Chips.Should().OnlyContain(c => c.Tooltip.Length > 0,
            "an unexplained chip like \"mirrored\" tells the user nothing on its own");
    }

    [Fact]
    public void RegContentSetting_TagsItsRegistryValueAsReadOnlyForDetection()
    {
        var column = MatrixOf(TechnicalDetailsBuilder.Build(RegContentSetting(), Snap(isSelected: true), FallbackLoc(), Build))
            .Columns.Should().ContainSingle(c => c.Kind == MatrixColumnKind.Value).Subject;

        column.Chips.Select(c => c.Text).Should().Contain("read only to detect");
    }

    [Fact]
    public void UnnamedRegistryValue_IsShownAsDefaultAndExplained()
    {
        // Catalogs author the key's unnamed default value as an EMPTY value name, not null.
        var column = MatrixOf(TechnicalDetailsBuilder.Build(RegContentSetting(), Snap(isSelected: true), FallbackLoc(), Build))
            .Columns.Should().ContainSingle(c => c.Kind == MatrixColumnKind.Value).Subject;

        column.Header.Should().Be("(Default)", "an empty header reads as a bug");
        column.HeaderTooltip.Should().NotBeEmpty("the user needs telling why this value has no name");
    }

    [Fact]
    public void TaskSetting_GetsItsOwnGroupNamingTheTask()
    {
        var matrix = MatrixOf(TechnicalDetailsBuilder.Build(TaskSetting(), Snap(isSelected: false), FallbackLoc(), Build));

        var group = matrix.Groups.Should().ContainSingle().Subject;
        group.Label.Should().Be("Scheduled task");
        group.Kind.Should().Be(MatrixGroupKind.ScheduledTask, "regedit cannot open a task path");
        group.CanOpenRegedit.Should().BeFalse("there is no launcher for a scheduled-task path");
        group.Paths.Should().ContainSingle().Which.Full.Should().Be(@"\Microsoft\Windows\Foo\Bar");
        matrix.Columns.Should().ContainSingle().Which.Header.Should().Be("Bar", "the leaf name keeps the column narrow");
    }

    [Fact]
    public void ScriptAndRegFileGetTheirOwnColumns_NotChipsOnTheOptionName()
    {
        var matrix = MatrixOf(TechnicalDetailsBuilder.Build(RegContentSetting(), Snap(isSelected: true), FallbackLoc(), Build));

        matrix.Columns.Should().Contain(c => c.Kind == MatrixColumnKind.RegFile);
        matrix.Groups.Should().Contain(g => g.Label == "Also runs");

        var regFileColumn = matrix.Columns.Select((c, i) => (c, i)).First(x => x.c.Kind == MatrixColumnKind.RegFile).i;
        matrix.Options.Should().OnlyContain(o => o.Cells[regFileColumn].IsCheck,
            "both On and Off import a .reg file");
    }

    [Fact]
    public void SettingWithNoScripts_HasNoAlsoRunsColumns()
    {
        var snapshot = Snap(InputType.Selection, selectedIndex: 0, optionLabels: ["Notify", "Never notify"]);

        var matrix = MatrixOf(TechnicalDetailsBuilder.Build(TwoKeySelection(), snapshot, FallbackLoc(), Build));

        matrix.Columns.Should().OnlyContain(c => c.Kind == MatrixColumnKind.Value);
        matrix.Groups.Should().NotContain(g => g.Label == "Also runs");
    }
    [Fact]
    public void NumericPowerSetting_BecomesAMatrixOfTheValuesWorthNaming()
    {
        var snapshot = new SettingStateSnapshot
        {
            InputType = InputType.NumericRange,
            SupportsSeparateACDC = true,
            HasBattery = false,
            AcNumericValue = 20,
        };

        var sections = TechnicalDetailsBuilder.Build(PowerNumericSetting(), snapshot, FallbackLoc(), Build);
        var matrix = MatrixOf(sections);

        // A range has no options, so the rows are the values worth naming: Windows' default, the
        // recommendation, and what the machine is on. Ordered by value, not by which role found them.
        matrix.Options.Select(o => o.Label).Should().Equal("10 Minutes", "15 Minutes", "20 Minutes");
        matrix.Options.Single(o => o.IsWindowsDefault).Label.Should().Be("10 Minutes");
        matrix.Options.Single(o => o.IsRecommended).Label.Should().Be("15 Minutes");
        matrix.Options.Single(o => o.IsCurrent).Label.Should().Be("20 Minutes");

        // No battery means one context, so nothing is qualified with "plugged in".
        matrix.Options.Should().OnlyContain(o =>
            o.CurrentContext == "" && o.RecommendedContext == "" && o.DefaultContext == "");

        matrix.Columns.Should().ContainSingle().Which.Chips.Select(c => c.Text)
            .Should().Contain(c => c.Contains("0-999 Minutes"),
                "the range is the thing to state when there is no list of options");
    }

    [Fact]
    public void NumericPowerSetting_SplitsAValueThatDiffersOnBattery()
    {
        var snapshot = new SettingStateSnapshot
        {
            InputType = InputType.NumericRange,
            SupportsSeparateACDC = true,
            HasBattery = true,
            AcNumericValue = 15,
            DcNumericValue = 5,
        };

        var matrix = MatrixOf(TechnicalDetailsBuilder.Build(PowerNumericSetting(), snapshot, FallbackLoc(), Build));

        // Recommended is 15 plugged in and 5 on battery, so each row carries its own qualifier
        // rather than the table growing a column that would repeat the same number down every row.
        matrix.Options.Single(o => o.Label == "15 Minutes").RecommendedContext.Should().Be("Plugged In");
        matrix.Options.Single(o => o.Label == "5 Minutes").RecommendedContext.Should().Be("On Battery");

        // Windows' default is 10 plugged in and 5 on battery; 5 is therefore both recommended on
        // battery and the default there, and is also what the machine is on.
        matrix.Options.Single(o => o.Label == "5 Minutes").DefaultContext.Should().Be("On Battery");
        matrix.Options.Single(o => o.Label == "10 Minutes").DefaultContext.Should().Be("Plugged In");

        // Two rows can be current at once: one for each context.
        matrix.Options.Where(o => o.IsCurrent).Select(o => o.Label)
            .Should().BeEquivalentTo(["15 Minutes", "5 Minutes"]);
    }

    private static Setting PowerNumericSetting() => new()
    {
        Id = "power-display",
        Display = Show(),
        Targets = [new PowerCfgTarget("P", "sub-guid", "set-guid", PowerModeSupport.Separate) { Units = "Seconds" }],
        Numeric = new Numeric
        {
            Min = 0,
            Max = 999,
            Units = "Minutes",
            Recommended = [new ContextValue(PowerContext.AC, 15), new ContextValue(PowerContext.DC, 5)],
            WindowsDefault = [new ContextValue(PowerContext.AC, 10), new ContextValue(PowerContext.DC, 5)],
        },
    };

    [Fact]
    public void Build_WithNoSetting_ReturnsNothing()
    {
        TechnicalDetailsBuilder.Build(null, Snap(), FallbackLoc(), Build).Should().BeNull();
    }

    [Fact]
    public void ASettingWithSomethingToDocument_ProducesOneTable()
    {
        var snapshot = Snap(InputType.Selection, selectedIndex: 0, optionLabels: ["A", "B"]);

        var matrix = MatrixOf(TechnicalDetailsBuilder.Build(TwoKeySelection(), snapshot, FallbackLoc(), Build));

        matrix.Columns.Should().NotBeEmpty();
        matrix.Options.Should().NotBeEmpty();
        matrix.SettingLabel.Should().NotBeEmpty("the table names itself in its own corner cell");
    }

    [Fact]
    public void ApplyBehaviour_BecomesRequirementChipsOnTheMatrix()
    {
        var setting = new Setting
        {
            Id = "restarts",
            Display = Show(),
            Apply = new ApplyBehavior { RequiresReboot = true, Restart = new RestartProcess("Explorer") },
            Targets = [new RegTarget("K", [@"HKEY_CURRENT_USER\SOFTWARE\X"], "V", RegistryValueKind.DWord)],
            States =
            [
                new SettingState { Label = "Enabled", Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Of(1) } },
                new SettingState { Label = "Disabled", Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Of(0) } },
            ],
        };

        // FallbackLoc, not Loc: the restart chip runs its text through Format, and a key-echo has no
        // {0} for the process name to land in.
        // These hang off Setting.Apply, so they are the same on every option. They belong to the
        // setting's own cell rather than to a section of their own below the table.
        var matrix = MatrixOf(TechnicalDetailsBuilder.Build(setting, Snap(isSelected: true), FallbackLoc(), Build));
        matrix.Requirements.Select(c => c.Text).Should()
            .Contain("Requires a system restart")
            .And.Contain("Explorer restart", "the chip names the process, not just 'a process'");
        matrix.Requirements.Should().Contain(c => c.Tooltip.Contains("bar at the bottom"),
            "Explorer is no longer killed on apply -- the user restarts it from the banner when ready");

    }

    private static Setting ActionSetting() => new()
    {
        Id = "clean-thing",
        Display = Show("Clean Thing"),
        Apply = new() { RequiresConfirmation = true },
        Effects =
        [
            new RegistryWriteEffect(@"HKEY_CURRENT_USER\Software\Foo", "Favorites", RegistryValueKind.Binary, Array.Empty<byte>()),
            new RegistryWriteEffect(@"HKEY_LOCAL_MACHINE\Software\Policies\Foo", "Pins", RegistryValueKind.String, "[]")
            {
                IsGroupPolicy = true,
            },
        ],
    };

    [Fact]
    public void Action_GetsOneRowCarryingTheValuesItWrites()
    {
        // The row IS the action: an action has nothing to choose between, so the table that documents
        // it has exactly one row rather than none.
        var matrix = MatrixOf(TechnicalDetailsBuilder.Build(ActionSetting(), Snap(), FallbackLoc(), Build));

        matrix.Columns.Select(c => c.Header).Should().Equal("Favorites", "Pins");
        matrix.Columns.Select(c => c.TypeName).Should().Equal("Binary", "String");
        matrix.Options.Should().ContainSingle();
        matrix.Options[0].Label.Should().Be("Clean Thing",
            "the row is named after the button the user pressed, not a new localization key");
        matrix.Options[0].Cells.Select(c => c.Text).Should().Equal("(empty)", "[]");
        matrix.Groups.Should().HaveCount(2, "the two writes go to two different paths");
        matrix.Groups.Should().OnlyContain(g => g.CanOpenRegedit,
            "each group names its path and can open it, like every other registry group");
    }

    [Fact]
    public void Action_RegistryWritesLeaveTheAlsoHappensBand()
    {
        // They ARE the action. Listing them under "Also happens when you apply" said the action's own
        // work was a side effect of itself, which is what made that band read as a puzzle.
        var matrix = MatrixOf(TechnicalDetailsBuilder.Build(ActionSetting(), Snap(), FallbackLoc(), Build));

        matrix.Notes.Should().NotContain(n => n.Detail.Contains("Favorites"));
    }

    [Fact]
    public void Action_RegistryColumnsSayTheyAreWrittenNotRead()
    {
        // An action is never detected, so the group cannot repeat the registry line about reading the
        // value to decide which option is active - there is no such reading and no option.
        var matrix = MatrixOf(TechnicalDetailsBuilder.Build(ActionSetting(), Snap(), Loc(), Build));

        matrix.Groups.Should().OnlyContain(g => g.Description.Length == 0);
        matrix.Columns.Should().OnlyContain(
            c => c.Chips.Any(chip => chip.Text == TechnicalDetailKeys.ChipApplyOnly));
        matrix.SettingDescription.Should().BeEmpty("there is no option to select on a one-shot");
    }

    [Fact]
    public void ScriptOnlyAction_KeepsItsPanelAndGrowsNoEmptyTable()
    {
        var setting = new Setting
        {
            Id = "script-action",
            Display = Show(),
            Apply = new() { RequiresConfirmation = true },
            Effects = [new ScriptEffect("do-the-thing", RunContext.System)],
        };

        var matrix = MatrixOf(TechnicalDetailsBuilder.Build(setting, Snap(), FallbackLoc(), Build));

        matrix.Columns.Should().BeEmpty();
        matrix.Options.Should().BeEmpty("a labelled row with no cell to its right heads nothing");
        matrix.CodeBlocks.Should().ContainSingle("its script is still the thing worth documenting");
    }

    [Fact]
    public void Action_NotesNameTheSettingsItsConfirmCheckboxWouldApply()
    {
        // The band under the table is headed "Also happens when you apply, if you agree to the prompt",
        // and the prompt's checkbox offers the feature's recommended settings. So the band answers the
        // question the checkbox raises: WHICH settings, and what each of them becomes. Read from the real
        // catalog because the feature grouping is what supplies the list.
        var setting = SettingCatalog.Find("taskbar-clean");
        setting.Should().NotBeNull("the taskbar cleaner must exist for this test to mean anything");

        var matrix = MatrixOf(TechnicalDetailsBuilder.Build(setting, Snap(), Loc(), Build));

        matrix.Notes.Should().Contain(n => n.Label == "Setting_taskbar-task-view_Name",
            "Show Task View button is a taskbar setting the recommended pass moves");
        matrix.Notes.Should().NotContain(n => n.Label == "Setting_taskbar-clean_Name",
            "the trigger applies itself; the checkbox is about the rest of the feature");
        matrix.Notes.Should().OnlyContain(n => n.Detail.Length > 0,
            "every row says what the setting will be set to, not just that it will be touched");
    }

    [Fact]
    public void TheTable_ProducesAScreenReaderSummary()
    {
        var snapshot = Snap(InputType.Selection, selectedIndex: 0, optionLabels: ["A", "B"]);

        MatrixOf(TechnicalDetailsBuilder.Build(TwoKeySelection(), snapshot, FallbackLoc(), Build))
            .AccessibleSummary.Should().NotBeEmpty();
    }
}
