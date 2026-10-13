namespace Winhance.Core.Features.Common.Constants;

public static class ScriptPaths
{
    public static readonly string ScriptsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Winhance", "Scripts");

    public const string ScriptsDirectoryLiteral = @"C:\ProgramData\Winhance\Scripts";

    public const string LogsDirectoryLiteral = @"C:\ProgramData\Winhance\Logs";

    public const string AutounattendScriptPath = @"C:\ProgramData\Winhance\Unattend\Scripts\Winhancements.ps1";

    // WIMUtil stages these under the media's sources\$OEM$\$1, which Setup copies to the same paths on C:.
    public const string UnattendAppsDirectory = @"C:\ProgramData\Winhance\Unattend\Apps";

    public const string UnattendOwnInstallersDirectory = @"C:\ProgramData\Winhance\Unattend\Apps\Custom";

    public const string UnattendUserScriptsDirectory = @"C:\ProgramData\Winhance\Unattend\Scripts\Custom";

    // The answer file sets KeepStagedInstallers=1 here when the user chose to keep the installers after they run.
    public const string UnattendSettingsKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Winhance";

    public const string UnattendWinhanceInstallerPath = @"C:\ProgramData\Winhance\Unattend\Winhance.Installer.exe";
}