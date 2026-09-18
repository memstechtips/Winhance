using System.Globalization;
using System.Xml.Linq;
using Winhance.Infrastructure.Features.Customize.Services;

namespace Winhance.Infrastructure.Features.Autounattend.Writers;

// Setup takes InputLocale as a pair, so no single setting can name it. Setup's first screen asks for the language
// and keyboard, so no windowsPE component is written.
internal sealed class TimeRegionLanguageWriter : IAutounattendElementWriter
{
    public IReadOnlyCollection<string> Handles => ["region-keyboard-layout"];

    public void Write(XDocument doc, AutounattendRenderContext context)
    {
        var inputLocale = InputLocale(context.Key("region-format"), context.Key("region-keyboard-layout"));
        if (inputLocale is null)
            return;

        doc.Pass("oobeSystem").Component("Microsoft-Windows-International-Core")
            .Child("InputLocale").Value = inputLocale;
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
