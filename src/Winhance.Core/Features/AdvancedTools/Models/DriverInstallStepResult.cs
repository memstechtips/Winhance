namespace Winhance.Core.Features.AdvancedTools.Models;

// Added and CreatedXml at media-creation time mean the earlier steps missed the ensure call -
// step 4's verification logs those as self-healed gaps.
public enum DriverInstallStepResult
{
    NoDriversStaged,
    AlreadyPresent,
    Added,
    CreatedXml
}
