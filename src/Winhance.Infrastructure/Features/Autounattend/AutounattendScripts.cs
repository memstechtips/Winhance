using System.Reflection;

namespace Winhance.Infrastructure.Features.Autounattend;

internal static class AutounattendScripts
{
    public const string ExtractDescription = "Loads Scripts in this XML File";

    // Windows Setup caches the answer file at C:\Windows\Panther\unattend.xml before specialize runs.
    public const string ExtractCommand =
        "powershell.exe -NoProfile -WindowStyle Hidden -Command \"$xml = [xml]::new(); $xml.Load('C:\\Windows\\Panther\\unattend.xml'); $sb = [scriptblock]::Create( $xml.unattend.Extensions.ExtractScript ); Invoke-Command -ScriptBlock $sb -ArgumentList $xml;\"";

    private const string ExtractScriptResource = "Winhance.Infrastructure.Resources.Autounattend.ExtractScript.ps1";

    private static readonly Lazy<string> ExtractScriptBody = new(() =>
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ExtractScriptResource)
            ?? throw new FileNotFoundException($"Embedded resource not found: {ExtractScriptResource}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    public static string ExtractScript => ExtractScriptBody.Value;
}
