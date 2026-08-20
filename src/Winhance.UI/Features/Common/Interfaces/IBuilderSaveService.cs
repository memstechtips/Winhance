using Winhance.Core.Features.Common.Enums;

namespace Winhance.UI.Features.Common.Interfaces;

public interface IBuilderSaveService
{
    Task SaveAsync(BuilderTarget target);
}
