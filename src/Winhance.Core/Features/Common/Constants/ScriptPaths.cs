namespace Winhance.Core.Features.Common.Constants;

public static class ScriptPaths
{
    public static readonly string ScriptsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Winhance", "Scripts");

    public const string ScriptsDirectoryLiteral = @"C:\ProgramData\Winhance\Scripts";

    public const string LogsDirectoryLiteral = @"C:\ProgramData\Winhance\Logs";

    public const string UnattendScriptPath = @"C:\ProgramData\Winhance\Unattend\Scripts\Winhancements.ps1";

    public const string PowerShellExePath = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe";
}