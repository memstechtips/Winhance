using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IOptionProviderRegistry
{
    IOptionProvider For(OptionSource source);
}
