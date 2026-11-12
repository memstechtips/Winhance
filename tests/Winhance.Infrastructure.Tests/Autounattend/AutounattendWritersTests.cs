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
        string name, string displayName, string group, string password, bool obscure, bool autoLogon = false,
        bool changePassword = false) =>
        new(new Dictionary<string, string>
        {
            ["name"] = name,
            ["display-name"] = displayName,
            ["group"] = group,
            ["password"] = password,
            ["obscure"] = obscure ? "true" : "false",
            ["auto-logon"] = autoLogon ? "true" : "false",
            ["change-password"] = changePassword ? "true" : "false",
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

    // OobeNetworkWriter decides the adapter cut from the account mode, so it reads a card the generic writer
    // wrote; the builder runs them in this order and so does this.
    private static XDocument WriteNetwork(params SettingChoice[] choices)
    {
        var doc = AutounattendDocument.NewSkeleton();
        var context = ContextWith(choices);
        Elements.Write(doc, context);
        new OobeNetworkWriter().Write(doc, context);
        return doc;
    }

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
    public void Key_KeyedSelection_AnswersItsKeyAndNothingElse()
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
    public void Write_ProductKeyAsk_WritesTheZeroKeyAndAlwaysShowsTheEditionList()
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
    public void Write_ProductKeyGeneric_WritesTheEditionsInstallKeyAndHidesTheUi()
    {
        var doc = Write(new ProductKeyWriter(), new SettingChoice("autounattend-edition", new ChoiceValue.Option(4)));

        var productKey = ComponentOrNull(doc, "windowsPE", "Microsoft-Windows-Setup")!.Element(U + "UserData")!.Element(U + "ProductKey")!;
        productKey.Element(U + "Key")!.Value.Should().Be("VK7JG-NPHTM-C97JM-9MPGT-3V66T");
        productKey.Element(U + "WillShowUI")!.Value.Should().Be("OnError");
        ComponentOrNull(doc, "specialize", "Microsoft-Windows-Shell-Setup").Should().BeNull();
    }

    [Fact]
    public void Write_ProductKeyEditionIndexNotInTheCatalog_FallsBackToTheDefault()
    {
        var doc = Write(new ProductKeyWriter(), new SettingChoice("autounattend-edition", new ChoiceValue.Option(99)));

        var productKey = ComponentOrNull(doc, "windowsPE", "Microsoft-Windows-Setup")!.Element(U + "UserData")!.Element(U + "ProductKey")!;
        productKey.Element(U + "Key")!.Value.Should().Be("00000-00000-00000-00000-00000");
        productKey.Element(U + "WillShowUI")!.Value.Should().Be("Always");
    }

    [Fact]
    public void Write_ProductKeyOwnKey_InstallsAndActivatesWithIt()
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
    public void Write_ProductKeyOwnKeyWithNothingTyped_WritesTheAskPlaceholder()
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
    public void Write_ProductKeyFirmware_WritesNoKeyAndNeverShowsTheUi()
    {
        var doc = Write(new ProductKeyWriter(), new SettingChoice("autounattend-edition", new ChoiceValue.Option(15)));

        var productKey = ComponentOrNull(doc, "windowsPE", "Microsoft-Windows-Setup")!.Element(U + "UserData")!.Element(U + "ProductKey")!;
        productKey.Element(U + "Key").Should().BeNull();
        productKey.Element(U + "WillShowUI")!.Value.Should().Be("Never");
    }

    [Fact]
    public void Write_HardwareChecksOff_WritesSixOrderedBypassesWithOneDescription()
    {
        var commands = RunSynchronous(Write(Elements), "windowsPE", "Microsoft-Windows-Setup");

        commands.Select(c => c.Element(U + "Order")!.Value).Should().Equal("1", "2", "3", "4", "5", "6");
        commands.Select(c => Path(c)).Should().Equal(HardwareCheckNames.Select(
            check => $"reg.exe add \"HKLM\\SYSTEM\\Setup\\LabConfig\" /v {check} /t REG_DWORD /d 1 /f"));
        Description(commands[0]).Should().Be("Order 1-6: Skip Windows 11 Hardware Requirement Checks");
        commands.Skip(1).Should().OnlyContain(c => c.Element(U + "Description") == null);
    }

    [Fact]
    public void Write_HardwareChecksOn_WritesNothing()
    {
        var doc = Write(Elements, new SettingChoice("autounattend-hardware-checks", new ChoiceValue.Toggle(true)));

        ComponentOrNull(doc, "windowsPE", "Microsoft-Windows-Setup").Should().BeNull();
    }

    [Fact]
    public void Write_NoChoices_HidesTheSetupScreensAndTurnsPrivacyOff()
    {
        var doc = Write(Elements);

        var oobe = ComponentOrNull(doc, "oobeSystem", "Microsoft-Windows-Shell-Setup")!.Element(U + "OOBE")!;
        oobe.Elements().Select(e => (e.Name.LocalName, e.Value)).Should().Equal(
            ("HideEULAPage", "true"), ("HideOEMRegistrationScreen", "true"), ("HideOnlineAccountScreens", "true"),
            ("HideWirelessSetupInOOBE", "true"), ("ProtectYourPC", "3"));
    }

    [Fact]
    public void Write_PrivacyExpressAndAsk_ChangeOnlyProtectYourPc()
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
    public void Write_EachSetupScreenToggle_RemovesOnlyItsOwnElement()
    {
        var noOem = Write(Elements, new SettingChoice("autounattend-oem-registration-page", new ChoiceValue.Toggle(true)));
        var noNetwork = Write(Elements, new SettingChoice("autounattend-network-page", new ChoiceValue.Toggle(true)));

        ComponentOrNull(noOem, "oobeSystem", "Microsoft-Windows-Shell-Setup")!.Element(U + "OOBE")!
            .Elements().Select(e => e.Name.LocalName).Should().Equal("HideEULAPage", "HideOnlineAccountScreens", "HideWirelessSetupInOOBE", "ProtectYourPC");
        ComponentOrNull(noNetwork, "oobeSystem", "Microsoft-Windows-Shell-Setup")!.Element(U + "OOBE")!
            .Elements().Select(e => e.Name.LocalName).Should().Equal("HideEULAPage", "HideOEMRegistrationScreen", "HideOnlineAccountScreens", "ProtectYourPC");
    }

    [Fact]
    public void Write_NothingForShellSetup_StillCarriesTheEmptyComponent()
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
    public void Write_NetFx3On_AddsTheOfflineDismCommand()
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
    public void Write_NetFx3Off_WritesNothing()
    {
        var doc = Write(Elements, new SettingChoice("autounattend-netfx3", new ChoiceValue.Toggle(false)), new SettingChoice("autounattend-account", new ChoiceValue.Option(1)));

        ComponentOrNull(doc, "specialize", "Microsoft-Windows-Deployment").Should().BeNull();
    }

    [Fact]
    public void Write_AccountLocal_KeepsOobeOfflineAndRestoresTheAdaptersAtFirstLogon()
    {
        // NetFx3 off keeps its dism command out of the Deployment list being counted.
        var doc = WriteNetwork(new SettingChoice("autounattend-netfx3", new ChoiceValue.Toggle(false)));

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
    public void Write_AccountCreateNow_WritesTheAccountsAndNoLocalModeCommands()
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
    public void Write_EmptyPassword_WritesAnEmptyPlainTextValue()
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
    public void Write_ObscuredPassword_IsBase64OfThePasswordPlusTheElementName()
    {
        var doc = Write(new AccountWriter(), CreateNow(AdministratorWithPassword));

        var password = LocalAccounts(doc)[0].Element(U + "Password")!;
        password.Element(U + "PlainText")!.Value.Should().Be("false");
        password.Element(U + "Value")!.Value.Should().Be(AccountPasswords.Obscure("hunter2", "Password"));
        System.Text.Encoding.Unicode.GetString(Convert.FromBase64String(password.Element(U + "Value")!.Value))
            .Should().Be("hunter2Password");
    }

    [Fact]
    public void Obscure_DocumentedMicrosoftExamples_DecodeThroughTheSameRule()
    {
        // Microsoft never writes the rule down; its documented examples are the only evidence for it.
        AccountPasswords.Obscure("pw", "Password").TrimEnd('=').Should().Be("cAB3AFAAYQBzAHMAdwBvAHIAZAA");
        AccountPasswords.Obscure("password", "Password").Should().Be("cABhAHMAcwB3AG8AcgBkAFAAYQBzAHMAdwBvAHIAZAA=");
    }

    [Fact]
    public void Write_PlainTextPassword_IsWrittenAsTyped()
    {
        var doc = Write(new AccountWriter(), CreateNow(PlainTextAdministrator));

        var password = LocalAccounts(doc)[0].Element(U + "Password")!;
        password.Element(U + "Value")!.Value.Should().Be("hunter2");
        password.Element(U + "PlainText")!.Value.Should().Be("true");
    }

    [Fact]
    public void Write_AutoLogon_NamesTheAccountAndRepeatsItsPasswordWithALogonCount()
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
    public void Write_AutoLogonWithNoPassword_WritesAnEmptyValue()
    {
        var doc = Write(new AccountWriter(), CreateNow(AdministratorWithNoPasswordSigningIn));

        var password = ComponentOrNull(doc, "oobeSystem", "Microsoft-Windows-Shell-Setup")!
            .Element(U + "AutoLogon")!.Element(U + "Password")!;
        password.Element(U + "Value")!.Value.Should().BeEmpty();
        password.Element(U + "PlainText")!.Value.Should().Be("true");
    }

    [Fact]
    public void Write_PasswordInTheFile_AddsTheCommandThatDeletesTheCachedCopy()
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
    public void Write_AccountAskedForANewPassword_AddsTheCommandThatExpiresIt()
    {
        var doc = Write(new AccountWriter(), CreateNow(
        [
            Account("Marco", "Marco du Plessis", "0", string.Empty, obscure: false, changePassword: true),
            Account("Guest", "Guest account", "1", string.Empty, obscure: false),
        ]));

        var commands = ComponentOrNull(doc, "oobeSystem", "Microsoft-Windows-Shell-Setup")!
            .Element(U + "FirstLogonCommands")!.Elements(U + "SynchronousCommand").ToList();
        commands.Should().ContainSingle();
        commands[0].Element(U + "CommandLine")!.Value.Should().Be(
            "powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command \"Set-LocalUser -Name 'Marco' -PasswordNeverExpires $false; net.exe user 'Marco' /logonpasswordchg:yes\"");
    }

    [Fact]
    public void Write_AccountNameWithASpaceAndAnApostrophe_IsQuotedForPowerShell()
    {
        var doc = Write(new AccountWriter(), CreateNow(
            [Account("O'Brien Test", "O'Brien", "0", string.Empty, obscure: false, changePassword: true)]));

        ComponentOrNull(doc, "oobeSystem", "Microsoft-Windows-Shell-Setup")!
            .Element(U + "FirstLogonCommands")!.Elements(U + "SynchronousCommand").Single()
            .Element(U + "CommandLine")!.Value.Should().Be(
                "powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command \"Set-LocalUser -Name 'O''Brien Test' -PasswordNeverExpires $false; net.exe user 'O''Brien Test' /logonpasswordchg:yes\"");
    }

    [Fact]
    public void Write_AccountNameWithATypographicApostrophe_IsQuotedForPowerShell()
    {
        var doc = Write(new AccountWriter(), CreateNow(
            [Account("O\u2019Brien", "O\u2019Brien", "0", string.Empty, obscure: false, changePassword: true)]));

        ComponentOrNull(doc, "oobeSystem", "Microsoft-Windows-Shell-Setup")!
            .Element(U + "FirstLogonCommands")!.Elements(U + "SynchronousCommand").Single()
            .Element(U + "CommandLine")!.Value.Should().Be(
                "powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command \"Set-LocalUser -Name 'O\u2019\u2019Brien' -PasswordNeverExpires $false; net.exe user 'O\u2019\u2019Brien' /logonpasswordchg:yes\"");
    }

    [Fact]
    public void Write_NewPasswordRequest_RunsBeforeTheCachedAnswerFileIsDeleted()
    {
        var doc = Write(new AccountWriter(), CreateNow(
            [Account("Marco", "Marco du Plessis", "0", "hunter2", obscure: true, autoLogon: true, changePassword: true)]));

        var commands = ComponentOrNull(doc, "oobeSystem", "Microsoft-Windows-Shell-Setup")!
            .Element(U + "FirstLogonCommands")!.Elements(U + "SynchronousCommand")
            .Select(c => (Order: c.Element(U + "Order")!.Value, Command: c.Element(U + "CommandLine")!.Value))
            .ToList();
        commands.Should().Equal(
            ("1", "powershell.exe -NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command \"Set-LocalUser -Name 'Marco' -PasswordNeverExpires $false; net.exe user 'Marco' /logonpasswordchg:yes\""),
            ("2", SettingCatalog.ById["autounattend-accounts"].List!.PasswordCleanup!.Command));
    }

    [Fact]
    public void Write_FileWithNoPassword_AddsNoCleanupCommand()
    {
        var doc = Write(new AccountWriter(), CreateNow(AdministratorWithNoPassword));

        ComponentOrNull(doc, "oobeSystem", "Microsoft-Windows-Shell-Setup")!
            .Element(U + "FirstLogonCommands").Should().BeNull();
    }

    [Fact]
    public void Write_TwoAccountsWithNoPasswords_AddNoCleanupCommand()
    {
        var doc = Write(new AccountWriter(), CreateNow(TwoAccountsWithNoPasswords));

        ComponentOrNull(doc, "oobeSystem", "Microsoft-Windows-Shell-Setup")!
            .Element(U + "FirstLogonCommands").Should().BeNull();
    }

    [Fact]
    public void Write_CreateNowWithNoAccounts_WritesTheFileLocalModeWrites()
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
    public void Write_CreateNowWithNoAccountList_WritesTheFileLocalModeWrites()
    {
        var modeOnly = Write(new AccountWriter(), new SettingChoice("autounattend-account", new ChoiceValue.Option(2)));

        modeOnly.ToString().Should().Be(LocalAccountState().ToString());
    }

    [Fact]
    public void Write_CreateNowMode_KeepsShellSetupChildrenInSchemaOrder()
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
    public void Write_NoLocales_WritesNoComponentAtAll()
    {
        var doc = Write(new TimeRegionLanguageWriter());

        doc.Descendants(U + "component").Should().BeEmpty();
    }

    [Fact]
    public void Write_AllThreeLocaleSettings_WritesOneOobeSystemComponentInSchemaOrder()
    {
        var doc = WriteRegion(
            new SettingChoice("region-format", new ChoiceValue.Keyed("de-DE", "German (Germany)")),
            new SettingChoice("region-system-locale", new ChoiceValue.Keyed("en-GB", "English (United Kingdom)")),
            new SettingChoice("region-keyboard-layout", new ChoiceValue.Keyed("00000407", "German")));

        // No setup language chosen, so Setup keeps its own first screen and windowsPE gets nothing.
        ComponentOrNull(doc, "windowsPE", "Microsoft-Windows-International-Core-WinPE").Should().BeNull();
        ComponentOrNull(doc, "oobeSystem", "Microsoft-Windows-International-Core")!
            .Elements().Select(e => (e.Name.LocalName, e.Value)).Should().Equal(
                ("InputLocale", "0407:00000407"),
                ("SystemLocale", "en-GB"),
                ("UserLocale", "de-DE"));
    }

    [Fact]
    public void Write_InputLocale_UppercasesOnlyTheLcidHalf()
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
    public void Write_KeyboardWithNoRegionalFormat_WritesNoInputLocale()
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
    public void Write_RegionalFormatAlone_WritesOnlyUserLocale()
    {
        var doc = WriteRegion(
            new SettingChoice("region-format", new ChoiceValue.Keyed("en-ZA", "English (South Africa)")));

        ComponentOrNull(doc, "windowsPE", "Microsoft-Windows-International-Core-WinPE").Should().BeNull();
        ComponentOrNull(doc, "oobeSystem", "Microsoft-Windows-International-Core")!
            .Elements().Select(e => (e.Name.LocalName, e.Value)).Should().Equal(("UserLocale", "en-ZA"));
    }

    [Fact]
    public void Write_SetupLanguage_AnswersEveryQuestionSetupsFirstScreenAsks()
    {
        var doc = WriteRegion(
            new SettingChoice("autounattend-setup-language", new ChoiceValue.Keyed("en-GB", "English (United Kingdom)")),
            new SettingChoice("region-format", new ChoiceValue.Keyed("de-DE", "German (Germany)")),
            new SettingChoice("region-system-locale", new ChoiceValue.Keyed("en-GB", "English (United Kingdom)")),
            new SettingChoice("region-keyboard-layout", new ChoiceValue.Keyed("00000407", "German")));

        var setup = ComponentOrNull(doc, "windowsPE", "Microsoft-Windows-International-Core-WinPE")!;
        setup.Elements().Select(e => e.Name.LocalName).Should().Equal(
            "SetupUILanguage", "InputLocale", "SystemLocale", "UILanguage", "UserLocale");
        setup.Element(U + "SetupUILanguage")!.Element(U + "UILanguage")!.Value.Should().Be("en-GB");
        setup.Element(U + "UILanguage")!.Value.Should().Be("en-GB");
        setup.Element(U + "InputLocale")!.Value.Should().Be("0407:00000407");
        setup.Element(U + "SystemLocale")!.Value.Should().Be("en-GB");
        setup.Element(U + "UserLocale")!.Value.Should().Be("de-DE");
    }

    [Fact]
    public void Write_AskMeOption_CarriesAnEmptyKeyAndWritesNoWindowsPeComponent()
    {
        var doc = WriteRegion(
            new SettingChoice("autounattend-setup-language", new ChoiceValue.Keyed(string.Empty, "Ask me during setup")),
            new SettingChoice("region-format", new ChoiceValue.Keyed("de-DE", "German (Germany)")),
            new SettingChoice("region-keyboard-layout", new ChoiceValue.Keyed("00000407", "German")));

        ComponentOrNull(doc, "windowsPE", "Microsoft-Windows-International-Core-WinPE").Should().BeNull();
        ComponentOrNull(doc, "oobeSystem", "Microsoft-Windows-International-Core")!
            .Elements().Select(e => e.Name.LocalName).Should().Equal("InputLocale", "UserLocale");
    }

    // The region cards are the only source for the other three, so a file that excludes them still skips the
    // language half of the screen and lets Setup ask the rest.
    [Fact]
    public void Write_SetupLanguageWithNoRegionCards_WritesTheLanguageAlone()
    {
        var doc = WriteRegion(
            new SettingChoice("autounattend-setup-language", new ChoiceValue.Keyed("fr-FR", "French (France)")));

        ComponentOrNull(doc, "windowsPE", "Microsoft-Windows-International-Core-WinPE")!
            .Elements().Select(e => e.Name.LocalName).Should().Equal("SetupUILanguage", "UILanguage");
    }

    [Fact]
    public void Write_DisplayLanguage_IsWrittenIntoOobeSystem()
    {
        var doc = WriteRegion(
            new SettingChoice("region-display-language", new ChoiceValue.Keyed("nl-NL", "Dutch (Netherlands)")));

        ComponentOrNull(doc, "windowsPE", "Microsoft-Windows-International-Core-WinPE").Should().BeNull();
        ComponentOrNull(doc, "oobeSystem", "Microsoft-Windows-International-Core")!
            .Elements().Select(e => (e.Name.LocalName, e.Value)).Should().Equal(("UILanguage", "nl-NL"));
    }

    [Fact]
    public void Write_CultureWithNoLcid_DropsThePairAndKeepsTheLocale()
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
    public void Write_ChosenTimeZone_IsWrittenIntoSpecialize()
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
    public void Write_NoTimeZone_WritesNoComponent()
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
    public void Write_ComputerNameExplicit_WritesTheNameIntoSpecialize()
    {
        var doc = Write(Elements, new SettingChoice("autounattend-computer-name", new ChoiceValue.Text("WORKSHOP")));

        ComponentOrNull(doc, "specialize", "Microsoft-Windows-Shell-Setup")!
            .Element(U + "ComputerName")!.Value.Should().Be("WORKSHOP");
    }

    [Fact]
    public void Write_ComputerNameLeftToSetupOrEmpty_WritesNothing()
    {
        var left = Write(Elements);
        var empty = Write(Elements, new SettingChoice("autounattend-computer-name", new ChoiceValue.Text(string.Empty)));

        ComponentOrNull(left, "specialize", "Microsoft-Windows-Shell-Setup").Should().BeNull();
        ComponentOrNull(empty, "specialize", "Microsoft-Windows-Shell-Setup").Should().BeNull();
    }

    [Fact]
    public void Build_WithUniqueName_SetsUpAsTempNameAndCarriesRenameScriptWithTypedPrefix()
    {
        var doc = AutounattendDocumentBuilder.Build(ContextWith(
            new SettingChoice("autounattend-computer-name", new ChoiceValue.Text("OFFICE")),
            new SettingChoice(UniqueComputerName.SettingId, new ChoiceValue.Toggle(true))));

        doc.Descendants(U + "ComputerName").Select(e => e.Value).Should().AllBe("TEMPNAME");
        var script = doc.Extensions().Elements(X + "File")
            .Single(f => f.Attribute("path")!.Value == ScriptPaths.UnattendComputerNameScriptPath).Value;
        script.Should().Contain("$name = 'OFFICE-' + $ending").And.Contain("'TOBEFILLEDBYOEM'").And.NotContain("{{");

        var rename = RunSynchronous(doc, "specialize", "Microsoft-Windows-Deployment")
            .Single(c => Path(c).Contains(ScriptPaths.UnattendComputerNameScriptPath, StringComparison.Ordinal));
        Path(rename).Length.Should().BeLessThanOrEqualTo(259);
    }

    [Fact]
    public void Build_WithUniqueName_RenameScriptReadsNetworkCardBeforeAdaptersGoOff()
    {
        var doc = AutounattendDocumentBuilder.Build(ContextWith(
            new SettingChoice("autounattend-account", new ChoiceValue.Option(0)),
            new SettingChoice(UniqueComputerName.SettingId, new ChoiceValue.Toggle(true))));

        var paths = RunSynchronous(doc, "specialize", "Microsoft-Windows-Deployment").Select(Path).ToList();
        paths.FindIndex(p => p.Contains("SetComputerName.ps1", StringComparison.Ordinal))
            .Should().BeLessThan(paths.FindIndex(p => p.Contains("Disable-NetAdapter", StringComparison.Ordinal)));
    }

    [Fact]
    public void Build_WithoutUniqueName_KeepsTypedNameAndCarriesNoRenameScript()
    {
        var doc = AutounattendDocumentBuilder.Build(ContextWith(
            new SettingChoice("autounattend-computer-name", new ChoiceValue.Text("OFFICE")),
            new SettingChoice(UniqueComputerName.SettingId, new ChoiceValue.Toggle(false))));

        doc.Descendants(U + "ComputerName").Select(e => e.Value).Should().AllBe("OFFICE");
        doc.Extensions().Elements(X + "File").Select(f => f.Attribute("path")!.Value)
            .Should().NotContain(ScriptPaths.UnattendComputerNameScriptPath);
    }

    [Fact]
    public void SetComputerName_WithEmptyName_FallsBackToTheDefaultPrefix()
    {
        AutounattendScripts.SetComputerName(null).Should().Contain($"$name = '{UniqueComputerName.DefaultPrefix}-' + $ending");
    }

    [Fact]
    public void Write_SpecializeShellSetup_KeepsChildrenInSchemaOrder()
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

    private static XElement Disk(XDocument doc) =>
        ComponentOrNull(doc, "windowsPE", "Microsoft-Windows-Setup")!.Element(U + "DiskConfiguration")!.Element(U + "Disk")!;

    // One row per CreatePartition, in Order: "<Type> <Size or Extend>".
    private static List<string> Creates(XDocument doc) =>
        Disk(doc).Element(U + "CreatePartitions")!.Elements(U + "CreatePartition")
            .Select(p => p.Element(U + "Type")!.Value + " " + (p.Element(U + "Size")?.Value ?? "extend"))
            .ToList();

    // One row per ModifyPartition: "<PartitionID> <Label> <Format>".
    private static List<string> Modifies(XDocument doc) =>
        Disk(doc).Element(U + "ModifyPartitions")!.Elements(U + "ModifyPartition")
            .Select(p => p.Element(U + "PartitionID")!.Value + " " + p.Element(U + "Label")!.Value + " " + p.Element(U + "Format")!.Value)
            .ToList();

    private static string InstalledPartition(XDocument doc) =>
        ComponentOrNull(doc, "windowsPE", "Microsoft-Windows-Setup")!
            .Element(U + "ImageInstall")!.Element(U + "OSImage")!.Element(U + "InstallTo")!.Element(U + "PartitionID")!.Value;

    private static SettingChoice[] Uefi(string recoverySize) =>
    [
        new("autounattend-disk", new ChoiceValue.Option(1)),
        new("autounattend-disk-recovery-size", new ChoiceValue.Text(recoverySize)),
    ];

    // The layout in Microsoft's own UEFI unattend sample, partition ids and all. Windows is the fourth
    // partition there because the MSR takes an id and no ModifyPartition, which is the part that is easy to
    // get wrong and impossible to see without installing.
    [Fact]
    public void Write_DiskUefi_LaysTheDiskOutLikeTheMicrosoftSample()
    {
        var doc = Write(new DiskConfigurationWriter(), Uefi("1000"));

        Creates(doc).Should().Equal("Primary 1000", "EFI 300", "MSR 16", "Primary extend");
        Modifies(doc).Should().Equal("1 Recovery NTFS", "2 System FAT32", "4 Windows NTFS");
        InstalledPartition(doc).Should().Be("4");

        var recovery = Disk(doc).Element(U + "ModifyPartitions")!.Elements(U + "ModifyPartition").First();
        recovery.Element(U + "TypeID")!.Value.Should().Be("de94bba4-06d1-4d40-a16a-bfd50179d6ac");
        Disk(doc).Element(U + "ModifyPartitions")!.Elements(U + "ModifyPartition").Last()
            .Element(U + "Letter")!.Value.Should().Be("C");
    }

    [Fact]
    public void Write_DiskUefiWithEmptyRecoverySize_LeavesThePartitionOutAndMovesWindowsUp()
    {
        var doc = Write(new DiskConfigurationWriter(), Uefi(string.Empty));

        Creates(doc).Should().Equal("EFI 300", "MSR 16", "Primary extend");
        Modifies(doc).Should().Equal("1 System FAT32", "3 Windows NTFS");
        InstalledPartition(doc).Should().Be("3");
    }

    [Fact]
    public void Write_DiskBios_MakesTheSystemPartitionPrimaryAndActiveWithNoMsr()
    {
        var doc = Write(
            new DiskConfigurationWriter(),
            new SettingChoice("autounattend-disk", new ChoiceValue.Option(2)),
            new SettingChoice("autounattend-disk-system-size", new ChoiceValue.Text("100")));

        Creates(doc).Should().Equal("Primary 1000", "Primary 100", "Primary extend");
        Modifies(doc).Should().Equal("1 Recovery NTFS", "2 System NTFS", "3 Windows NTFS");
        InstalledPartition(doc).Should().Be("3");

        var system = Disk(doc).Element(U + "ModifyPartitions")!.Elements(U + "ModifyPartition").ElementAt(1);
        system.Element(U + "Active")!.Value.Should().Be("true");
        Disk(doc).Element(U + "ModifyPartitions")!.Elements(U + "ModifyPartition").First()
            .Element(U + "TypeID")!.Value.Should().Be("0x27");
    }

    // "Do not set both CreatePartition:Size and CreatePartition:Extend... An error is logged, and installation
    // is terminated", and one of the two is required.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Write_DiskLayouts_EveryPartitionNamesASizeOrExtendsNeverBothAndNeverNeither(int layout)
    {
        var doc = Write(new DiskConfigurationWriter(), new SettingChoice("autounattend-disk", new ChoiceValue.Option(layout)));

        foreach (var partition in Disk(doc).Element(U + "CreatePartitions")!.Elements(U + "CreatePartition"))
            (partition.Element(U + "Size") is null).Should().NotBe(partition.Element(U + "Extend") is null);
    }

    [Fact]
    public void Write_DiskNumber_ReachesTheWipedDiskAndTheInstallTarget()
    {
        var doc = Write(
            new DiskConfigurationWriter(),
            new SettingChoice("autounattend-disk", new ChoiceValue.Option(1)),
            new SettingChoice("autounattend-disk-number", new ChoiceValue.Text("2")));

        Disk(doc).Element(U + "DiskID")!.Value.Should().Be("2");
        ComponentOrNull(doc, "windowsPE", "Microsoft-Windows-Setup")!
            .Element(U + "ImageInstall")!.Element(U + "OSImage")!.Element(U + "InstallTo")!
            .Element(U + "DiskID")!.Value.Should().Be("2");
    }

    private static List<string> OsImageChildren(XDocument doc) =>
        ComponentOrNull(doc, "windowsPE", "Microsoft-Windows-Setup")!
            .Element(U + "ImageInstall")!.Element(U + "OSImage")!.Elements().Select(e => e.Name.LocalName).ToList();

    [Fact]
    public void Build_WithCompactAndDiskLayout_PlacesCompactAfterInstallToInTheOsImage()
    {
        var doc = AutounattendDocumentBuilder.Build(ContextWith(
            new SettingChoice("autounattend-disk", new ChoiceValue.Option(1)),
            new SettingChoice("autounattend-compact", new ChoiceValue.Toggle(true))));

        OsImageChildren(doc).Should().Equal("InstallTo", "Compact");
        ComponentOrNull(doc, "windowsPE", "Microsoft-Windows-Setup")!
            .Element(U + "ImageInstall")!.Element(U + "OSImage")!.Element(U + "Compact")!.Value.Should().Be("true");
    }

    [Fact]
    public void Build_WithCompactAndNoDiskLayout_CreatesTheOsImage()
    {
        var doc = AutounattendDocumentBuilder.Build(ContextWith(
            new SettingChoice("autounattend-compact", new ChoiceValue.Toggle(true))));

        OsImageChildren(doc).Should().Equal("Compact");
        ComponentOrNull(doc, "windowsPE", "Microsoft-Windows-Setup")!
            .Elements().Select(e => e.Name.LocalName).Should().Contain("ImageInstall");
    }

    [Fact]
    public void Build_WithCompactOff_WritesNoImageInstall()
    {
        var doc = AutounattendDocumentBuilder.Build(ContextWith(
            new SettingChoice("autounattend-compact", new ChoiceValue.Toggle(false))));

        doc.Descendants(U + "Compact").Should().BeEmpty();
        doc.Descendants(U + "ImageInstall").Should().BeEmpty();
    }

    // Every sample writes DiskConfiguration before ImageInstall, Disk's own ids before its partition lists, and
    // WillShowUI after the last Disk.
    [Fact]
    public void Write_DiskConfiguration_KeepsWindowsPeSetupChildrenInSchemaOrder()
    {
        var doc = Write(new DiskConfigurationWriter(), Uefi("1000"));

        ComponentOrNull(doc, "windowsPE", "Microsoft-Windows-Setup")!
            .Elements().Select(e => e.Name.LocalName).Should().Equal("DiskConfiguration", "ImageInstall");
        Disk(doc).Elements().Select(e => e.Name.LocalName)
            .Should().Equal("DiskID", "WillWipeDisk", "CreatePartitions", "ModifyPartitions");
        ComponentOrNull(doc, "windowsPE", "Microsoft-Windows-Setup")!.Element(U + "DiskConfiguration")!
            .Elements().Select(e => e.Name.LocalName).Should().Equal("Disk", "WillShowUI");
    }

    [Fact]
    public void Write_DiskAskDuringSetup_WritesNoDiskConfigurationAtAll()
    {
        var doc = Write(new DiskConfigurationWriter(), new SettingChoice("autounattend-disk", new ChoiceValue.Option(0)));

        ComponentOrNull(doc, "windowsPE", "Microsoft-Windows-Setup").Should().BeNull();
    }

    [Fact]
    public void Write_AccountMicrosoft_WritesNothing()
    {
        var doc = Write(new AccountWriter(), new SettingChoice("autounattend-account", new ChoiceValue.Option(1)));

        ComponentOrNull(doc, "specialize", "Microsoft-Windows-Deployment").Should().BeNull();
        ComponentOrNull(doc, "oobeSystem", "Microsoft-Windows-Shell-Setup").Should().BeNull();
    }

    private static void AdaptersAreCut(XDocument doc)
    {
        var commands = RunSynchronous(doc, "specialize", "Microsoft-Windows-Deployment");
        commands.Should().ContainSingle();
        Path(commands[0]).Should().Contain("Disable-NetAdapter");

        var firstLogon = ComponentOrNull(doc, "oobeSystem", "Microsoft-Windows-Shell-Setup")!
            .Element(U + "FirstLogonCommands")!.Elements(U + "SynchronousCommand").ToList();
        firstLogon.Should().ContainSingle();
        firstLogon[0].Element(U + "CommandLine")!.Value.Should().Contain("Enable-NetAdapter");
    }

    private static void SetupStaysOnline(XDocument doc)
    {
        ComponentOrNull(doc, "specialize", "Microsoft-Windows-Deployment").Should().BeNull();
        ComponentOrNull(doc, "oobeSystem", "Microsoft-Windows-Shell-Setup").Should().BeNull();
    }

    [Fact]
    public void Write_LocalAccount_CutsTheAdaptersWhateverTheNetworkCardSays()
    {
        AdaptersAreCut(Write(
            new OobeNetworkWriter(),
            new SettingChoice("autounattend-account", new ChoiceValue.Option(0)),
            new SettingChoice("autounattend-oobe-network", new ChoiceValue.Option(0))));
    }

    [Fact]
    public void Write_MicrosoftAccount_LeavesSetupOnlineWhateverTheNetworkCardSays()
    {
        SetupStaysOnline(Write(
            new OobeNetworkWriter(),
            new SettingChoice("autounattend-account", new ChoiceValue.Option(1)),
            new SettingChoice("autounattend-oobe-network", new ChoiceValue.Option(1))));
    }

    [Fact]
    public void Write_AccountCreateNow_TakesTheNetworkCardsAnswer()
    {
        SetupStaysOnline(Write(
            new OobeNetworkWriter(),
            new SettingChoice("autounattend-account", new ChoiceValue.Option(2)),
            new SettingChoice("autounattend-oobe-network", new ChoiceValue.Option(0))));

        AdaptersAreCut(Write(
            new OobeNetworkWriter(),
            new SettingChoice("autounattend-account", new ChoiceValue.Option(2)),
            new SettingChoice("autounattend-oobe-network", new ChoiceValue.Option(1))));
    }

    // Both cards fall back to their seed state, so the file a user never opened cuts the adapters.
    [Fact]
    public void Write_NeitherAccountNorNetworkCard_CutsTheAdapters()
    {
        AdaptersAreCut(Write(new OobeNetworkWriter()));
        AdaptersAreCut(Write(new OobeNetworkWriter(), new SettingChoice("autounattend-account", new ChoiceValue.Option(2))));
    }

    // A wrong LogicalName leaves the resource unreachable, and the only symptom is a wallpaper that never
    // applies on a PC nobody can attach a debugger to.
    [Fact]
    public void DesktopBackgroundInterop_Always_TravelsAsTheSourceAddTypeCompiles()
    {
        var source = AutounattendScripts.DesktopBackgroundInterop;

        source.Should().Contain("namespace Winhance.Desktop")
            .And.Contain("public static class DesktopBackground")
            .And.Contain("B92B56A9-8B55-4E14-9A89-0199BBB6F93B");
        // The here-string that carries it ends at a line of its own, and C# 5 has no file-scoped namespace.
        source.Should().NotContain("\n'@").And.NotContain("namespace Winhance.Desktop;");
    }

    [Fact]
    public void ExtractScript_Always_DecodesBothCarriedShapes()
    {
        AutounattendScripts.ExtractScript.Should().StartWith("param(");
        AutounattendScripts.ExtractScript.Should().Contain("$Document.unattend.Extensions.File");
        AutounattendScripts.ExtractScript.Should().Contain("WriteAllBytes");
        AutounattendScripts.ExtractScript.Should().Contain("FromBase64String");
        AutounattendScripts.ExtractCommand.Should().Contain("C:\\Windows\\Panther\\unattend.xml").And.Contain("Extensions.ExtractScript");
    }

    // XML parsing hands the extractor LF line endings, and cmd.exe misses labels in a batch file that has them.
    [Fact]
    public void ExtractScript_Always_WritesTextFilesWithCrlfLineEndings()
    {
        AutounattendScripts.ExtractScript.Should().Contain("$text = $file.InnerText.Trim() -replace '\\r?\\n', \"`r`n\";");
        AutounattendScripts.ExtractScript.Should().Contain("$encoding.GetBytes( $text )");
    }

    [Fact]
    public void Build_Carried_RunsTheExtractorFirstAndTheWinhancementsScriptLastInSpecialize()
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
    public void Build_EmbeddedPicture_RidesBesideTheScriptAsBase64()
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
    public void Build_PictureTheFileOnlyNames_CarriesNoPictureElement()
    {
        var doc = AutounattendDocumentBuilder.Build(new AutounattendRenderContext("Write-Host 'carried'", "0.0.0", SetWith(
            new SettingChoice("theme-wallpaper-picture",
                new ChoiceValue.Keyed(@"C:\Windows\Web\Wallpaper\Windows\img0.jpg", "Windows 11 light")))));

        doc.Extensions().Elements(X + "File").Should().ContainSingle()
            .Which.Attribute("path")!.Value.Should().Be(ScriptPaths.AutounattendScriptPath);
    }

    [Fact]
    public void Build_Carried_StampsItsVersionFirstUnderExtensions()
    {
        var doc = AutounattendDocumentBuilder.Build(Carried);

        var first = doc.Extensions().Elements().First();
        first.Name.Should().Be(X + "Generator");
        first.Attribute("version")!.Value.Should().Be("26.09.04");
    }

    [Fact]
    public void Write_SingleArchitecture_RewritesThePlaceholder()
    {
        var doc = AutounattendDocument.NewSkeleton();
        doc.Pass("specialize").Component("Microsoft-Windows-Deployment").AddRunSynchronousCommand("cmd.exe /c one");
        doc.Pass("oobeSystem").Component("Microsoft-Windows-Shell-Setup").ElementPath("OOBE/HideEULAPage").Value = "true";

        new ArchitectureWriter().Write(doc, ContextWith(
            new SettingChoice("autounattend-architecture-arm64", new ChoiceValue.CheckBox(false)),
            new SettingChoice("autounattend-architecture-x64", new ChoiceValue.CheckBox(false))));

        doc.Descendants(U + "component").Should().HaveCount(2);
        doc.Descendants(U + "component").Should().OnlyContain(c => (string?)c.Attribute("processorArchitecture") == "x86");
    }

    [Fact]
    public void Write_ThreeArchitectures_CloneEveryComponentInTheFilesOrder()
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
    public void Write_TwoArchitectures_CloneOnceAndKeepTheFilesOrder()
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
    public void Write_NoArchitectureChecked_StillCoversAllThree()
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
    public void Build_Defaults_LaysSpecializeOutLikeTheTemplateDid()
    {
        var doc = AutounattendDocumentBuilder.Build(Carried);

        foreach (var pass in new[] { "windowsPE", "specialize", "oobeSystem" })
            doc.Pass(pass).Elements(U + "component").Select(c => (string?)c.Attribute("processorArchitecture")).Should().Equal("x86", "arm64", "amd64");
        doc.Root!.Elements(U + "settings").Select(s => (string?)s.Attribute("pass")).Should().Equal("windowsPE", "specialize", "oobeSystem");

        var specialize = doc.Pass("specialize").Elements(U + "component").First().Element(U + "RunSynchronous")!.Elements(U + "RunSynchronousCommand").ToList();
        specialize.Select(c => (string?)c.Element(U + "Order")).Should().Equal("1", "2", "3", "4", "5");
        specialize.Select(c => c.Element(U + "Description")!.Value).Should().SatisfyRespectively(
            d => d.Should().Be("Loads Scripts in this XML File"),
            d => d.Should().Be("Enables .NET Framework 3.5 from Windows Installation Media"),
            d => d.Should().Be("Registry Entry to Create a Local Account during OOBE"),
            // The cut sits behind the cards the generic writer emits because OobeNetworkWriter runs after it;
            // it only has to land before OOBE, and nothing above it needs the network.
            d => d.Should().StartWith("Disable All Network Adapters Temporarily"),
            d => d.Should().StartWith("Runs Winhance Script System Wide Operations"));
        doc.Pass("windowsPE").Elements(U + "component").First().Element(U + "RunSynchronous")!.Elements(U + "RunSynchronousCommand").Should().HaveCount(6);
        doc.Extensions().Elements().Select(e => e.Name.LocalName).Should().Equal("Generator", "ExtractScript", "File");
        doc.Root!.Elements().Last().Name.Should().Be(X + "Extensions");
    }

    [Fact]
    public void Build_LocalesAndTimeZone_ReachTheAssembledDocument()
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
    public void Serialize_Always_WritesUtf8WithoutBomAndKeepsTheCdata()
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
