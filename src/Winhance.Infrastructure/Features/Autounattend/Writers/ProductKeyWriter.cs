using System.Xml.Linq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Localization;

namespace Winhance.Infrastructure.Features.Autounattend.Writers;

internal sealed class ProductKeyWriter : IAutounattendElementWriter
{
    public IReadOnlyCollection<string> Handles => ["autounattend-edition", "autounattend-product-key"];

    public void Write(XDocument doc, AutounattendRenderContext context)
    {
        var edition = context.StateOf("autounattend-edition")
            ?? throw new InvalidOperationException("The edition setting is in the catalog, so a state always resolves.");

        var userData = doc.Pass("windowsPE").Component("Microsoft-Windows-Setup").Child("UserData");
        var productKey = userData.Child("ProductKey");

        if (edition.Label == LocKey.Setting.AutounattendEdition.Option14)
            WriteOwnKey(doc, productKey, context.Text("autounattend-product-key"));
        else
            WriteCatalogKey(productKey, edition);

        userData.SetSetting("AcceptEula", "true");
    }

    private static void WriteOwnKey(XDocument doc, XElement productKey, string? typed)
    {
        // An own-key choice with nothing typed yet is "ask me during setup"; Setup rejects an empty Key outright.
        if (string.IsNullOrWhiteSpace(typed))
        {
            productKey.SetSetting("Key", "00000-00000-00000-00000-00000");
            productKey.SetSetting("WillShowUI", "Always");
            return;
        }

        productKey.SetSetting("Key", typed);
        productKey.SetSetting("WillShowUI", "OnError");
        doc.Pass("specialize").Component("Microsoft-Windows-Shell-Setup").Child("ProductKey").Value = typed;
    }

    // The firmware edition carries no key, so an Absent payload writes no Key element rather than an empty one.
    private static void WriteCatalogKey(XElement productKey, SettingState edition)
    {
        if (edition.Set.TryGetValue("key", out var key) && key.WritePayload is string keyText)
            productKey.SetSetting("Key", keyText);
        if (edition.Set.TryGetValue("show", out var show) && show.WritePayload is string showText)
            productKey.SetSetting("WillShowUI", showText);
    }
}
