namespace Winhance.Core.Features.Common.Enums;

public enum RunContext
{
    System,
    User,
    // SYSTEM, from the UserCustomizations task's $alreadyApplied branch: every sign-in from the one after Setup's
    // restart onward, so the script decides for itself whether it has already run. Answer file only.
    SignInAfterSetup,
}
