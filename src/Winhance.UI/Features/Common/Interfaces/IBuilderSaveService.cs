using Winhance.Core.Features.Common.Enums;

namespace Winhance.UI.Features.Common.Interfaces;

public interface IBuilderSaveService
{
    Task SaveAsync(BuilderTarget target);

    // Asks for a name first; the profile Builder was opened from may be overwritten, any other may not.
    Task SaveAsProfileAsync();
}
