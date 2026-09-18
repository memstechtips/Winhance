using System.Xml.Linq;
using Winhance.Core.Features.Autounattend;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Selections;
using Winhance.Core.Features.Common.Localization;

namespace Winhance.Infrastructure.Features.Autounattend.Writers;

// The rows are generic List values: the field keys read here are the contract with the catalog's Fields.
internal sealed class AccountWriter : IAutounattendElementWriter
{
    private static readonly XNamespace U = AutounattendDocument.Unattend;

    public IReadOnlyCollection<string> Handles => ["autounattend-accounts"];

    public void Write(XDocument doc, AutounattendRenderContext context)
    {
        if (context.StateOf("autounattend-account")?.Label == LocKey.Setting.AutounattendAccount.Option2)
            WriteCreateNow(doc, context.Value("autounattend-accounts") as ChoiceValue.List);
    }

    private static void WriteCreateNow(XDocument doc, ChoiceValue.List? accounts)
    {
        // A file that hides the online screens and creates no account cannot finish OOBE offline, so an empty
        // list gets the local account state.
        var mode = SettingCatalog.ById["autounattend-account"];
        if (accounts is null || accounts.Rows.Count == 0)
        {
            AutounattendElementWriter.WriteState(doc, mode, mode.States.Single(s => s.Label == LocKey.Setting.AutounattendAccount.Option0));
            return;
        }

        var shellSetup = doc.Pass("oobeSystem").Component("Microsoft-Windows-Shell-Setup");

        var localAccounts = shellSetup.Child("UserAccounts").Child("LocalAccounts");
        foreach (var account in accounts.Rows)
        {
            localAccounts.Add(new XElement(U + "LocalAccount",
                new XAttribute(AutounattendDocument.Wcm + "action", "add"),
                new XElement(U + "Name", Field(account, "name")),
                new XElement(U + "DisplayName", Field(account, "display-name")),
                new XElement(U + "Group", GroupName(account)),
                Password(account)));
        }

        if (accounts.Rows.FirstOrDefault(row => Field(row, "auto-logon") == "true") is { } signsIn)
        {
            shellSetup.Child("AutoLogon").Add(
                Password(signsIn),
                new XElement(U + "Enabled", "true"),
                new XElement(U + "LogonCount", "1"),
                new XElement(U + "Username", Field(signsIn, "name")));
        }

        if (accounts.Rows.Any(row => Field(row, "password").Length > 0)
            && SettingCatalog.ById["autounattend-accounts"].List!.PasswordCleanup is { } cleanup)
        {
            shellSetup.AddFirstLogonCommand(cleanup.Command, cleanup.Description);
        }
    }

    // An empty password cannot be obscured: base64 of the bare suffix would BE the password.
    private static XElement Password(ChoiceValue.ListRow account)
    {
        var password = Field(account, "password");
        var plainText = Field(account, "obscure") != "true" || password.Length == 0;
        return new XElement(U + "Password",
            new XElement(U + "Value", plainText ? password : AccountPasswords.Obscure(password, "Password")),
            new XElement(U + "PlainText", plainText ? "true" : "false"));
    }

    // A Selection field holds the option index the row is on.
    private static string GroupName(ChoiceValue.ListRow account) => Field(account, "group") == "1" ? "Users" : "Administrators";

    private static string Field(ChoiceValue.ListRow account, string key) =>
        account.Values.TryGetValue(key, out var value) ? value : string.Empty;
}
