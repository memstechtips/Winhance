using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.TestSupport;
using Xunit;

using Winhance.Core.Features.Common.Localization;
namespace Winhance.Core.Tests.Catalog;

public class CatalogValidatorTests
{
    private static readonly string[] TestPaths = [@"HKEY_LOCAL_MACHINE\TEST"];

    private static RegTarget Reg(string key, string valueName) =>
        new(key, TestPaths, valueName, RegistryValueKind.DWord);

    private static AutounattendElement Element(string key) =>
        new(key, "oobeSystem", "Microsoft-Windows-Shell-Setup", "OOBE/" + key);

    private static SettingState St(LocKey label, Dictionary<string, StateValue> set,
        bool fallback = false, params StateRole[] roles) =>
        new() { Label = label, Set = set, Roles = roles, IsFallback = fallback };

    private static Setting Make(IReadOnlyList<Target> targets, IReadOnlyList<SettingState> states,
        IReadOnlyList<PowerContext>? contexts = null, IStateDetector? detector = null) =>
        new()
        {
            Id = "test-setting", Display = new() { Name = TestKeys.Of("Test"), Description = TestKeys.Of("Test") },
            Targets = targets, States = states, Detector = detector,
            Contexts = contexts ?? new[] { PowerContext.Always },
        };

    private static SettingState Named(string label, string key) =>
        St(TestKeys.Of(label), new() { [FakeOptionProvider.TargetKey] = StateValue.Of(key) });

    [Fact]
    public void Validate_EveryCatalogSetting_ReturnsNoErrors()
    {
        var errors = SettingCatalog.All
            .SelectMany(CatalogValidator.Validate)
            .Select(e => $"{e.SettingId}: {e.Message}")
            .ToList();

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(@"HKEY_CLASSES_ROOT\lnkfile")]
    [InlineData(@"HKCR\lnkfile")]
    public void Validate_RegistryPathThroughClassesRoot_ReturnsError(string path)
    {
        var s = Make([new RegTarget("NeverShowExt", [path], "NeverShowExt", RegistryValueKind.String)],
            [St(TestKeys.Of("On"), new() { ["NeverShowExt"] = StateValue.Of("") }, fallback: true)]);

        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("HKEY_CLASSES_ROOT"));
    }

    [Fact]
    public void Validate_KeyedSelectionWithOneTargetAndNoStates_IsValid()
    {
        Assert.Empty(CatalogValidator.Validate(FakeOptionProvider.SettingFor()));
    }

    [Fact]
    public void Validate_KeyedSelectionNamingOptionsAsStatesByKeyAlone_IsValid()
    {
        var s = FakeOptionProvider.SettingFor() with
        {
            Targets = [.. FakeOptionProvider.SettingFor().Targets, Reg("Type", "BackgroundType") with { ReadOnly = true }],
            States = [Named("A", "alpha"), Named("B", "beta")],
        };

        Assert.Empty(CatalogValidator.Validate(s));
    }

    [Fact]
    public void Validate_KeyedStateGivingKeyTargetNoString_ReturnsError()
    {
        var s = FakeOptionProvider.SettingFor() with
        {
            States = [St(TestKeys.Of("A"), new() { [FakeOptionProvider.TargetKey] = StateValue.Of(1) })],
        };

        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("names no key"));
    }

    [Fact]
    public void Validate_KeyedSelectionWithNoKeyTarget_RejectsNamedState()
    {
        var s = FakeOptionProvider.SettingFor() with
        {
            Targets = [new RegTarget(FakeOptionProvider.TargetKey, TestPaths, "V", RegistryValueKind.String) { From = OptionValue.Color }],
            States = [Named("A", "alpha")],
        };

        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("names no key"));
    }

    [Fact]
    public void Validate_TwoKeyedStatesWithSameKeyInAnyCase_ReturnsError()
    {
        var s = FakeOptionProvider.SettingFor() with { States = [Named("A", "alpha"), Named("B", "ALPHA")] };

        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("repeats the key 'ALPHA'"));
    }

    [Fact]
    public void Validate_KeyedStateCarryingRoleEffectControlOrLink_ReturnsError()
    {
        var named = Named("A", "alpha");
        var states = new[]
        {
            named with { Roles = [StateRole.Recommended] },
            named with { Effects = [new ScriptEffect("x", RunContext.User)] },
            named with { Controls = new Dictionary<string, LocKey> { ["other"] = TestKeys.Of("On") } },
            named with { Links = [new Link("other", LinkKind.Requires, TestKeys.Of("On"))] },
        };

        foreach (var state in states)
        {
            var s = FakeOptionProvider.SettingFor() with { States = [state] };
            Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("may not carry roles, effects, controls or links"));
        }
    }

    [Fact]
    public void Validate_PowerPlanListWithNoTarget_IsValid()
    {
        var s = FakeOptionProvider.SettingFor() with
        {
            Options = new(OptionSource.PowerPlans),
            Targets = System.Array.Empty<Target>(),
        };

        Assert.Empty(CatalogValidator.Validate(s));
    }

    [Fact]
    public void Validate_NonPowerPlanKeyedSelectionWithoutRegistryTarget_ReturnsError()
    {
        var none = FakeOptionProvider.SettingFor() with { Targets = System.Array.Empty<Target>() };
        var readOnly = FakeOptionProvider.SettingFor() with { Targets = [Reg("Type", "BackgroundType") with { ReadOnly = true }] };

        Assert.Contains(CatalogValidator.Validate(none), e => e.Message.Contains("at least one registry target that is not ReadOnly"));
        Assert.Contains(CatalogValidator.Validate(readOnly), e => e.Message.Contains("at least one registry target that is not ReadOnly"));
    }

    [Fact]
    public void Validate_KeyedSelectionWithNumericRange_ReturnsError()
    {
        var s = FakeOptionProvider.SettingFor() with { Numeric = new() { Min = 0, Max = 100 } };

        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("Numeric range"));
    }

    [Fact]
    public void Validate_ComputedValueTargetOutsideKeyedSelection_ReturnsError()
    {
        var keyed = FakeOptionProvider.SettingFor() with
        {
            Targets = [.. FakeOptionProvider.SettingFor().Targets, Reg("Locale", "Locale") with { From = OptionValue.LocaleId }],
        };
        var live = Make(
            new[] { Reg("Mode", "Mode") with { From = OptionValue.LocaleId } },
            new[]
            {
                St(TestKeys.Of("Hide"), new() { ["Mode"] = StateValue.Of(0) }),
                St(TestKeys.Of("Box"), new() { ["Mode"] = StateValue.Of(2) }),
            });

        Assert.Empty(CatalogValidator.Validate(keyed));
        Assert.Contains(CatalogValidator.Validate(live), e => e.Message.Contains("takes From LocaleId"));
    }

    [Theory]
    [InlineData(OptionSource.TimeZones)]
    [InlineData(OptionSource.KeyboardLayouts)]
    public void Validate_RegistrySubkeyListWithoutPathOrCaption_ReturnsError(OptionSource source)
    {
        const string message = "must name their Path and LabelValue";
        var named = FakeOptionProvider.SettingFor() with { Options = new(source) { Path = @"HKEY_LOCAL_MACHINE\TEST", LabelValue = "Display" } };

        Assert.DoesNotContain(CatalogValidator.Validate(named), e => e.Message.Contains(message));
        Assert.Contains(CatalogValidator.Validate(named with { Options = named.Options! with { Path = null } }), e => e.Message.Contains(message));
        Assert.Contains(CatalogValidator.Validate(named with { Options = named.Options! with { LabelValue = null } }), e => e.Message.Contains(message));
    }

    [Fact]
    public void Validate_NonSubkeyListNamingPathOrCaption_ReturnsError()
    {
        var withPath = FakeOptionProvider.SettingFor() with { Options = new(OptionSource.RegionalFormats) { Path = @"HKEY_LOCAL_MACHINE\TEST" } };
        var withCaption = FakeOptionProvider.SettingFor() with { Options = new(OptionSource.RegionalFormats) { LabelValue = "Display" } };

        Assert.Contains(CatalogValidator.Validate(withPath), e => e.Message.Contains("Path and LabelValue would be silently ignored"));
        Assert.Contains(CatalogValidator.Validate(withCaption), e => e.Message.Contains("Path and LabelValue would be silently ignored"));
    }

    [Fact]
    public void Validate_FixedKeysOnListOtherThanColourOrLocale_ReturnsError()
    {
        var colors = FakeOptionProvider.SettingFor() with { Options = new(OptionSource.Colors) { Keys = ["#000000"] } };
        var locale = FakeOptionProvider.SettingFor() with
        {
            Options = new(OptionSource.LocaleValues) { Value = OptionValue.ShortDate, Keys = ["dd/MM/yyyy"] },
        };
        var formats = FakeOptionProvider.SettingFor() with { Options = new(OptionSource.RegionalFormats) { Keys = ["en-US"] } };

        Assert.DoesNotContain(CatalogValidator.Validate(colors), e => e.Message.Contains("fixed Keys"));
        Assert.DoesNotContain(CatalogValidator.Validate(locale), e => e.Message.Contains("fixed Keys"));
        Assert.Contains(CatalogValidator.Validate(formats), e => e.Message.Contains("Only a Colors or LocaleValues list declares fixed Keys"));
    }

    [Fact]
    public void Validate_LocaleValueListWithoutValueName_ReturnsError()
    {
        var unnamed = FakeOptionProvider.SettingFor() with { Options = new(OptionSource.LocaleValues) };
        var elsewhere = FakeOptionProvider.SettingFor() with
        {
            Options = new(OptionSource.RegionalFormats) { Value = OptionValue.ShortDate },
        };

        Assert.Contains(CatalogValidator.Validate(unnamed), e => e.Message.Contains("must name the Value it offers"));
        Assert.Contains(CatalogValidator.Validate(elsewhere), e => e.Message.Contains("must name the Value it offers"));
    }

    [Fact]
    public void Validate_ValidSetting_ReturnsNoErrors()
    {
        var s = Make(
            new[] { Reg("Mode", "SearchboxTaskbarMode") },
            new[]
            {
                St(TestKeys.Of("Hide"), new() { ["Mode"] = StateValue.Of(0) }, roles: new StateRole(RoleKind.Recommended)),
                St(TestKeys.Of("Box"),  new() { ["Mode"] = StateValue.Of(2) }, roles: new StateRole(RoleKind.WindowsDefault)),
            });
        Assert.Empty(CatalogValidator.Validate(s));
    }

    [Fact]
    public void Validate_TwoFallbackStates_ReturnsError()
    {
        var s = Make(
            new[] { Reg("K", "V") },
            new[]
            {
                St(TestKeys.Of("A"), new() { ["K"] = StateValue.Of(1) }, fallback: true),
                St(TestKeys.Of("B"), new() { ["K"] = StateValue.Of(2) }, fallback: true),
            });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("IsFallback"));
    }

    [Fact]
    public void Validate_TwoRecommendedInSameContext_ReturnsError()
    {
        var s = Make(
            new[] { Reg("K", "V") },
            new[]
            {
                St(TestKeys.Of("A"), new() { ["K"] = StateValue.Of(1) }, roles: new StateRole(RoleKind.Recommended)),
                St(TestKeys.Of("B"), new() { ["K"] = StateValue.Of(2) }, roles: new StateRole(RoleKind.Recommended)),
            });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("Recommended"));
    }

    [Fact]
    public void Validate_RecommendedInDifferentContexts_IsValid()
    {
        var s = Make(
            new[] { Reg("K", "V") },
            new[]
            {
                St(TestKeys.Of("A"), new() { ["K"] = StateValue.Of(1) }, roles: new StateRole(RoleKind.Recommended, PowerContext.AC)),
                St(TestKeys.Of("B"), new() { ["K"] = StateValue.Of(2) }, roles: new StateRole(RoleKind.Recommended, PowerContext.DC)),
            },
            contexts: new[] { PowerContext.AC, PowerContext.DC });
        Assert.DoesNotContain(CatalogValidator.Validate(s), e => e.Message.Contains("Recommended"));
    }

    [Fact]
    public void Validate_NonFallbackStateWithEmptySet_ReturnsError()
    {
        var s = Make(
            new[] { Reg("K", "V") },
            new[] { St(TestKeys.Of("Broken"), new()) });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("undetectable"));
    }

    [Fact]
    public void Validate_FallbackStateWithEmptySet_IsValid()
    {
        var s = Make(
            new[] { Reg("K", "V") },
            new[]
            {
                St(TestKeys.Of("Known"), new() { ["K"] = StateValue.Of(1) }),
                St(TestKeys.Of("Default"), new(), fallback: true),
            });
        Assert.DoesNotContain(CatalogValidator.Validate(s), e => e.Message.Contains("undetectable"));
    }

    [Fact]
    public void Validate_CheckedOrUncheckedOutsideExactPair_ReturnsError()
    {
        var s = Make(
            new[] { Reg("K", "V") },
            new[]
            {
                St(LocKey.Common.Checked, new() { ["K"] = StateValue.Of(1) }),
                St(TestKeys.Of("Off"), new() { ["K"] = StateValue.Of(0) }),
            });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("Checked/Unchecked"));
    }

    [Fact]
    public void Validate_StateMissingTargetKey_ReturnsError()
    {
        var s = Make(
            new[] { Reg("Start", "Start"), Reg("Preload", "IsInputAppPreloadEnabled") },
            new[] { St(TestKeys.Of("Manual"), new() { ["Start"] = StateValue.Of(3) }) });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("missing target key"));
    }

    [Fact]
    public void Validate_StateWithUnknownTargetKey_ReturnsError()
    {
        var s = Make(
            new[] { Reg("Start", "Start") },
            new[] { St(TestKeys.Of("X"), new() { ["Start"] = StateValue.Of(3), ["Ghost"] = StateValue.Of(1) }) });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("unknown target key"));
    }

    [Fact]
    public void Validate_DuplicateTargetKey_ReturnsError()
    {
        var s = Make(
            new[] { Reg("K", "A"), Reg("K", "B") },
            new[] { St(TestKeys.Of("X"), new() { ["K"] = StateValue.Of(1) }) });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("Duplicate target key"));
    }

    [Fact]
    public void Validate_CustomDetector_SkipsTargetCoverageCheck()
    {
        var detector = new FakeDetector();
        var s = Make(
            new[] { Reg("Start", "Start"), Reg("Preload", "P") },
            new[] { St(TestKeys.Of("X"), new() { ["Start"] = StateValue.Of(3) }) }, // would be "missing Preload" without detector
            detector: detector);
        Assert.DoesNotContain(CatalogValidator.Validate(s), e => e.Message.Contains("missing target key"));
    }

    [Fact]
    public void Validate_StateWithNoSetAndNoDetector_ReturnsError()
    {
        var s = Make(
            new[] { Reg("Start", "Start") },
            new[] { St(TestKeys.Of("X"), new()) });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("empty Set"));
    }

    [Fact]
    public void Validate_CustomDetector_AllowsStateWithNoSet()
    {
        var s = Make(
            new[] { Reg("Start", "Start") },
            new[] { St(TestKeys.Of("X"), new()) },
            detector: new FakeDetector());
        Assert.DoesNotContain(CatalogValidator.Validate(s), e => e.Message.Contains("empty Set"));
    }

    [Fact]
    public void Validate_FallbackStateWithPartialSet_IsValid()
    {
        // A fallback is the last-resort catch-all, so a partial representative Set is allowed -
        // it must NOT trip the "missing target key" rule that non-fallback states do.
        var s = Make(
            new[] { Reg("Start", "Start"), Reg("Preload", "P") },
            new[]
            {
                St(TestKeys.Of("Known"),   new() { ["Start"] = StateValue.Of(3), ["Preload"] = StateValue.Of(1) }),
                St(TestKeys.Of("Default"), new() { ["Start"] = StateValue.Of(2) }, fallback: true),
            });
        Assert.DoesNotContain(CatalogValidator.Validate(s), e => e.Message.Contains("missing target key"));
    }

    [Fact]
    public void Validate_FallbackStateWithUnknownKey_ReturnsError()
    {
        // Exempt from "missing", but a typo'd/unknown key is always caught - fallback or not.
        var s = Make(
            new[] { Reg("Start", "Start") },
            new[]
            {
                St(TestKeys.Of("Known"),   new() { ["Start"] = StateValue.Of(3) }),
                St(TestKeys.Of("Default"), new() { ["Start"] = StateValue.Of(2), ["Ghost"] = StateValue.Of(1) }, fallback: true),
            });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("unknown target key"));
    }

    [Fact]
    public void Action_ZeroStateWithEffect_IsValid()
    {
        var s = new Setting
        {
            Id = "act-ok",
            Display = new() { Name = TestKeys.Of("n"), Description = TestKeys.Of("d"), GroupName = TestKeys.Of("g") },
            Effects = new Effect[] { new ScriptEffect("echo hi", RunContext.System) },
        };
        Assert.Empty(CatalogValidator.Validate(s));
    }

    [Fact]
    public void Action_EffectsWithStates_IsRejected()
    {
        var s = new Setting
        {
            Id = "act-bad-states",
            Display = new() { Name = TestKeys.Of("n"), Description = TestKeys.Of("d"), GroupName = TestKeys.Of("g") },
            Effects = new Effect[] { new ScriptEffect("echo hi", RunContext.System) },
            States = new[] { new SettingState { Label = LocKey.Common.Enabled, IsFallback = true } },
        };
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("stateless Actions"));
    }

    [Fact]
    public void Action_EffectsWithTargets_IsRejected()
    {
        var s = new Setting
        {
            Id = "act-bad-targets",
            Display = new() { Name = TestKeys.Of("n"), Description = TestKeys.Of("d"), GroupName = TestKeys.Of("g") },
            Effects = new Effect[] { new ScriptEffect("echo hi", RunContext.System) },
            Targets = new Target[] { Reg("k", "k") },
        };
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("stateless Actions"));
    }

    [Fact]
    public void Setting_NoStatesNoTargetsNoDetectorNoEffects_IsRejected()
    {
        var s = new Setting
        {
            Id = "does-nothing",
            Display = new() { Name = TestKeys.Of("n"), Description = TestKeys.Of("d"), GroupName = TestKeys.Of("g") },
        };
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("detects nothing and does nothing"));
    }

    [Fact]
    public void NumericRangeShape_ZeroStateWithTargetNoEffects_IsValid()
    {
        var s = new Setting
        {
            Id = "range-ok",
            Display = new() { Name = TestKeys.Of("n"), Description = TestKeys.Of("d"), GroupName = TestKeys.Of("g") },
            Targets = new Target[] { Reg("v", "v") },
        };
        Assert.Empty(CatalogValidator.Validate(s));
    }

    private static Setting WithId(string id, IReadOnlyList<SettingState> states) =>
        new()
        {
            Id = id, Display = new() { Name = TestKeys.Of("n"), Description = TestKeys.Of("d") },
            Targets = new Target[] { Reg("K", "V") }, States = states,
        };

    [Fact]
    public void Validate_DetectOnlyStateWithRecommendedRole_ReturnsError()
    {
        var s = Make(
            new[] { Reg("K", "V") },
            new[]
            {
                St(TestKeys.Of("A"), new() { ["K"] = StateValue.Of(1) }),
                new SettingState { Label = TestKeys.Of("Neutral"), IsFallback = true, IsDetectOnly = true,
                    Roles = new[] { StateRole.Recommended } },
            });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("IsDetectOnly"));
    }

    [Fact]
    public void Validate_DetectOnlyStateWithBuildScopedWindowsDefaultRole_ReturnsError()
    {
        // HasRole deliberately ignores build-scoped roles, so the rule reads Roles directly. Without
        // that, an OS-divergent default could be hung on an unchoosable state and nothing would notice.
        var s = Make(
            new[] { Reg("K", "V") },
            new[]
            {
                St(TestKeys.Of("A"), new() { ["K"] = StateValue.Of(1) }),
                new SettingState { Label = TestKeys.Of("Neutral"), IsFallback = true, IsDetectOnly = true,
                    Roles = new[] { new StateRole(RoleKind.WindowsDefault) { AppliesTo = new[] { BuildRange.Windows11 } } } },
            });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("IsDetectOnly"));
    }

    [Fact]
    public void Validate_DetectOnlyStateWithNoRoleAndNoSet_IsValid()
    {
        // The shape the theme master's neutral state uses: fallback exempts it from the empty-Set rule.
        var s = Make(
            new[] { Reg("K", "V") },
            new[]
            {
                St(TestKeys.Of("A"), new() { ["K"] = StateValue.Of(1) }, roles: new StateRole(RoleKind.Recommended)),
                new SettingState { Label = TestKeys.Of("Neutral"), IsFallback = true, IsDetectOnly = true },
            });
        Assert.Empty(CatalogValidator.Validate(s));
    }

    [Fact]
    public void ValidateCatalog_ControlsNamingDetectOnlyChildState_ReturnsError()
    {
        var child = WithId("child", new[]
        {
            St(TestKeys.Of("On"), new() { ["K"] = StateValue.Of(1) }),
            new SettingState { Label = TestKeys.Of("Neutral"), IsFallback = true, IsDetectOnly = true },
        });
        var master = WithId("master", new[]
        {
            new SettingState
            {
                Label = TestKeys.Of("Preset"),
                Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Of(1) },
                Controls = new Dictionary<string, LocKey> { ["child"] = TestKeys.Of("Neutral") },
            },
        });

        Assert.Contains(CatalogValidator.ValidateCatalog(new[] { master, child }),
            e => e.Message.Contains("detect-only state"));
    }

    [Fact]
    public void ValidateCatalog_LinkRequiredStateNamingDetectOnlyState_ReturnsError()
    {
        var other = WithId("other", new[]
        {
            St(TestKeys.Of("On"), new() { ["K"] = StateValue.Of(1) }),
            new SettingState { Label = TestKeys.Of("Neutral"), IsFallback = true, IsDetectOnly = true },
        });
        var owner = WithId("owner", new[]
        {
            new SettingState
            {
                Label = TestKeys.Of("On"),
                Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Of(1) },
                Links = new[] { new Link("other", LinkKind.Requires, TestKeys.Of("Neutral")) },
            },
        });

        Assert.Contains(CatalogValidator.ValidateCatalog(new[] { owner, other }),
            e => e.Message.Contains("detect-only state"));
    }

    [Fact]
    public void ValidateCatalog_ControlsNamingChoosableChildState_IsValid()
    {
        // Non-vacuity for the two rules above: the same shape pointed at a real option raises nothing.
        var child = WithId("child", new[]
        {
            St(TestKeys.Of("On"), new() { ["K"] = StateValue.Of(1) }),
            new SettingState { Label = TestKeys.Of("Neutral"), IsFallback = true, IsDetectOnly = true },
        });
        var master = WithId("master", new[]
        {
            new SettingState
            {
                Label = TestKeys.Of("Preset"),
                Set = new Dictionary<string, StateValue> { ["K"] = StateValue.Of(1) },
                Controls = new Dictionary<string, LocKey> { ["child"] = TestKeys.Of("On") },
            },
        });

        Assert.DoesNotContain(CatalogValidator.ValidateCatalog(new[] { master, child }),
            e => e.Message.Contains("detect-only state"));
    }

    [Fact]
    public void Validate_AnswerFileOnlyStateWritingNothing_IsValid()
    {
        var s = Make(
            new[] { Element("K") },
            new[]
            {
                St(LocKey.Common.Enabled, new(), roles: new StateRole(RoleKind.WindowsDefault)),
                St(LocKey.Common.Disabled, new() { ["K"] = StateValue.Of("true") }),
            });
        Assert.DoesNotContain(CatalogValidator.Validate(s), e => e.Message.Contains("undetectable"));
        Assert.DoesNotContain(CatalogValidator.Validate(s), e => e.Message.Contains("missing target key"));
    }

    [Fact]
    public void Validate_AnswerFileOnlySettingWithStatesAndNoSeed_ReturnsError()
    {
        var s = Make(
            new[] { Element("K") },
            new[] { St(LocKey.Common.Enabled, new()), St(LocKey.Common.Disabled, new() { ["K"] = StateValue.Of("true") }) });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("state to seed from"));
    }

    [Theory]
    [InlineData(RoleKind.Recommended)]
    [InlineData(RoleKind.WindowsDefault)]
    public void Validate_AnswerFileOnlySettingWithEitherRole_HasStateToSeedFrom(RoleKind role)
    {
        var s = Make(
            new[] { Element("K") },
            new[]
            {
                St(LocKey.Common.Enabled, new()),
                St(LocKey.Common.Disabled, new() { ["K"] = StateValue.Of("true") }, roles: new StateRole(role)),
            });
        Assert.DoesNotContain(CatalogValidator.Validate(s), e => e.Message.Contains("state to seed from"));
    }

    [Fact]
    public void Validate_LiveSettingWithAnswerFileTarget_ReturnsError()
    {
        var s = Make(
            new Target[] { Reg("R", "V"), Element("K") },
            new[]
            {
                St(LocKey.Common.Enabled, new() { ["R"] = StateValue.Of(1), ["K"] = StateValue.Of("true") }, roles: new StateRole(RoleKind.WindowsDefault)),
                St(LocKey.Common.Disabled, new() { ["R"] = StateValue.Of(0), ["K"] = StateValue.Absent }),
            });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("mixes"));
    }

    [Fact]
    public void Validate_LiveSettingWithSetupCommand_ReturnsError()
    {
        var s = Make(
            new[] { Reg("R", "V") },
            new[]
            {
                St(LocKey.Common.Enabled, new() { ["R"] = StateValue.Of(1) }, roles: new StateRole(RoleKind.WindowsDefault)),
                St(LocKey.Common.Disabled, new() { ["R"] = StateValue.Of(0) }) with
                {
                    Effects = new[] { new AutounattendCommand("specialize", "Microsoft-Windows-Deployment", "cmd.exe /c echo") },
                },
            });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("mixes"));
    }

    [Fact]
    public void Validate_SettingLevelMachineEffectOnElementOnlySetting_KeepsItLive()
    {
        var s = Make(new[] { Element("K") }, Array.Empty<SettingState>()) with
        {
            Effects = new[] { new RegistryWriteEffect("HKCU\\S", "V", RegistryValueKind.DWord, 1) },
        };
        Assert.False(s.IsAnswerFileOnly);
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("mixes"));
    }

    [Fact]
    public void Validate_SettingLevelSetupCommandOnLiveTarget_ReturnsMixedError()
    {
        var s = Make(new[] { Reg("R", "V") }, Array.Empty<SettingState>()) with
        {
            Effects = new[] { new AutounattendCommand("specialize", "Microsoft-Windows-Deployment", "cmd.exe /c echo") },
        };
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("mixes"));
    }

    private static Setting LiveWithScript(ScriptEffect script) =>
        Make(
            new[] { Reg("R", "V") },
            new[]
            {
                St(LocKey.Common.Enabled, new() { ["R"] = StateValue.Of(1) }) with { Effects = new[] { script } },
                St(LocKey.Common.Disabled, new() { ["R"] = StateValue.Of(0) }),
            });

    [Fact]
    public void Validate_LiveScriptDependingOnAnswerFileApps_ReturnsError()
    {
        var s = LiveWithScript(new ScriptEffect("echo hi", RunContext.System) { When = AppsToInstall.Some });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("the live apply ignores both"));
    }

    [Fact]
    public void Validate_LiveScriptWaitingForSignInAfterSetup_ReturnsError()
    {
        var s = LiveWithScript(new ScriptEffect("echo hi", RunContext.SignInAfterSetup));
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("the live apply ignores both"));
    }

    [Fact]
    public void Validate_LiveScriptThatRunsEveryTime_IsValid()
    {
        Assert.Empty(CatalogValidator.Validate(LiveWithScript(new ScriptEffect("echo hi", RunContext.System))));
    }

    [Fact]
    public void Validate_AnswerFileOnlyScriptDependingOnAppsAndSignIn_IsValid()
    {
        var s = Make(
            Array.Empty<Target>(),
            new[]
            {
                St(LocKey.Common.Enabled, new(), roles: new StateRole(RoleKind.WindowsDefault)) with
                {
                    Effects = new[]
                    {
                        new ScriptEffect("echo some", RunContext.System) { When = AppsToInstall.Some },
                        new ScriptEffect("echo later", RunContext.SignInAfterSetup),
                    },
                },
                St(LocKey.Common.Disabled, new()),
            });

        Assert.True(s.IsAnswerFileOnly);
        Assert.Empty(CatalogValidator.Validate(s));
    }

    [Fact]
    public void Validate_AnswerFileOnlyStateOmittingUnwrittenElement_IsValid()
    {
        var s = Make(
            new[] { Element("K1"), Element("K2") },
            new[]
            {
                St(LocKey.Common.Enabled, new() { ["K1"] = StateValue.Of("true") }, roles: new StateRole(RoleKind.WindowsDefault)),
                St(LocKey.Common.Disabled, new() { ["K1"] = StateValue.Absent, ["K2"] = StateValue.Of("true") }),
            });
        Assert.DoesNotContain(CatalogValidator.Validate(s), e => e.Message.Contains("missing target key"));
    }

    [Fact]
    public void Validate_ExistsValueOnAnswerFileElement_ReturnsError()
    {
        var s = Make(
            new[] { Element("K") },
            new[]
            {
                St(LocKey.Common.Enabled, new() { ["K"] = StateValue.Exists }, roles: new StateRole(RoleKind.WindowsDefault)),
                St(LocKey.Common.Disabled, new() { ["K"] = StateValue.Absent }),
            });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("Exists"));
    }

    private static Setting Typed(IReadOnlyList<Target> targets, IReadOnlyList<SettingState> states) =>
        Make(targets, states) with { TextBox = new(new TextRule("^.+$", UpperCase: false, TestKeys.Of("Anything."))) };

    [Fact]
    public void Validate_TextSettingWithStates_ReturnsError()
    {
        var s = Typed(new[] { Element("K") }, new[] { St(LocKey.Common.Enabled, new() { ["K"] = StateValue.Of("true") }) });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("may not declare states"));
    }

    [Fact]
    public void Validate_TextSettingWithNumericRange_ReturnsError()
    {
        var s = Typed(new[] { Element("K") }, Array.Empty<SettingState>()) with { Numeric = new Numeric { Min = 0, Max = 10 } };
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("Numeric range"));
    }

    [Fact]
    public void Validate_TextSettingWithOptionList_ReturnsError()
    {
        var s = FakeOptionProvider.SettingFor() with { TextBox = new(new TextRule("^.+$", UpperCase: false, TestKeys.Of("Anything."))) };
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("option list"));
    }

    [Fact]
    public void Validate_TextSettingWithNoDestination_ReturnsError()
    {
        var s = Typed(Array.Empty<Target>(), Array.Empty<SettingState>());
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("at least one target to write to"));
    }

    [Fact]
    public void Validate_TextSettingHandingTextToScripts_IsValid()
    {
        var s = Typed(Array.Empty<Target>(), Array.Empty<SettingState>())
            with { CustomStateScripts = [new ScriptEffect("Move-Folders '{{value}}'", RunContext.User)] };

        Assert.Empty(CatalogValidator.Validate(s));
    }

    [Fact]
    public void Validate_TextSettingWritingToRegistry_ReturnsError()
    {
        var s = Typed(new[] { Reg("R", "V") }, Array.Empty<SettingState>());
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("answer-file target"));
    }

    [Fact]
    public void Validate_TextSettingSeededFromReadOnlyTarget_IsValid()
    {
        var s = Typed(new Target[] { Element("K"), Reg("this-pc", "Hostname") with { ReadOnly = true } }, Array.Empty<SettingState>())
            with { TextBox = new(new TextRule("^.+$", UpperCase: false, TestKeys.Of("Anything.")), SeedKey: "this-pc") };

        Assert.DoesNotContain(CatalogValidator.Validate(s), e => e.Message.Contains("answer-file target") || e.Message.Contains("SeedKey"));
    }

    [Fact]
    public void Validate_UserScriptTargetOutsideList_ReturnsError()
    {
        var s = Make(new[] { new AutounattendScript("script", RunContext.System) }, Array.Empty<SettingState>());

        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("carries the user's own scripts"));
    }

    [Fact]
    public void Validate_UserScriptRunningAtSignInAfterSetup_ReturnsError()
    {
        var s = Typed(new[] { new AutounattendScript("script", RunContext.SignInAfterSetup) }, Array.Empty<SettingState>());

        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("not at the sign-in after Setup"));
    }

    [Fact]
    public void Validate_TextSeedNotNamingReadOnlyRegistryTarget_ReturnsError()
    {
        var s = Typed(new[] { Element("K") }, Array.Empty<SettingState>())
            with { TextBox = new(new TextRule("^.+$", UpperCase: false, TestKeys.Of("Anything.")), SeedKey: "nope") };

        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("SeedKey names 'nope'"));
    }

    [Fact]
    public void Validate_TextSettingWritingDesktopSlideshow_IsValid()
    {
        var s = Typed(
            new Target[]
            {
                new DesktopSlideshowTarget("album"),
                Reg("BackgroundType", "BackgroundType") with { ReadOnly = true },
                Reg("SlideshowDirectoryPath1", "SlideshowDirectoryPath1") with { ReadOnly = true },
            },
            Array.Empty<SettingState>()) with
        {
            TextBox = new(new TextRule("^.+$", UpperCase: false, TestKeys.Of("Anything.")), SeedKey: "album"),
            CustomStateScripts = [new ScriptEffect("Set-Slideshow '{{value}}'", RunContext.User)],
        };

        Assert.Empty(CatalogValidator.Validate(s));
    }

    [Fact]
    public void Validate_SlideshowBoxWithoutScript_ReturnsError()
    {
        var s = Typed(new Target[] { new DesktopSlideshowTarget("album") }, Array.Empty<SettingState>());

        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("CustomStateScripts"));
    }

    [Fact]
    public void Validate_SlideshowBoxWithoutAlbumValues_ReturnsError()
    {
        var s = Typed(new Target[] { new DesktopSlideshowTarget("album") }, Array.Empty<SettingState>());

        var errors = CatalogValidator.Validate(s);

        Assert.Contains(errors, e => e.Message.Contains("keyed 'BackgroundType'"));
        Assert.Contains(errors, e => e.Message.Contains("keyed 'SlideshowDirectoryPath1'"));
    }

    [Fact]
    public void Validate_ReadOnlyRegistryTargetOnSettingWithoutSeed_ReturnsError()
    {
        var live = Make(new[] { Reg("R", "V") with { ReadOnly = true } }, new[] { St(LocKey.Common.Enabled, new() { ["R"] = StateValue.Of(1) }) });
        Assert.Contains(CatalogValidator.Validate(live), e => e.Message.Contains("is ReadOnly, which seeds"));
    }

    [Fact]
    public void Validate_ReadOnlyRegistryTargetOnKeyedSelection_IsValid()
    {
        var s = FakeOptionProvider.SettingFor() with
        {
            Targets = [.. FakeOptionProvider.SettingFor().Targets, Reg("Type", "BackgroundType") with { ReadOnly = true }],
        };

        Assert.DoesNotContain(CatalogValidator.Validate(s), e => e.Message.Contains("is ReadOnly"));
    }

    [Fact]
    public void Validate_KeyedSelectionWithAnswerFileElementBesideLiveTargets_IsValid()
    {
        var s = FakeOptionProvider.SettingFor() with
        {
            Targets = [.. FakeOptionProvider.SettingFor().Targets, Element("zone-element")],
        };

        Assert.DoesNotContain(CatalogValidator.Validate(s), e => e.Message.Contains("mixes an answer-file mechanism"));
    }

    [Fact]
    public void Validate_RegistryTargetClearedOnApplyNotReadOnly_ReturnsError()
    {
        var s = Listed(new Target[] { Element("K"), Reg("R", "V") with { ClearedOnApply = true } }, Array.Empty<SettingState>());
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("ClearedOnApply but not ReadOnly"));
    }

    [Fact]
    public void Validate_RegistryTargetBothReadOnlyAndApplyOnly_ReturnsError()
    {
        var s = Listed(new Target[] { Element("K"), Reg("R", "V") with { ReadOnly = true, ApplyOnly = true } }, Array.Empty<SettingState>());
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("both ReadOnly and ApplyOnly"));
    }

    [Fact]
    public void Validate_ListSettingWithReadOnlyRegistryTargetsBesideElement_IsValid()
    {
        var s = Listed(new Target[] { Element("K"), Reg("name", "USERNAME") with { ReadOnly = true } }, Array.Empty<SettingState>());
        Assert.DoesNotContain(CatalogValidator.Validate(s), e => e.Message.Contains("exactly one answer-file element"));
    }

    [Fact]
    public void Validate_ListSeedNamingTargetThatIsNotReadOnlyRegistry_ReturnsError()
    {
        var s = Listed(new Target[] { Element("K"), Reg("name", "USERNAME") with { ReadOnly = true } }, Array.Empty<SettingState>())
            with
            {
                List = new(
                    [new Field("name", FieldKind.Text, TestKeys.Of("Name"))],
                    Seed: new Dictionary<string, string> { ["name"] = "name", ["display-name"] = "display-name" }),
            };
        var errors = CatalogValidator.Validate(s);
        Assert.Contains(errors, e => e.Message.Contains("List.Seed names 'display-name'"));
        Assert.DoesNotContain(errors, e => e.Message.Contains("List.Seed names 'name'"));
    }

    [Fact]
    public void Validate_ListSettingWithNoField_ReturnsError()
    {
        var s = Make(new[] { Element("K") }, Array.Empty<SettingState>()) with { List = new([]) };
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("at least one field"));
    }

    [Fact]
    public void Validate_TwoFieldsSharingKey_ReturnsError()
    {
        var s = Make(new[] { Element("K") }, Array.Empty<SettingState>()) with
        {
            List = new([new Field("name", FieldKind.Text, TestKeys.Of("Name")), new Field("name", FieldKind.Password, TestKeys.Of("Again"))]),
        };
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("Duplicate field key 'name'"));
    }

    private static Setting Listed(IReadOnlyList<Target> targets, IReadOnlyList<SettingState> states) =>
        Make(targets, states) with { List = new([new Field("name", FieldKind.Text, TestKeys.Of("Name"))]) };

    [Fact]
    public void Validate_ListSettingWithStates_ReturnsError()
    {
        var s = Listed(new[] { Element("K") }, new[] { St(LocKey.Common.Enabled, new() { ["K"] = StateValue.Of("true") }) });
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("may not declare states"));
    }

    [Fact]
    public void Validate_ListSettingWithoutElement_ReturnsError()
    {
        var s = Listed(Array.Empty<Target>(), Array.Empty<SettingState>());
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("exactly one answer-file element"));
    }

    [Fact]
    public void Validate_ListSettingWithScriptsInsteadOfElement_IsValid()
    {
        var s = Listed(new[] { new AutounattendScript("script", RunContext.System) }, Array.Empty<SettingState>());
        Assert.DoesNotContain(CatalogValidator.Validate(s), e => e.Message.Contains("exactly one answer-file element"));
    }

    [Fact]
    public void Validate_ListSettingChangingMediaInsteadOfElement_IsValid()
    {
        var s = Listed(new[] { new AutounattendMedia("installers") }, Array.Empty<SettingState>());
        Assert.DoesNotContain(CatalogValidator.Validate(s), e => e.Message.Contains("exactly one answer-file element"));
    }

    [Fact]
    public void Validate_FileFieldNamingPickerFilter_IsValid()
    {
        var bare = Listed(new[] { Element("K") }, Array.Empty<SettingState>()) with
        {
            List = new([new Field("file", FieldKind.File, TestKeys.Of("File"))]),
        };
        var filtered = bare with
        {
            List = new([new Field("file", FieldKind.File, TestKeys.Of("File"), Filter: new FileFilter(TestKeys.Of("Files"), "*.exe"))]),
        };

        Assert.Contains(CatalogValidator.Validate(bare), e => e.Message.Contains("Field 'file': a File field names the filter"));
        Assert.DoesNotContain(CatalogValidator.Validate(filtered), e => e.Message.Contains("a File field names the filter"));
    }

    [Fact]
    public void Validate_NonFileFieldNamingPickerFilter_ReturnsError()
    {
        var s = Listed(new[] { Element("K") }, Array.Empty<SettingState>()) with
        {
            List = new([new Field("name", FieldKind.Text, TestKeys.Of("Name"), Filter: new FileFilter(TestKeys.Of("Files"), "*.exe"))]),
        };
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("Field 'name': a File field names the filter"));
    }

    [Fact]
    public void Validate_ListSettingWritingToRegistry_ReturnsError()
    {
        var s = Listed(new[] { Reg("R", "V") }, Array.Empty<SettingState>());
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("exactly one answer-file element"));
    }

    [Fact]
    public void Validate_ListSettingWritingTwoElements_ReturnsError()
    {
        var s = Listed(new[] { Element("K1"), Element("K2") }, Array.Empty<SettingState>());
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("exactly one answer-file element"));
    }

    [Fact]
    public void Validate_ListSettingWithTextRule_ReturnsError()
    {
        var s = Listed(new[] { Element("K") }, Array.Empty<SettingState>()) with
        {
            TextBox = new(new TextRule("^.+$", UpperCase: false, TestKeys.Of("Anything."))),
        };
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("silently ignored"));
    }

    [Fact]
    public void Validate_ListSettingWithOptionList_ReturnsError()
    {
        var s = FakeOptionProvider.SettingFor() with { List = new([new Field("name", FieldKind.Text, TestKeys.Of("Name"))]) };
        Assert.Contains(CatalogValidator.Validate(s), e => e.Message.Contains("option list"));
    }

    private sealed class FakeDetector : IStateDetector
    {
        public string? Detect(Setting setting, IDetectionContext context) => null;
    }
}
