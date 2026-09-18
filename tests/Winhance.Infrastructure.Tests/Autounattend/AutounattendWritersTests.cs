using System.Globalization;
using System.Xml.Linq;
using FluentAssertions;
using Winhance.Core.Features.Autounattend;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Common.Selections;
using Winhance.Infrastructure.Features.Autounattend;
using Winhance.Infrastructure.Features.Autounattend.Writers;
using Winhance.Infrastructure.Features.Customize.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Autounattend;

public class AutounattendWritersTests
{
    private static readonly XNamespace U = AutounattendDocument.Unattend;
    private static readonly XNamespace X = AutounattendDocument.WinhanceExtensions;
    private static readonly AutounattendRenderContext Context =
        new("Write-Host 'test'", "0.0.0", SelectionSet.Empty);

    private static readonly AutounattendRenderContext Carried =
        new("Write-Host 'carried'", "26.09.04", SelectionSet.Empty);

    private static readonly AutounattendElementWriter Elements = AutounattendDocumentBuilder.Elements;

    // "group" is the option index of the list's Selection field: 0 Administrators, 1 Users.
    private static ChoiceValue.ListRow Account(
        string name, string displayName, string group, string password, bool obscure, bool autoLogon = false) =>
        new(new Dictionary<string, string>
        {
            ["name"] = name,
            ["display-name"] = displayName,
            ["group"] = group,
            ["password"] = password,
            ["obscure"] = obscure ? "true" : "false",
            ["auto-logon"] = autoLogon ? "true" : "false",
        });

    private static readonly ChoiceValue.ListRow[] NoAccounts = [];

    private static readonly ChoiceValue.ListRow[] AdministratorWithPassword =
        [Account("Marco", "Marco du Plessis", "0", "hunter2", obscure: true)];

    private static readonly ChoiceValue.ListRow[] AdministratorWithPasswordSigningIn =
        [Account("Marco", "Marco du Plessis", "0", "hunter2", obscure: true, autoLogon: true)];

    private static readonly ChoiceValue.ListRow[] AdministratorWithNoPassword =
        [Account("Marco", "Marco du Plessis", "0", string.Empty, obscure: true)];

    private static readonly ChoiceValue.ListRow[] AdministratorWithNoPasswordSigningIn =
        [Account("Marco", "Marco du Plessis", "0", string.Empty, obscure: true, autoLogon: true)];

    private static readonly ChoiceValue.ListRow[] PlainTextAdministrator =
        [Account("Marco", "Marco du Plessis", "0", "hunter2", obscure: false)];

    private static readonly ChoiceValue.ListRow[] TwoAccountsWithNoPasswords =
    [
        Account("Marco", "Marco du Plessis", "0", string.Empty, obscure: false),
        Account("Guest", "Guest account", "1", string.Empty, obscure: true),
    ];

    private static readonly ChoiceValue.ListRow[] AdministratorAndStandardUser =
    [
        Account("Marco", "Marco du Plessis", "0", string.Empty, obscure: false),
        Account("Guest", "Guest account", "1", "hunter2", obscure: true),
    ];

    private static readonly string[] HardwareCheckNames =
    [
        "BypassTPMCheck",
        "BypassSecureBootCheck",
        "BypassStorageCheck",
        "BypassCPUCheck",
        "BypassRAMCheck",
        "BypassDiskCheck",
    ];

    private static SelectionSet SetWith(params SettingChoice[] choices) =>
        SelectionSet.Empty with { Settings = choices };

    private static AutounattendRenderContext ContextWith(params SettingChoice[] choices) =>
        new("Write-Host 'test'", "0.0.0", SetWith(choices));

    private static XDocument Write(IAutounattendElementWriter writer, params SettingChoice[] choices)
    {
        var doc = AutounattendDocument.NewSkeleton();
        writer.Write(doc, new AutounattendRenderContext("Write-Host 'test'", "0.0.0", SetWith(choices)));
        return doc;
    }

    private static SettingChoice[] CreateNow(ChoiceValue.ListRow[] accounts) =>
    [
        new("autounattend-account", new ChoiceValue.Option(2)),
        new("autounattend-accounts", new ChoiceValue.List(accounts)),
    ];

    // InputLocale is the format's LCID paired with the layout id: no one setting names it, so it has its own writer.
    private static XDocument WriteRegion(params SettingChoice[] choices)
    {
        var doc = AutounattendDocument.NewSkeleton();
        var context = ContextWith(choices);
        Elements.Write(doc, context);
        new TimeRegionLanguageWriter().Write(doc, context);
        return doc;
    }

    private static XElement? ComponentOrNull(XDocument doc, string pass, string name) =>
        doc.Pass(pass).Elements(U + "component").FirstOrDefault(c => (string?)c.Attribute("name") == name);

    private static List<XElement> RunSynchronous(XDocument doc, string pass, string name) =>
        ComponentOrNull(doc, pass, name)!.Element(U + "RunSynchronous")!.Elements(U + "RunSynchronousCommand").ToList();

    private static string Path(XElement command) => command.Element(U + "Path")!.Value;

    private static string? Description(XElement command) => (string?)command.Element(U + "Description");

    [Fact]
    public void The_context_answers_a_keyed_selections_key_and_nothing_else()
    {
        var context = new AutounattendRenderContext("Write-Host 'test'", "0.0.0", SetWith(
            new SettingChoice("region-time-zone", new ChoiceValue.Keyed("W. Europe Standard Time", "(UTC+01:00) Amsterdam")),
            new SettingChoice("region-format", new ChoiceValue.Keyed(string.Empty, string.Empty)),
            new SettingChoice("theme-mode-windows", new ChoiceValue.Option(1))));

        context.Key("region-time-zone").Should().Be("W. Europe Standard Time");
        context.Key("region-format").Should().BeNull();
        context.Key("theme-mode-windows").Should().BeNull();
        context.Key("region-keyboard-layout").Should().BeNull();
        Context.Key("region-time-zone").Should().BeNull();
    }

    [Fact]
    public void ProductKey_Ask_writes_the_zero_key_and_always_shows_the_edition_list()
    {
        var doc = Write(new ProductKeyWriter());

        var userData = ComponentOrNull(doc, "windowsPE", "Microsoft-Windows-Setup")!.Element(U + "UserData")!;
        userData.Elements().Select(e => e.Name.LocalName).Should().Equal("ProductKey", "AcceptEula");
        var productKey = userData.Element(U + "ProductKey")!;
        productKey.Elements().Select(e => e.Name.LocalName).Should().Equal("Key", "WillShowUI");
        productKey.Element(U + "Key")!.Value.Should().Be("00000-00000-00000-00000-00000");
        productKey.Element(U + "WillShowUI")!.Value.Should().Be("Always");
        userData.Element(U + "AcceptEula")!.Value.Should().Be("true");
        ComponentOrNull(doc, "specialize", "Microsoft-Windows-Shell-Setup").Should().BeNull();
    }

    [Fact]
    public void ProductKey_Generic_writes_the_editions_install_key_and_hides_the_ui()
    {
        var doc = Write(new ProductKeyWriter(), new SettingChoice("autounattend-edition", new ChoiceValue.Option(4)));

        var productKey = ComponentOrNull(doc, "windowsPE", "Microsoft-Windows-Setup")!.Element(U + "UserData")!.Element(U + "ProductKey")!;
        productKey.Element(U + "Key")!.Value.Should().Be("VK7JG-NPHTM-C97JM-9MPGT-3V66T");
        productKey.Element(U + "WillShowUI")!.Value.Should().Be("OnError");
        ComponentOrNull(doc, "specialize", "Microsoft-Windows-Shell-Setup").Should().BeNull();
    }

    [Fact]
    public void ProductKey_an_edition_index_the_catalog_does_not_have_falls_back_to_the_default()
    {
        var doc = Write(new ProductKeyWriter(), new SettingChoice("autounattend-edition", new ChoiceValue.Option(99)));

        var productKey = ComponentOrNull(doc, "windowsPE", "Microsoft-Windows-Setup")!.Element(U + "UserData")!.Element(U + "ProductKey")!;
        productKey.Element(U + "Key")!.Value.Should().Be("00000-00000-00000-00000-00000");
        productKey.Element(U + "WillShowUI")!.Value.Should().Be("Always");
    }

    [Fact]
    public void ProductKey_OwnKey_installs_and_activates_with_it()
    {
        var doc = Write(
            new ProductKeyWriter(),
            new SettingChoice("autounattend-edition", new ChoiceValue.Option(14)),
            new SettingChoice("autounattend-product-key", new ChoiceValue.Text("AAAAA-BBBBB-CCCCC-DDDDD-EEEEE")));

        var productKey = ComponentOrNull(doc, "windowsPE", "Microsoft-Windows-Setup")!.Element(U + "UserData")!.Element(U + "ProductKey")!;
        productKey.Element(U + "Key")!.Value.Should().Be("AAAAA-BBBBB-CCCCC-DDDDD-EEEEE");
        productKey.Element(U + "WillShowUI")!.Value.Should().Be("OnError");
        ComponentOrNull(doc, "specialize", "Microsoft-Windows-Shell-Setup")!.Element(U + "ProductKey")!.Value
            .Should().Be("AAAAA-BBBBB-CCCCC-DDDDD-EEEEE");
    }

    [Fact]
    public void ProductKey_own_key_with_nothing_typed_writes_the_ask_placeholder()
    {
        var doc = Write(
            new ProductKeyWriter(),
            new SettingChoice("autounattend-edition", new ChoiceValue.Option(14)),
            new SettingChoice("autounattend-product-key", new ChoiceValue.Text(string.Empty)));

        var productKey = ComponentOrNull(doc, "windowsPE", "Microsoft-Windows-Setup")!.Element(U + "UserData")!.Element(U + "ProductKey")!;
        productKey.Element(U + "Key")!.Value.Should().Be("00000-00000-00000-00000-00000");
        productKey.Element(U + "WillShowUI")!.Value.Should().Be("Always");
        ComponentOrNull(doc, "specialize", "Microsoft-Windows-Shell-Setup").Should().BeNull();
    }

    [Fact]
    public void ProductKey_Firmware_writes_no_key_and_never_shows_the_ui()
    {
        var doc = Write(new ProductKeyWriter(), new SettingChoice("autounattend-edition", new ChoiceValue.Option(15)));

        var productKey = ComponentOrNull(doc, "windowsPE", "Microsoft-Windows-Setup")!.Element(U + "UserData")!.Element(U + "ProductKey")!;
        productKey.Element(U + "Key").Should().BeNull();
        productKey.Element(U + "WillShowUI")!.Value.Should().Be("Never");
    }

    [Fact]
    public void HardwareChecks_off_writes_six_ordered_bypasses_with_one_description()
    {
        var commands = RunSynchronous(Write(Elements), "windowsPE", "Microsoft-Windows-Setup");

        commands.Select(c => c.Element(U + "Order")!.Value).Should().Equal("1", "2", "3", "4", "5", "6");
        commands.Select(c => Path(c)).Should().Equal(HardwareCheckNames.Select(
            check => $"reg.exe add \"HKLM\\SYSTEM\\Setup\\LabConfig\" /v {check} /t REG_DWORD /d 1 /f"));
        Description(commands[0]).Should().Be("Order 1-6: Skip Windows 11 Hardware Requirement Checks");
        commands.Skip(1).Should().OnlyContain(c => c.Element(U + "Description") == null);
    }

    [Fact]
    public void HardwareChecks_on_writes_nothing()
    {
        var doc = Write(Elements, new SettingChoice("autounattend-hardware-checks", new ChoiceValue.Toggle(true)));

        ComponentOrNull(doc, "windowsPE", "Microsoft-Windows-Setup").Should().BeNull();
    }

    [Fact]
    public void Setup_screens_default_to_hidden_with_privacy_off()
    {
        var doc = Write(Elements);

        var oobe = ComponentOrNull(doc, "oobeSystem", "Microsoft-Windows-Shell-Setup")!.Element(U + "OOBE")!;
        oobe.Elements().Select(e => (e.Name.LocalName, e.Value)).Should().Equal(
            ("HideEULAPage", "true"), ("HideOEMRegistrationScreen", "true"), ("HideOnlineAccountScreens", "true"),
            ("HideWirelessSetupInOOBE", "true"), ("ProtectYourPC", "3"));
    }

    [Fact]
    public void Privacy_express_and_ask_change_only_ProtectYourPC()
    {
        var express = Write(Elements, new SettingChoice("autounattend-privacy", new ChoiceValue.Option(1)));
        var ask = Write(
            Elements,
            new SettingChoice("autounattend-privacy", new ChoiceValue.Option(2)),
            new SettingChoice("autounattend-licence-page", new ChoiceValue.Toggle(true)));

        ComponentOrNull(express, "oobeSystem", "Microsoft-Windows-Shell-Setup")!.Element(U + "OOBE")!.Element(U + "ProtectYourPC")!.Value.Should().Be("1");
        var askOobe = ComponentOrNull(ask, "oobeSystem", "Microsoft-Windows-Shell-Setup")!.Element(U + "OOBE")!;
        askOobe.Element(U + "ProtectYourPC").Should().BeNull();
        askOobe.Element(U + "HideEULAPage").Should().BeNull();
        askOobe.Elements().Select(e => e.Name.LocalName).Should().Equal("HideOEMRegistrationScreen", "HideOnlineAccountScreens", "HideWirelessSetupInOOBE");
    }

    [Fact]
    public void Each_setup_screen_toggle_removes_only_its_own_element()
    {
        var noOem = Write(Elements, new SettingChoice("autounattend-oem-registration-page", new ChoiceValue.Toggle(true)));
        var noNetwork = Write(Elements, new SettingChoice("autounattend-network-page", new ChoiceValue.Toggle(true)));

        ComponentOrNull(noOem, "oobeSystem", "Microsoft-Windows-Shell-Setup")!.Element(U + "OOBE")!
            .Elements().Select(e => e.Name.LocalName).Should().Equal("HideEULAPage", "HideOnlineAccountScreens", "HideWirelessSetupInOOBE", "ProtectYourPC");
        ComponentOrNull(noNetwork, "oobeSystem", "Microsoft-Windows-Shell-Setup")!.Element(U + "OOBE")!
            .Elements().Select(e => e.Name.LocalName).Should().Equal("HideEULAPage", "HideOEMRegistrationScreen", "HideOnlineAccountScreens", "ProtectYourPC");
    }

    [Fact]
    public void A_file_that_writes_nothing_into_Shell_Setup_still_carries_the_empty_component()
    {
        var doc = Write(
            Elements,
            new SettingChoice("autounattend-licence-page", new ChoiceValue.Toggle(true)),
            new SettingChoice("autounattend-oem-registration-page", new ChoiceValue.Toggle(true)),
            new SettingChoice("autounattend-network-page", new ChoiceValue.Toggle(true)),
            new SettingChoice("autounattend-privacy", new ChoiceValue.Option(2)),
            new SettingChoice("autounattend-account", new ChoiceValue.Option(1)));

        ComponentOrNull(doc, "oobeSystem", "Microsoft-Windows-Shell-Setup")!.Elements().Should().BeEmpty();
    }

    [Fact]
    public void NetFx3_on_adds_the_offline_dism_command()
    {
        var setting = SettingCatalog.ById["autounattend-netfx3"];
        var payload = (AutounattendCommand)setting.States.Single(s => s.Label == TwoState.OnLabel(setting.Control)).Effects.Single();
        // A Microsoft account keeps the local-account commands out of the same Deployment list.
        var doc = Write(Elements, new SettingChoice("autounattend-account", new ChoiceValue.Option(1)));

        var commands = RunSynchronous(doc, "specialize", "Microsoft-Windows-Deployment");
        commands.Should().ContainSingle();
        Path(commands[0]).Should().Be(payload.Command);
        Path(commands[0]).Should().Contain("/FeatureName:NetFx3").And.Contain("/LimitAccess");
        Description(commands[0]).Should().Be("Enables .NET Framework 3.5 from Windows Installation Media");
    }

    [Fact]
    public void NetFx3_off_writes_nothing()
    {
        var doc = Write(Elements, new SettingChoice("autounattend-netfx3", new ChoiceValue.Toggle(false)), new SettingChoice("autounattend-account", new ChoiceValue.Option(1)));

        ComponentOrNull(doc, "specialize", "Microsoft-Windows-Deployment").Should().BeNull();
    }

    [Fact]
    public void Account_local_keeps_oobe_offline_and_restores_the_adapters_at_first_logon()
    {
        // NetFx3 off keeps its dism command out of the Deployment list being counted.
        var doc = Write(Elements, new SettingChoice("autounattend-netfx3", new ChoiceValue.Toggle(false)));

        var commands = RunSynchronous(doc, "specialize", "Microsoft-Windows-Deployment");
        commands.Should().HaveCount(2);
        Path(commands[0]).Should().Contain("BypassNRO");
        Description(commands[0]).Should().Be("Registry Entry to Create a Local Account during OOBE");
        Path(commands[1]).Should().Contain("Disable-NetAdapter");
        Description(commands[1]).Should().StartWith("Disable All Network Adapters Temporarily");

        var shell = ComponentOrNull(doc, "oobeSystem", "Microsoft-Windows-Shell-Setup")!;
        shell.Element(U + "OOBE")!.Element(U + "HideOnlineAccountScreens")!.Value.Should().Be("true");
        var firstLogon = shell.Element(U + "FirstLogonCommands")!.Elements(U + "SynchronousCommand").ToList();
        firstLogon.Should().ContainSingle();
        firstLogon[0].Element(U + "CommandLine")!.Value.Should().Contain("Enable-NetAdapter");
        firstLogon[0].Element(U + "Description")!.Value.Should().Be("Enables Network Adapters After OOBE Completes");
    }

    [Fact]
    public void Account_create_now_writes_the_accounts_and_none_of_the_local_mode_commands()
    {
        var doc = Write(new AccountWriter(), CreateNow(AdministratorAndStandardUser));

        ComponentOrNull(doc, "specialize", "Microsoft-Windows-Deployment").Should().BeNull();

        var shell = ComponentOrNull(doc, "oobeSystem", "Microsoft-Windows-Shell-Setup")!;
        var accounts = shell.Element(U + "UserAccounts")!.Element(U + "LocalAccounts")!
            .Elements(U + "LocalAccount").ToList();
        accounts.Should().HaveCount(2);
        accounts.Should().OnlyContain(a => (string?)a.Attribute(AutounattendDocument.Wcm + "action") == "add");
        accounts[0].Elements().Select(e => e.Name.LocalName).Should().Equal("Name", "DisplayName", "Group", "Password");
        accounts[0].Element(U + "Name")!.Value.Should().Be("Marco");
        accounts[0].Element(U + "DisplayName")!.Value.Should().Be("Marco du Plessis");
        accounts[0].Element(U + "Group")!.Value.Should().Be("Administrators");
        accounts[1].Element(U + "Group")!.Value.Should().Be("Users");
    }

    [Fact]
    public void An_empty_password_is_written_as_an_empty_plain_text_value()
    {
        var doc = Write(new AccountWriter(), CreateNow(AdministratorWithNoPassword));

        var password = LocalAccounts(doc)[0].Element(U + "Password")!;
        password.Elements().Select(e => e.Name.LocalName).Should().Equal("Value", "PlainText");
        password.Element(U + "Value")!.Value.Should().BeEmpty();
        // <Value></Value>, never <Value />: Setup reads the self-closing form as no password element at all.
        password.Element(U + "Value")!.IsEmpty.Should().BeFalse();
        password.Element(U + "PlainText")!.Value.Should().Be("true");
    }

    [Fact]
    public void An_obscured_password_is_base64_of_the_password_plus_the_element_name()
    {
        var doc = Write(new AccountWriter(), CreateNow(AdministratorWithPassword));

        var password = LocalAccounts(doc)[0].Element(U + "Password")!;
        password.Element(U + "PlainText")!.Value.Should().Be("false");
        password.Element(U + "Value")!.Value.Should().Be(AccountPasswords.Obscure("hunter2", "Password"));
        System.Text.Encoding.Unicode.GetString(Convert.FromBase64String(password.Element(U + "Value")!.Value))
            .Should().Be("hunter2Password");
    }

    [Fact]
    public void The_documented_microsoft_examples_decode_through_the_same_rule()
    {
        // Microsoft never writes the rule down; its documented examples are the only evidence for it.
        AccountPasswords.Obscure("pw", "Password").TrimEnd('=').Should().Be("cAB3AFAAYQBzAHMAdwBvAHIAZAA");
        AccountPasswords.Obscure("password", "Password").Should().Be("cABhAHMAcwB3AG8AcgBkAFAAYQBzAHMAdwBvAHIAZAA=");
    }

    [Fact]
    public void A_plain_text_password_is_written_as_typed()
    {
        var doc = Write(new AccountWriter(), CreateNow(PlainTextAdministrator));

        var password = LocalAccounts(doc)[0].Element(U + "Password")!;
        password.Element(U + "Value")!.Value.Should().Be("hunter2");
        password.Element(U + "PlainText")!.Value.Should().Be("true");
    }

    [Fact]
    public void Auto_logon_names_the_account_and_repeats_its_password_with_a_logon_count()
    {
        var doc = Write(new AccountWriter(), CreateNow(AdministratorWithPasswordSigningIn));

        var autoLogon = ComponentOrNull(doc, "oobeSystem", "Microsoft-Windows-Shell-Setup")!.Element(U + "AutoLogon")!;
        autoLogon.Elements().Select(e => e.Name.LocalName).Should().Equal("Password", "Enabled", "LogonCount", "Username");
        autoLogon.Element(U + "Enabled")!.Value.Should().Be("true");
        autoLogon.Element(U + "LogonCount")!.Value.Should().Be("1");
        autoLogon.Element(U + "Username")!.Value.Should().Be("Marco");
        autoLogon.Element(U + "Password")!.Element(U + "Value")!.Value
            .Should().Be(AccountPasswords.Obscure("hunter2", "Password"));
        autoLogon.Element(U + "Password")!.Element(U + "PlainText")!.Value.Should().Be("false");
    }

    [Fact]
    public void Auto_logon_for_an_account_with_no_password_writes_an_empty_value()
    {
        var doc = Write(new AccountWriter(), CreateNow(AdministratorWithNoPasswordSigningIn));

        var password = ComponentOrNull(doc, "oobeSystem", "Microsoft-Windows-Shell-Setup")!
            .Element(U + "AutoLogon")!.Element(U + "Password")!;
        password.Element(U + "Value")!.Value.Should().BeEmpty();
        password.Element(U + "PlainText")!.Value.Should().Be("true");
    }

    [Fact]
    public void A_password_in_the_file_earns_the_command_that_deletes_the_cached_copy()
    {
        var doc = Write(new AccountWriter(), CreateNow(AdministratorAndStandardUser));

        var commands = ComponentOrNull(doc, "oobeSystem", "Microsoft-Windows-Shell-Setup")!
            .Element(U + "FirstLogonCommands")!.Elements(U + "SynchronousCommand").ToList();
        commands.Should().ContainSingle();
        commands[0].Element(U + "Order")!.Value.Should().Be("1");
        commands[0].Element(U + "CommandLine")!.Value.Should().Be(SettingCatalog.ById["autounattend-accounts"].List!.PasswordCleanup!.Command);
        commands[0].Element(U + "Description")!.Value.Should().Be(SettingCatalog.ById["autounattend-accounts"].List!.PasswordCleanup!.Description);
    }

    [Fact]
    public void A_file_with_no_password_in_it_gets_no_cleanup_command()
    {
        var doc = Write(new AccountWriter(), CreateNow(AdministratorWithNoPassword));

        ComponentOrNull(doc, "oobeSystem", "Microsoft-Windows-Shell-Setup")!
            .Element(U + "FirstLogonCommands").Should().BeNull();
    }

    [Fact]
    public void Two_accounts_with_no_password_between_them_get_no_cleanup_command()
    {
        var doc = Write(new AccountWriter(), CreateNow(TwoAccountsWithNoPasswords));

        ComponentOrNull(doc, "oobeSystem", "Microsoft-Windows-Shell-Setup")!
            .Element(U + "FirstLogonCommands").Should().BeNull();
    }

    [Fact]
    public void Create_now_with_no_accounts_writes_the_file_local_mode_writes()
    {
        var createNow = Write(new AccountWriter(), CreateNow(NoAccounts));

        createNow.ToString().Should().Be(LocalAccountState().ToString());
    }

    private static XDocument LocalAccountState()
    {
        var doc = AutounattendDocument.NewSkeleton();
        var mode = SettingCatalog.ById["autounattend-account"];
        AutounattendElementWriter.WriteState(doc, mode, mode.States.Single(s => s.Label == LocKey.Setting.AutounattendAccount.Option0));
        return doc;
    }

    [Fact]
    public void Create_now_with_no_account_list_at_all_writes_the_file_local_mode_writes()
    {
        var modeOnly = Write(new AccountWriter(), new SettingChoice("autounattend-account", new ChoiceValue.Option(2)));

        modeOnly.ToString().Should().Be(LocalAccountState().ToString());
    }

    [Fact]
    public void The_shell_setup_children_stay_in_schema_order_in_create_now_mode()
    {
        var doc = AutounattendDocument.NewSkeleton();
        var context = new AutounattendRenderContext(
            "Write-Host 'test'", "0.0.0", SetWith(CreateNow(AdministratorWithPasswordSigningIn)));

        new AccountWriter().Write(doc, context);
        Elements.Write(doc, context);

        ComponentOrNull(doc, "oobeSystem", "Microsoft-Windows-Shell-Setup")!
            .Elements().Select(e => e.Name.LocalName)
            .Should().Equal("OOBE", "UserAccounts", "AutoLogon", "FirstLogonCommands");
    }

    private static List<XElement> LocalAccounts(XDocument doc) =>
        ComponentOrNull(doc, "oobeSystem", "Microsoft-Windows-Shell-Setup")!
            .Element(U + "UserAccounts")!.Element(U + "LocalAccounts")!.Elements(U + "LocalAccount").ToList();

    [Fact]
    public void A_file_that_carries_no_locales_writes_no_component_at_all()
    {
        var doc = Write(new TimeRegionLanguageWriter());

        doc.Descendants(U + "component").Should().BeEmpty();
    }

    [Fact]
    public void All_three_locale_settings_write_one_oobeSystem_component_in_schema_order()
    {
        var doc = WriteRegion(
            new SettingChoice("region-format", new ChoiceValue.Keyed("de-DE", "German (Germany)")),
            new SettingChoice("region-system-locale", new ChoiceValue.Keyed("en-GB", "English (United Kingdom)")),
            new SettingChoice("region-keyboard-layout", new ChoiceValue.Keyed("00000407", "German")));

        // Setup asks for the language and keyboard on its own first screen, so windowsPE gets nothing.
        ComponentOrNull(doc, "windowsPE", "Microsoft-Windows-International-Core-WinPE").Should().BeNull();
        ComponentOrNull(doc, "oobeSystem", "Microsoft-Windows-International-Core")!
            .Elements().Select(e => (e.Name.LocalName, e.Value)).Should().Equal(
                ("InputLocale", "0407:00000407"),
                ("SystemLocale", "en-GB"),
                ("UserLocale", "de-DE"));
    }

    [Fact]
    public void Only_the_LCID_half_of_InputLocale_is_uppercased_the_KLID_half_is_written_as_given()
    {
        var doc = WriteRegion(
            new SettingChoice("region-format", new ChoiceValue.Keyed("hr-HR", "Croatian (Croatia)")),
            new SettingChoice("region-keyboard-layout", new ChoiceValue.Keyed("0000041A", "Croatian")));

        ComponentOrNull(doc, "oobeSystem", "Microsoft-Windows-International-Core")!
            .Elements().Select(e => (e.Name.LocalName, e.Value)).Should().Equal(
                ("InputLocale", "041A:0000041A"),
                ("UserLocale", "hr-HR"));
    }

    [Fact]
    public void A_keyboard_with_no_regional_format_beside_it_writes_no_InputLocale()
    {
        var alone = Write(
            new TimeRegionLanguageWriter(),
            new SettingChoice("region-keyboard-layout", new ChoiceValue.Keyed("00000407", "German")));
        var withSystemLocale = WriteRegion(
            new SettingChoice("region-keyboard-layout", new ChoiceValue.Keyed("00000407", "German")),
            new SettingChoice("region-system-locale", new ChoiceValue.Keyed("en-GB", "English (United Kingdom)")));

        alone.Descendants(U + "component").Should().BeEmpty();
        ComponentOrNull(withSystemLocale, "oobeSystem", "Microsoft-Windows-International-Core")!
            .Elements().Select(e => e.Name.LocalName).Should().Equal("SystemLocale");
    }

    [Fact]
    public void The_regional_format_alone_writes_only_UserLocale()
    {
        var doc = WriteRegion(
            new SettingChoice("region-format", new ChoiceValue.Keyed("en-ZA", "English (South Africa)")));

        ComponentOrNull(doc, "windowsPE", "Microsoft-Windows-International-Core-WinPE").Should().BeNull();
        ComponentOrNull(doc, "oobeSystem", "Microsoft-Windows-International-Core")!
            .Elements().Select(e => (e.Name.LocalName, e.Value)).Should().Equal(("UserLocale", "en-ZA"));
    }

    [Fact]
    public void A_culture_with_no_LCID_drops_the_pair_and_keeps_the_locale()
    {
        // ICU and NLS disagree on which cultures have no LCID, so the culture is found at run time, not named.
        var culture = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
            .First(c => c.LCID == TimeRegionLanguageService.NoLcid);
        var doc = WriteRegion(
            new SettingChoice("region-format", new ChoiceValue.Keyed(culture.Name, culture.EnglishName)),
            new SettingChoice("region-keyboard-layout", new ChoiceValue.Keyed("00000407", "German")));

        ComponentOrNull(doc, "oobeSystem", "Microsoft-Windows-International-Core")!
            .Elements().Select(e => (e.Name.LocalName, e.Value)).Should().Equal(("UserLocale", culture.Name));
    }

    [Fact]
    public void The_chosen_time_zone_is_written_into_specialize()
    {
        var doc = Write(
            Elements,
            new SettingChoice(
                "region-time-zone",
                new ChoiceValue.Keyed("South Africa Standard Time", "(UTC+02:00) Harare, Pretoria")));

        ComponentOrNull(doc, "specialize", "Microsoft-Windows-Shell-Setup")!
            .Element(U + "TimeZone")!.Value.Should().Be("South Africa Standard Time");
    }

    [Fact]
    public void A_file_that_carries_no_time_zone_writes_no_component()
    {
        var none = Write(Elements);
        var otherSettings = Write(
            Elements,
            new SettingChoice("region-format", new ChoiceValue.Keyed("en-ZA", "English (South Africa)")));
        var emptyKey = Write(
            Elements,
            new SettingChoice("region-time-zone", new ChoiceValue.Keyed(string.Empty, string.Empty)));

        ComponentOrNull(none, "specialize", "Microsoft-Windows-Shell-Setup").Should().BeNull();
        ComponentOrNull(otherSettings, "specialize", "Microsoft-Windows-Shell-Setup").Should().BeNull();
        ComponentOrNull(emptyKey, "specialize", "Microsoft-Windows-Shell-Setup").Should().BeNull();
    }

    [Fact]
    public void ComputerName_explicit_writes_the_name_into_specialize()
    {
        var doc = Write(Elements, new SettingChoice("autounattend-computer-name", new ChoiceValue.Text("WORKSHOP")));

        ComponentOrNull(doc, "specialize", "Microsoft-Windows-Shell-Setup")!
            .Element(U + "ComputerName")!.Value.Should().Be("WORKSHOP");
    }

    [Fact]
    public void ComputerName_left_to_setup_or_empty_writes_nothing()
    {
        var left = Write(Elements);
        var empty = Write(Elements, new SettingChoice("autounattend-computer-name", new ChoiceValue.Text(string.Empty)));

        ComponentOrNull(left, "specialize", "Microsoft-Windows-Shell-Setup").Should().BeNull();
        ComponentOrNull(empty, "specialize", "Microsoft-Windows-Shell-Setup").Should().BeNull();
    }

    [Fact]
    public void The_specialize_shell_setup_children_stay_in_schema_order()
    {
        var doc = AutounattendDocument.NewSkeleton();
        var context = new AutounattendRenderContext("Write-Host 'test'", "0.0.0", SetWith(
            new SettingChoice("autounattend-edition", new ChoiceValue.Option(14)),
            new SettingChoice("autounattend-product-key", new ChoiceValue.Text("VK7JG-NPHTM-C97JM-9MPGT-3V66T")),
            new SettingChoice("autounattend-computer-name", new ChoiceValue.Text("WORKSHOP")),
            new SettingChoice("region-time-zone", new ChoiceValue.Keyed("UTC", "(UTC) Coordinated Universal Time"))));

        new ProductKeyWriter().Write(doc, context);
        Elements.Write(doc, context);

        ComponentOrNull(doc, "specialize", "Microsoft-Windows-Shell-Setup")!
            .Elements().Select(e => e.Name.LocalName).Should().Equal("ComputerName", "TimeZone", "ProductKey");
    }

    [Fact]
    public void Account_microsoft_writes_nothing()
    {
        var doc = Write(new AccountWriter(), new SettingChoice("autounattend-account", new ChoiceValue.Option(1)));

        ComponentOrNull(doc, "specialize", "Microsoft-Windows-Deployment").Should().BeNull();
        ComponentOrNull(doc, "oobeSystem", "Microsoft-Windows-Shell-Setup").Should().BeNull();
    }

    [Fact]
    public void ExtractScript_resource_decodes_both_carried_shapes()
    {
        AutounattendScripts.ExtractScript.Should().StartWith("param(");
        AutounattendScripts.ExtractScript.Should().Contain("$Document.unattend.Extensions.File");
        AutounattendScripts.ExtractScript.Should().Contain("WriteAllBytes");
        AutounattendScripts.ExtractScript.Should().Contain("FromBase64String");
        AutounattendScripts.ExtractCommand.Should().Contain("C:\\Windows\\Panther\\unattend.xml").And.Contain("Extensions.ExtractScript");
    }

    [Fact]
    public void The_builder_runs_the_extractor_first_and_the_Winhancements_script_last_in_specialize()
    {
        var doc = AutounattendDocumentBuilder.Build(Carried);

        var commands = RunSynchronous(doc, "specialize", "Microsoft-Windows-Deployment");
        Path(commands[0]).Should().Be(AutounattendScripts.ExtractCommand);
        Description(commands[0]).Should().Be(AutounattendScripts.ExtractDescription);
        Path(commands[^1]).Should().Be(
            $"powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File \"{ScriptPaths.AutounattendScriptPath}\"");
        doc.Extensions().Element(X + "ExtractScript")!.Value.Should().Be(AutounattendScripts.ExtractScript);
        var file = doc.Extensions().Element(X + "File")!;
        file.Attribute("path")!.Value.Should().Be(ScriptPaths.AutounattendScriptPath);
        file.Nodes().Should().ContainSingle().Which.Should().BeOfType<XCData>().Which.Value.Should().Be("Write-Host 'carried'");
    }

    [Fact]
    public void An_embedded_picture_rides_beside_the_script_as_base64()
    {
        var set = SelectionSet.Empty with
        {
            Files = [new CarriedFile("theme-wallpaper-picture", @"C:\Windows\Web\Wallpaper\Winhance\holiday.jpg", "QUJD")],
        };
        var doc = AutounattendDocumentBuilder.Build(new AutounattendRenderContext("Write-Host 'carried'", "0.0.0", set));

        var files = doc.Extensions().Elements(X + "File").ToList();
        files.Should().HaveCount(2);
        files[0].Attribute("path")!.Value.Should().Be(ScriptPaths.AutounattendScriptPath);
        files[1].Attribute("path")!.Value.Should().Be(@"C:\Windows\Web\Wallpaper\Winhance\holiday.jpg");
        files[1].Attribute("encoding")!.Value.Should().Be("base64");
        files[1].Value.Should().Be("QUJD");
    }

    [Fact]
    public void A_picture_the_file_only_names_carries_no_picture_element()
    {
        var doc = AutounattendDocumentBuilder.Build(new AutounattendRenderContext("Write-Host 'carried'", "0.0.0", SetWith(
            new SettingChoice("theme-wallpaper-picture",
                new ChoiceValue.Keyed(@"C:\Windows\Web\Wallpaper\Windows\img0.jpg", "Windows 11 light")))));

        doc.Extensions().Elements(X + "File").Should().ContainSingle()
            .Which.Attribute("path")!.Value.Should().Be(ScriptPaths.AutounattendScriptPath);
    }

    [Fact]
    public void The_builder_stamps_its_version_first_under_Extensions()
    {
        var doc = AutounattendDocumentBuilder.Build(Carried);

        var first = doc.Extensions().Elements().First();
        first.Name.Should().Be(X + "Generator");
        first.Attribute("version")!.Value.Should().Be("26.09.04");
    }

    [Fact]
    public void Architecture_single_selection_rewrites_the_placeholder()
    {
        var doc = AutounattendDocument.NewSkeleton();
        doc.Pass("specialize").Component("Microsoft-Windows-Deployment").AddRunSynchronousCommand("cmd.exe /c one");
        doc.Pass("oobeSystem").Component("Microsoft-Windows-Shell-Setup").SetOobe("HideEULAPage", "true");

        new ArchitectureWriter().Write(doc, ContextWith(
            new SettingChoice("autounattend-architecture-arm64", new ChoiceValue.CheckBox(false)),
            new SettingChoice("autounattend-architecture-x64", new ChoiceValue.CheckBox(false))));

        doc.Descendants(U + "component").Should().HaveCount(2);
        doc.Descendants(U + "component").Should().OnlyContain(c => (string?)c.Attribute("processorArchitecture") == "x86");
    }

    [Fact]
    public void Architecture_three_selections_clone_every_component_in_the_files_order()
    {
        var doc = AutounattendDocument.NewSkeleton();
        doc.Pass("specialize").Component("Microsoft-Windows-Deployment").AddRunSynchronousCommand("cmd.exe /c one");

        new ArchitectureWriter().Write(doc, Context);

        var components = doc.Pass("specialize").Elements(U + "component").ToList();
        components.Select(c => (string?)c.Attribute("processorArchitecture")).Should().Equal("x86", "arm64", "amd64");
        foreach (var clone in components.Skip(1))
        {
            clone.SetAttributeValue("processorArchitecture", "x86");
            XNode.DeepEquals(clone, components[0]).Should().BeTrue();
        }
    }

    [Fact]
    public void Architecture_two_selections_clone_once_and_keep_the_files_order()
    {
        var doc = AutounattendDocument.NewSkeleton();
        doc.Pass("windowsPE").Component("Microsoft-Windows-Setup").AddRunSynchronousCommand("cmd.exe /c one");
        doc.Pass("specialize").Component("Microsoft-Windows-Deployment").AddRunSynchronousCommand("cmd.exe /c two");

        new ArchitectureWriter().Write(doc, ContextWith(
            new SettingChoice("autounattend-architecture-arm64", new ChoiceValue.CheckBox(false))));

        foreach (var pass in new[] { "windowsPE", "specialize" })
            doc.Pass(pass).Elements(U + "component").Select(c => (string?)c.Attribute("processorArchitecture")).Should().Equal("x86", "amd64");
    }

    [Fact]
    public void Architecture_with_none_checked_still_covers_all_three()
    {
        var doc = AutounattendDocument.NewSkeleton();
        doc.Pass("specialize").Component("Microsoft-Windows-Deployment").AddRunSynchronousCommand("cmd.exe /c one");

        new ArchitectureWriter().Write(doc, ContextWith(
            new SettingChoice("autounattend-architecture-x86", new ChoiceValue.CheckBox(false)),
            new SettingChoice("autounattend-architecture-arm64", new ChoiceValue.CheckBox(false)),
            new SettingChoice("autounattend-architecture-x64", new ChoiceValue.CheckBox(false))));

        doc.Pass("specialize").Elements(U + "component")
            .Select(c => (string?)c.Attribute("processorArchitecture")).Should().Equal("x86", "arm64", "amd64");
    }

    [Fact]
    public void Build_with_defaults_lays_specialize_out_the_way_the_template_did()
    {
        var doc = AutounattendDocumentBuilder.Build(Carried);

        foreach (var pass in new[] { "windowsPE", "specialize", "oobeSystem" })
            doc.Pass(pass).Elements(U + "component").Select(c => (string?)c.Attribute("processorArchitecture")).Should().Equal("x86", "arm64", "amd64");
        foreach (var pass in new[] { "offlineServicing", "generalize", "auditSystem", "auditUser" })
            doc.Pass(pass).HasElements.Should().BeFalse();

        var specialize = doc.Pass("specialize").Elements(U + "component").First().Element(U + "RunSynchronous")!.Elements(U + "RunSynchronousCommand").ToList();
        specialize.Select(c => (string?)c.Element(U + "Order")).Should().Equal("1", "2", "3", "4", "5");
        specialize.Select(c => c.Element(U + "Description")!.Value).Should().SatisfyRespectively(
            d => d.Should().Be("Loads Scripts in this XML File"),
            d => d.Should().Be("Registry Entry to Create a Local Account during OOBE"),
            d => d.Should().StartWith("Disable All Network Adapters Temporarily"),
            d => d.Should().Be("Enables .NET Framework 3.5 from Windows Installation Media"),
            d => d.Should().StartWith("Runs Winhance Script System Wide Operations"));
        doc.Pass("windowsPE").Elements(U + "component").First().Element(U + "RunSynchronous")!.Elements(U + "RunSynchronousCommand").Should().HaveCount(6);
        doc.Extensions().Elements().Select(e => e.Name.LocalName).Should().Equal("Generator", "ExtractScript", "File");
        doc.Root!.Elements().Last().Name.Should().Be(X + "Extensions");
    }

    [Fact]
    public void The_selection_sets_locales_and_time_zone_reach_the_assembled_document()
    {
        var context = new AutounattendRenderContext(
            "Write-Host 'carried'",
            "26.09.04",
            SetWith(
                new SettingChoice("region-format", new ChoiceValue.Keyed("en-GB", "English (United Kingdom)")),
                new SettingChoice("region-system-locale", new ChoiceValue.Keyed("en-GB", "English (United Kingdom)")),
                new SettingChoice("region-keyboard-layout", new ChoiceValue.Keyed("00000809", "United Kingdom")),
                new SettingChoice("region-time-zone", new ChoiceValue.Keyed("GMT Standard Time", "(UTC+00:00) London")),
                new SettingChoice("autounattend-computer-name", new ChoiceValue.Text("WORKSHOP"))));

        var doc = AutounattendDocumentBuilder.Build(context);

        doc.Descendants(U + "UILanguage").Should().BeEmpty();
        doc.Descendants(U + "InputLocale").Select(e => e.Value).Should().AllBe("0809:00000809");
        doc.Descendants(U + "UserLocale").Select(e => e.Value).Should().AllBe("en-GB");
        doc.Descendants(U + "TimeZone").Select(e => e.Value).Should().AllBe("GMT Standard Time");
        doc.Descendants(U + "ComputerName").Select(e => e.Value).Should().AllBe("WORKSHOP");

        doc.Pass("windowsPE").Elements(U + "component")
            .Where(c => (string?)c.Attribute("name") == "Microsoft-Windows-International-Core-WinPE")
            .Should().BeEmpty();

        // The architecture clone runs last, so every component this file carries exists three times over.
        doc.Pass("oobeSystem").Elements(U + "component")
            .Where(c => (string?)c.Attribute("name") == "Microsoft-Windows-International-Core")
            .Should().HaveCount(3);
    }

    [Fact]
    public void Serialize_writes_utf8_without_bom_and_keeps_the_cdata()
    {
        var xml = AutounattendDocumentBuilder.Serialize(AutounattendDocumentBuilder.Build(Carried));

        xml.Should().StartWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        xml.Should().NotContain("\uFEFF");
        xml.Should().Contain("\r\n");
        xml.Should().Contain("<![CDATA[Write-Host 'carried']]>");
        xml.Should().Contain("xmlns:wcm=\"http://schemas.microsoft.com/WMIConfig/2002/State\"");
        xml.Should().Contain("\r\n    <component ");
        var act = () => XDocument.Parse(xml);
        act.Should().NotThrow();
    }
}
