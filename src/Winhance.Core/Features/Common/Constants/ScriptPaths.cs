using Winhance.Core.Features.Common.Helpers;

namespace Winhance.Core.Features.Common.Constants;

public static class ScriptPaths
{
    public static readonly string ScriptsDirectory = Path.Combine(AppDataPaths.InstalledMachineRoot, "Scripts");

    public const string ScriptsDirectoryLiteral = @"C:\ProgramData\Winhance\Scripts";

    public const string TaskFolder = @"\Winhance";

    // Edge removal always leaves this folder and its AtLogon task behind; reinstalling Edge deletes both.
    public const string OpenWebSearchTaskName = "OpenWebSearchRepair";

    public const string OpenWebSearchDirectoryLiteral = @"C:\ProgramData\Winhance\OpenWebSearch";

    public const string LogsDirectoryLiteral = @"C:\ProgramData\Winhance\Logs";

    public const string AutounattendScriptPath = @"C:\ProgramData\Winhance\Unattend\Scripts\Winhancements.ps1";

    // WIMUtil stages these under the media's sources\$OEM$\$1, which Setup copies to the same paths on C:.
    public const string UnattendAppsDirectory = @"C:\ProgramData\Winhance\Unattend\Apps";

    public const string UnattendUpdatesDirectory = @"C:\ProgramData\Winhance\Unattend\Updates";

    // Written into the image when the offline build removed the chosen apps; setup then skips the removal run.
    public const string UnattendAppsRemovedMarker = @"C:\ProgramData\Winhance\Unattend\AppsRemovedInImage";

    // DISM finds a target update's checkpoints in the folder it sits in, so the folder holds that update's .msu files and nothing else.
    public const string UnattendUpdatesMediaDirectory = @"sources\$OEM$\$1\ProgramData\Winhance\Unattend\Updates";

    public const string UnattendOwnInstallersDirectory = @"C:\ProgramData\Winhance\Unattend\Apps\Custom";

    public const string UnattendUserScriptsDirectory = @"C:\ProgramData\Winhance\Unattend\Scripts\Custom";

    public const string UnattendComputerNameScriptPath = @"C:\ProgramData\Winhance\Unattend\Scripts\SetComputerName.ps1";

    // The answer file sets KeepStagedInstallers=1 here when the user chose to keep the installers after they run.
    public const string UnattendSettingsKey = @"HKEY_LOCAL_MACHINE\SOFTWARE\Winhance";

    public const string UnattendWinhanceInstallerPath = @"C:\ProgramData\Winhance\Unattend\Winhance.Installer.exe";
}