namespace Winhance.Core.Features.Common.Enums;

/// <summary>
/// What a Builder-mode session is authoring. Only meaningful when
/// <see cref="WinhanceMode.Builder"/> is active.
/// </summary>
public enum BuilderTarget
{
    Config,
    Autounattend,
}
