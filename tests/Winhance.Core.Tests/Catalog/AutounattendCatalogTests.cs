using FluentAssertions;
using Microsoft.Win32;
using Winhance.Core.Features.Autounattend.Catalogs;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Xunit;

using Winhance.Core.Features.Common.Localization;
namespace Winhance.Core.Tests.Catalog;

public class AutounattendCatalogTests
{
    private static readonly string[] PageOrder =
    [
        "autounattend-architecture-x64",
        "autounattend-architecture-arm64",
        "autounattend-architecture-x86",
        "autounattend-edition",
        "autounattend-product-key",
        "autounattend-hardware-checks",
        "autounattend-account",
        "autounattend-accounts",
        "autounattend-licence-page",
        "autounattend-oem-registration-page",
        "autounattend-network-page",
        "autounattend-privacy",
        "autounattend-computer-name",
        "autounattend-netfx3",
        "autounattend-desktop-shortcut",
    ];

    private static readonly ControlKind[] ControlOrder =
    [
        ControlKind.CheckBox,
        ControlKind.CheckBox,
        ControlKind.CheckBox,
        ControlKind.Selection,
        ControlKind.TextBox,
        ControlKind.Toggle,
        ControlKind.Selection,
        ControlKind.List,
        ControlKind.Toggle,
        ControlKind.Toggle,
        ControlKind.Toggle,
        ControlKind.Selection,
        ControlKind.TextBox,
        ControlKind.Toggle,
        ControlKind.Toggle,
    ];

    // Microsoft's generic RTM install keys, pinned as literals because the key text is what Setup reads.
    private static readonly (string Label, string ProductKey)[] GenericEditions =
    [
        ("Windows 11 Home", "YTMG3-N6DKC-DKB77-7M9GH-8HVX7"),
        ("Windows 11 Home N", "4CPRK-NM3K3-X6XXQ-RXX86-WXCHW"),
        ("Windows 11 Home Single Language", "BT79Q-G7N6G-PGBYW-4YWX6-6F4BT"),
        ("Windows 11 Pro", "VK7JG-NPHTM-C97JM-9MPGT-3V66T"),
        ("Windows 11 Pro N", "2B87N-8KFHP-DKV6R-Y2C8J-PKCKT"),
        ("Windows 11 Pro for Workstations", "DXG7C-N36C4-C4HTG-X4T3X-2YV77"),
        ("Windows 11 Pro for Workstations N", "WYPNQ-8C467-V2W6J-TX4WX-WT2RQ"),
        ("Windows 11 Pro Education", "8PTT6-RNW4C-6V7J2-C2D3X-MHBPB"),
        ("Windows 11 Pro Education N", "GJTYN-HDMQY-FRR76-HVGC7-QPF8P"),
        ("Windows 11 Education", "YNMGQ-8RYV3-4PGQ3-C8XTP-7CFBY"),
        ("Windows 11 Education N", "84NGF-MHBT6-FXBX8-QWJK7-DRR8H"),
        ("Windows 11 Enterprise", "XGVPP-NMH47-7TTHJ-W3FW7-8HV2C"),
        ("Windows 11 Enterprise N", "WGGHN-J84D6-QYCPR-T7PJ7-X766F"),
    ];

    private static readonly string[] LabConfigValues =
    [
        "BypassTPMCheck",
        "BypassSecureBootCheck",
        "BypassStorageCheck",
        "BypassCPUCheck",
        "BypassRAMCheck",
        "BypassDiskCheck",
    ];

    private static Setting S(string id) => AutounattendCatalog.All.First(s => s.Id == id);

    private static SettingState State(string id, LocKey label) => S(id).States.Single(st => st.Label == label);

    private static object? Written(SettingState state, string key) => state.Set[key].WritePayload;

    [Fact]
    public void The_page_is_the_fifteen_settings_in_the_order_the_cards_are_drawn()
    {
        AutounattendCatalog.All.Select(s => s.Id).Should().Equal(PageOrder);
    }

    [Fact]
    public void Every_setting_writes_the_answer_file_and_nothing_else()
    {
        foreach (var setting in AutounattendCatalog.All)
            setting.IsAnswerFileOnly.Should().BeTrue(setting.Id);
    }

    [Fact]
    public void Every_setting_passes_the_validator()
    {
        var errors = AutounattendCatalog.All
            .SelectMany(CatalogValidator.Validate)
            .Select(e => $"{e.SettingId}: {e.Message}")
            .ToList();

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Every_relationship_the_page_declares_resolves_inside_it()
    {
        var errors = CatalogValidator.ValidateCatalog(AutounattendCatalog.All)
            .Select(e => $"{e.SettingId}: {e.Message}")
            .ToList();

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Each_card_derives_the_control_kind_its_mechanism_implies()
    {
        AutounattendCatalog.All.Select(s => s.Control).Should().Equal(ControlOrder);
    }

    [Fact]
    public void Every_default_role_is_unconditional_and_context_free()
    {
        // A build-scoped default slips past the validator's one-default-per-context rule yet still seeds the
        // Builder card. A file written before Windows exists has no build to scope to.
        foreach (var setting in AutounattendCatalog.All.Where(s => s.States.Count > 0))
        {
            var defaults = setting.States
                .SelectMany(st => st.Roles)
                .Where(r => r.Kind == RoleKind.WindowsDefault)
                .ToList();

            defaults.Should().ContainSingle(setting.Id);
            defaults[0].AppliesTo.Should().BeEmpty(setting.Id);
            defaults[0].Context.Should().Be(PowerContext.Always, setting.Id);
        }
    }

    [Theory]
    [InlineData("autounattend-architecture-x64", "amd64", "AMD64")]
    [InlineData("autounattend-architecture-arm64", "arm64", "ARM64")]
    [InlineData("autounattend-architecture-x86", "x86", "x86")]
    public void Each_architecture_is_a_check_box_that_starts_checked_on_the_PC_it_names(string id, string architecture, string thisPc)
    {
        var setting = S(id);

        setting.Display.Name.Value.Should().Be($"Setting_{id}_Name");
        setting.Control.Should().Be(ControlKind.CheckBox);
        setting.Targets.OfType<AutounattendArchitecture>().Single().Architecture.Should().Be(architecture);
        var processor = setting.Targets.OfType<RegTarget>().Single();
        processor.ReadOnly.Should().BeTrue();
        processor.Paths.Single().Should().Be(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Session Manager\Environment");
        processor.ValueName.Should().Be("PROCESSOR_ARCHITECTURE");

        var checkedState = State(id, LocKey.Common.Checked);
        Written(checkedState, "arch").Should().Be(architecture);
        checkedState.Set[processor.Key].Matches(thisPc, present: true).Should().BeTrue();
        checkedState.HasRole(RoleKind.WindowsDefault).Should().BeTrue();
        State(id, LocKey.Common.Unchecked).Set["arch"].AcceptsAbsent.Should().BeTrue();
        State(id, LocKey.Common.Unchecked).Set[processor.Key].AcceptsAnyPresent.Should().BeTrue();
    }

    [Fact]
    public void No_two_architecture_cards_claim_the_same_processor()
    {
        var accepted = AutounattendCatalog.All
            .Where(s => s.Targets.OfType<AutounattendArchitecture>().Any())
            .Select(s => State(s.Id, LocKey.Common.Checked).Set["this-pc"].AcceptedValues.Single())
            .ToList();

        accepted.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void The_edition_dropdown_offers_the_generic_keys_and_the_two_that_carry_none()
    {
        var states = S("autounattend-edition").States;

        states.Should().HaveCount(16);

        // WillShowUI is Setup's own key prompt: Always for the choice that defers to it, OnError where a key is
        // given, Never where the key comes from elsewhere.
        states[0].Label.Should().Be(LocKey.Setting.AutounattendEdition.Option0);
        Written(states[0], "key").Should().Be("00000-00000-00000-00000-00000");
        Written(states[0], "show").Should().Be("Always");
        states[0].HasRole(RoleKind.WindowsDefault).Should().BeTrue();

        for (int i = 0; i < GenericEditions.Length; i++)
        {
            var state = states[i + 1];
            state.Label.Value.Should().Be($"Setting_autounattend-edition_Option_{i + 1}");
            Written(state, "key").Should().Be(GenericEditions[i].ProductKey);
            Written(state, "show").Should().Be("OnError");
        }

        states[14].Label.Should().Be(LocKey.Setting.AutounattendEdition.Option14);
        states[14].Set["key"].AcceptsAbsent.Should().BeTrue();
        Written(states[14], "show").Should().Be("OnError");

        states[15].Label.Should().Be(LocKey.Setting.AutounattendEdition.Option15);
        states[15].Set["key"].AcceptsAbsent.Should().BeTrue();
        Written(states[15], "show").Should().Be("Never");
    }

    [Fact]
    public void The_product_key_box_opens_only_when_the_edition_defers_to_it()
    {
        var setting = S("autounattend-product-key");

        setting.UiParentId.Should().Be("autounattend-edition");
        setting.EnabledWhen!.OtherId.Should().Be("autounattend-edition");
        setting.EnabledWhen.States.Should().Equal(LocKey.Setting.AutounattendEdition.Option14);
        setting.TextBox!.Rule.Matches("VK7JG-NPHTM-C97JM-9MPGT-3V66T").Should().BeTrue();
        setting.TextBox.Rule.Matches("not-a-key").Should().BeFalse();

        var targets = setting.Targets.Cast<AutounattendElement>().ToList();
        targets.Select(t => t.Key).Should().Equal("key", "activate");
        targets[1].Pass.Should().Be("specialize");
        targets[1].Path.Should().Be("ProductKey");
    }

    [Fact]
    public void The_account_list_opens_only_when_Setup_is_asked_to_create_them()
    {
        var setting = S("autounattend-accounts");

        setting.UiParentId.Should().Be("autounattend-account");
        setting.EnabledWhen!.OtherId.Should().Be("autounattend-account");
        setting.EnabledWhen.States.Should().Equal(LocKey.Setting.AutounattendAccount.Option2);
        var name = setting.List!.Fields.Single(f => f.Key == "name");
        name.Rule!.Matches("Marco").Should().BeTrue();
        name.Rule.Matches("a:b").Should().BeFalse();
        setting.Targets.OfType<AutounattendElement>().Single().Path.Should().Be("UserAccounts/LocalAccounts");
    }

    [Fact]
    public void The_account_row_declares_the_six_fields_the_answer_file_writes()
    {
        var fields = S("autounattend-accounts").List!.Fields;

        fields.Select(f => (f.Key, f.Kind)).Should().Equal(
            ("name", FieldKind.Text),
            ("display-name", FieldKind.Text),
            ("group", FieldKind.Selection),
            ("password", FieldKind.Password),
            ("obscure", FieldKind.CheckBox),
            ("auto-logon", FieldKind.CheckBox));

        // Administrators is index 0 because a fresh install's first account has to be one.
        fields.Single(f => f.Key == "group").Options!.Should().Equal(
            LocKey.AutounattendAccounts.GroupAdministrators, LocKey.AutounattendAccounts.GroupUsers);
        fields.Single(f => f.Key == "group").Default.Should().Be("0");
        fields.Single(f => f.Key == "obscure").Default.Should().Be("true");

        var autoLogon = fields.Single(f => f.Key == "auto-logon");
        autoLogon.OneRowOnly.Should().BeTrue();
        autoLogon.OnlyWhen.Should().Be(new FieldCondition("group", 0));
    }

    [Fact]
    public void The_first_account_is_seeded_from_this_PCs_logon_through_read_only_registry_targets()
    {
        var setting = S("autounattend-accounts");
        var seeds = setting.Targets.OfType<RegTarget>().ToList();

        seeds.Should().OnlyContain(t => t.ReadOnly && t.Type == RegistryValueKind.String);
        seeds.Select(t => (t.Key, t.Paths.Single(), t.ValueName)).Should().Equal(
            ("name", @"HKEY_CURRENT_USER\Volatile Environment", "USERNAME"),
            ("display-name", @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\LogonUI", "LastLoggedOnDisplayName"));
        setting.List!.Seed.Should().Equal(new Dictionary<string, string> { ["name"] = "name", ["display-name"] = "display-name" });
        setting.IsAnswerFileOnly.Should().BeTrue();
    }

    [Fact]
    public void The_computer_name_is_seeded_from_this_PCs_host_name_through_a_read_only_registry_target()
    {
        var setting = S("autounattend-computer-name");
        var seed = setting.Targets.OfType<RegTarget>().Single();

        seed.ReadOnly.Should().BeTrue();
        seed.Type.Should().Be(RegistryValueKind.String);
        (seed.Key, seed.Paths.Single(), seed.ValueName).Should().Be(("this-pc", @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters", "Hostname"));
        setting.TextBox!.SeedKey.Should().Be("this-pc");
        setting.IsAnswerFileOnly.Should().BeTrue();
    }

    [Fact]
    public void Both_offline_account_choices_hide_the_online_sign_in_screens()
    {
        Written(State("autounattend-account", LocKey.Setting.AutounattendAccount.Option0), "hide-online").Should().Be("true");
        Written(State("autounattend-account", LocKey.Setting.AutounattendAccount.Option2), "hide-online").Should().Be("true");
        State("autounattend-account", LocKey.Setting.AutounattendAccount.Option1).Set["hide-online"].AcceptsAbsent.Should().BeTrue();
        State("autounattend-account", LocKey.Setting.AutounattendAccount.Option0).HasRole(RoleKind.WindowsDefault).Should().BeTrue();
    }

    [Fact]
    public void A_local_account_keeps_oobe_offline_with_two_setup_commands_and_one_first_logon_command()
    {
        var local = S("autounattend-account").States.Single(s => s.Label == LocKey.Setting.AutounattendAccount.Option0);

        var setup = local.Effects.OfType<AutounattendCommand>().ToList();
        setup.Should().HaveCount(2);
        setup.Should().OnlyContain(c => c.Pass == "specialize" && c.Component == "Microsoft-Windows-Deployment");
        setup[0].Command.Should().Contain("BypassNRO");
        setup[1].Command.Should().Contain("Disable-NetAdapter");
        local.Effects.OfType<AutounattendFirstLogonCommand>().Should().ContainSingle()
            .Which.Command.Should().Contain("Enable-NetAdapter");
        S("autounattend-account").States.Where(s => s.Label != LocKey.Setting.AutounattendAccount.Option0)
            .Should().OnlyContain(s => s.Effects.Count == 0);
    }

    [Fact]
    public void The_account_list_carries_the_command_that_deletes_the_cached_file()
    {
        var cleanup = S("autounattend-accounts").List!.PasswordCleanup;

        cleanup!.Command.Should().Contain("C:\\Windows\\Panther\\unattend.xml");
        cleanup.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Turning_the_hardware_checks_off_fires_the_six_LabConfig_commands_in_order()
    {
        State("autounattend-hardware-checks", LocKey.Common.Enabled).Effects.Should().BeEmpty();
        State("autounattend-hardware-checks", LocKey.Common.Disabled).HasRole(RoleKind.WindowsDefault).Should().BeTrue(
            "a fresh file skips the checks");

        var commands = State("autounattend-hardware-checks", LocKey.Common.Disabled).Effects.Cast<AutounattendCommand>().ToList();

        commands.Should().HaveCount(6);
        for (int i = 0; i < LabConfigValues.Length; i++)
        {
            commands[i].Pass.Should().Be("windowsPE");
            commands[i].Component.Should().Be("Microsoft-Windows-Setup");
            commands[i].Command.Should().Contain($"/v {LabConfigValues[i]} ");
        }

        commands[0].Description.Should().Be("Order 1-6: Skip Windows 11 Hardware Requirement Checks");
        commands.Skip(1).Should().OnlyContain(c => c.Description == null);
    }

    [Fact]
    public void NetFx3_is_enabled_from_the_media_in_the_specialize_pass()
    {
        var command = (AutounattendCommand)State("autounattend-netfx3", LocKey.Common.Enabled).Effects.Single();

        command.Pass.Should().Be("specialize");
        command.Component.Should().Be("Microsoft-Windows-Deployment");
        command.Command.Should().Contain("/FeatureName:NetFx3").And.Contain("/LimitAccess");
        command.Description.Should().Be("Enables .NET Framework 3.5 from Windows Installation Media");
        State("autounattend-netfx3", LocKey.Common.Enabled).HasRole(RoleKind.WindowsDefault).Should().BeTrue();
        State("autounattend-netfx3", LocKey.Common.Disabled).Effects.Should().BeEmpty();
    }

    [Fact]
    public void The_desktop_shortcut_is_an_answer_file_toggle_whose_on_state_runs_the_shortcut_script()
    {
        var setting = S("autounattend-desktop-shortcut");

        setting.IsAnswerFileOnly.Should().BeTrue();
        setting.Control.Should().Be(ControlKind.Toggle);
        State("autounattend-desktop-shortcut", LocKey.Common.Enabled).HasRole(RoleKind.WindowsDefault).Should().BeTrue();
        var script = State("autounattend-desktop-shortcut", LocKey.Common.Enabled).Effects.Should().ContainSingle().Which.Should().BeOfType<ScriptEffect>().Subject;
        script.Run.Should().Be(RunContext.System);
        script.Script.Should().Contain("Install Winhance.lnk").And.Contain("https://get.winhance.net");
        script.Script.Should().Contain(
            "$shortcut.Arguments = \"-ExecutionPolicy Bypass -NoProfile -Command `\"irm 'https://get.winhance.net' | iex`\"\"");
        script.Script.Should().Contain("$bytes[21] = 34");
        State("autounattend-desktop-shortcut", LocKey.Common.Disabled).Effects.Should().BeEmpty();
    }

    [Theory]
    [InlineData("autounattend-licence-page", "OOBE/HideEULAPage")]
    [InlineData("autounattend-oem-registration-page", "OOBE/HideOEMRegistrationScreen")]
    [InlineData("autounattend-network-page", "OOBE/HideWirelessSetupInOOBE")]
    public void Each_setup_page_toggle_hides_its_page_by_default(string id, string path)
    {
        var element = (AutounattendElement)S(id).Targets.Single();
        element.Pass.Should().Be("oobeSystem");
        element.Component.Should().Be("Microsoft-Windows-Shell-Setup");
        element.Path.Should().Be(path);

        var off = State(id, LocKey.Common.Disabled);
        Written(off, "hide").Should().Be("true");
        off.HasRole(RoleKind.WindowsDefault).Should().BeTrue();
        State(id, LocKey.Common.Enabled).Set["hide"].AcceptsAbsent.Should().BeTrue();
    }

    [Fact]
    public void The_privacy_dropdown_writes_3_then_1_then_nothing()
    {
        var states = S("autounattend-privacy").States;

        Written(states[0], "protect").Should().Be("3");
        states[0].HasRole(RoleKind.WindowsDefault).Should().BeTrue();
        Written(states[1], "protect").Should().Be("1");
        states[2].Set["protect"].AcceptsAbsent.Should().BeTrue();
    }

    [Fact]
    public void The_page_is_wired_into_the_live_catalog_under_its_own_feature()
    {
        AutounattendCatalog.FeatureId.Should().Be(FeatureIds.Autounattend);
        SettingCatalog.ByFeature[FeatureIds.Autounattend].Should().Equal(AutounattendCatalog.All);
        FeatureDefinitions.AutounattendFeatures.Should().Contain(FeatureIds.Autounattend);

        foreach (var id in PageOrder)
            SettingCatalog.Find(id).Should().NotBeNull(id);
    }

    [Fact]
    public void No_live_setting_points_a_relationship_at_an_answer_file_setting()
    {
        // Answer-file settings are filtered out of SettingCatalog.All on Live System, so a live setting naming one
        // would send the relationship cascade's GetById after a setting that is not there.
        var answerFileIds = AutounattendCatalog.All.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);

        answerFileIds.Should().HaveCount(PageOrder.Length);

        var offenders = SettingCatalog.All
            .Where(s => !s.IsAnswerFileOnly)
            .SelectMany(s => Relationships(s)
                .Where(answerFileIds.Contains)
                .Select(other => $"{s.Id} -> {other}"))
            .ToList();

        offenders.Should().BeEmpty();
    }

    private static IEnumerable<string> Relationships(Setting setting)
    {
        foreach (var link in setting.States.SelectMany(st => st.Links))
            yield return link.OtherId;

        foreach (var state in setting.States)
            if (state.Controls is { } controls)
                foreach (var childId in controls.Keys)
                    yield return childId;

        if (setting.EnabledWhen is { } gate)
            yield return gate.OtherId;

        if (setting.UiParentId is { } parent)
            yield return parent;
    }
}
