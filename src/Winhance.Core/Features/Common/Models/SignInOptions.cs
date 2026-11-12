namespace Winhance.Core.Features.Common.Models;

// What this user's "Winhance at sign-in" task is set to do.
public sealed record SignInOptions(bool StartInTray, bool Reapply, bool Maintain)
{
    public static SignInOptions None { get; } = new(false, false, false);
}
