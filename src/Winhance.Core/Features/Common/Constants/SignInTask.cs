namespace Winhance.Core.Features.Common.Constants;

public static class SignInTask
{
    public const string Name = "Winhance at sign-in";

    public const string TrayFlag = "--tray";
    public const string ReapplyFlag = "--reapply-profile";
    public const string MaintainFlag = "--maintain";

    // What a refused registration reports when other accounts can change Winhance's exe or its folder.
    public const string UnsafeFolderError = "Other accounts can change Winhance's files in this folder";

    // Tasks earlier versions registered; they are folded into the one sign-in task and deleted.
    public const string LegacyTrayName = "Start Winhance in the tray";
    public const string LegacyReapplyName = "Re-apply the Winhance profile";
    public const string LegacyMaintainName = "Keep removed apps";
}
