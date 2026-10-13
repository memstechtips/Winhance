using System.Globalization;
using System.Xml.Linq;
using Winhance.Infrastructure.Features.Customize.Services;

namespace Winhance.Infrastructure.Features.Autounattend.Writers;

// Setup takes InputLocale as a pair, so no single setting can name it.
internal sealed class TimeRegionLanguageWriter : IAutounattendElementWriter
{
    public IReadOnlyCollection<string> Handles => ["region-keyboard-layout"];

    public void Write(XDocument doc, AutounattendRenderContext context)
    {
        var inputLocale = InputLocale(context.Key("region-format"), context.Key("region-keyboard-layout"));
        if (inputLocale is not null)
        {
            doc.Pass("oobeSystem").Component("Microsoft-Windows-International-Core")
                .Child("InputLocale").Value = inputLocale;
        }

        WriteSetupScreen(doc, context, inputLocale);
    }

    // Setup's first screen asks for the language, the regional format and the keyboard, and skips itself only when
    // windowsPE answers all of them. The setup-language card has already put its own two elements there through the
    // generic writer; the other three are the same choices the installed Windows gets, so they come from the region
    // cards. With no setup language chosen the pass stays empty and Setup asks.
    private static void WriteSetupScreen(XDocument doc, AutounattendRenderContext context, string? inputLocale)
    {
        if (context.Key("autounattend-setup-language") is null)
            return;

        var setup = doc.Pass("windowsPE").Component("Microsoft-Windows-International-Core-WinPE");

        if (inputLocale is not null)
            setup.Child("InputLocale").Value = inputLocale;

        if (context.Key("region-system-locale") is { } systemLocale)
            setup.Child("SystemLocale").Value = systemLocale;

        if (context.Key("region-format") is { } userLocale)
            setup.Child("UserLocale").Value = userLocale;
    }

    // Microsoft documents the pair as the regional format's LCID in four hex digits, a colon, and the layout id.
    private static string? InputLocale(string? formatCulture, string? klid)
    {
        if (formatCulture is null || klid is null)
            return null;

        try
        {
            var lcid = CultureInfo.GetCultureInfo(formatCulture).LCID;
            // A culture this Windows does not know throws or reports NoLcid, depending on the globalization backend.
            return lcid == TimeRegionLanguageService.NoLcid
                ? null
                : lcid.ToString("X4", CultureInfo.InvariantCulture) + ":" + klid;
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}
