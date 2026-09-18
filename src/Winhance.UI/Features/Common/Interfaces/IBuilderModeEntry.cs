using Winhance.Core.Features.Common.Enums;

namespace Winhance.UI.Features.Common.Interfaces;

public interface IBuilderModeEntry
{
    Task<bool> EnterAsync(BuilderTarget target);
}
