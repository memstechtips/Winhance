namespace Winhance.Core.Features.Common.Enums;

/// <summary>
/// The app-wide interaction mode. Determines whether UI changes apply to the
/// live system (Normal), author a file without applying (Builder), or review a
/// loaded config before applying (ConfigReview).
/// </summary>
public enum WinhanceMode
{
    Normal,
    Builder,
    ConfigReview,
}
