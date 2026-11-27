using System.Reflection;
using Winhance.Core.Features.Autounattend;

namespace Winhance.Infrastructure.Features.Autounattend;

internal static class AutounattendScripts
{
    public const string ExtractDescription = "Loads Scripts in this XML File";

    // Windows Setup caches the answer file at C:\Windows\Panther\unattend.xml before specialize runs.
    public const string ExtractCommand =
        "powershell.exe -NoProfile -WindowStyle Hidden -Command \"$xml = [xml]::new(); $xml.Load('C:\\Windows\\Panther\\unattend.xml'); $sb = [scriptblock]::Create( $xml.unattend.Extensions.ExtractScript ); Invoke-Command -ScriptBlock $sb -ArgumentList $xml;\"";

    private const string ExtractScriptResource = "Winhance.Infrastructure.Resources.Autounattend.ExtractScript.ps1";

    private const string SetComputerNameResource = "Winhance.Infrastructure.Resources.Autounattend.SetComputerName.ps1";

    private const string DesktopBackgroundResource = "Winhance.Infrastructure.Resources.Native.DesktopBackgroundInterop.cs";

    private static readonly Lazy<string> ExtractScriptBody = new(() => Read(ExtractScriptResource));

    private static readonly Lazy<string> SetComputerNameBody = new(() => Read(SetComputerNameResource));

    // The C# the app compiles for IDesktopWallpaper, shipped as text so Add-Type compiles the same source
    // on the new PC. One implementation, two compilers.
    private static readonly Lazy<string> DesktopBackgroundBody = new(() => Read(DesktopBackgroundResource));

    public static string ExtractScript => ExtractScriptBody.Value;

    public static string DesktopBackgroundInterop => DesktopBackgroundBody.Value;

    // The prefix keeps only letters, digits and dashes, so it sits safely inside the script's quotes.
    public static string SetComputerName(string? typedName) => SetComputerNameBody.Value
        .Replace("{{prefix}}", UniqueComputerName.Prefix(typedName), StringComparison.Ordinal)
        .Replace("{{placeholder-serials}}", string.Join(", ", UniqueComputerName.PlaceholderSerials.Select(s => $"'{s}'")), StringComparison.Ordinal);

    private static string Read(string resource)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
            ?? throw new FileNotFoundException($"Embedded resource not found: {resource}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
