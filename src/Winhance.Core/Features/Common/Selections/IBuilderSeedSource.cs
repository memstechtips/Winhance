using Winhance.Core.Features.Common.Catalog;

namespace Winhance.Core.Features.Common.Selections;

public enum BuilderSeed
{
    CurrentMachine,
    Recommended,
    WindowsDefaults,
}

// The choices a Builder session starts from when the user picks a seed other than this machine's state: one
// SettingChoice per catalog setting in scope that carries a value for that role. Empty for CurrentMachine.
public interface IBuilderSeedSource
{
    Task<IReadOnlyList<SettingChoice>> ChoicesForAsync(BuilderSeed seed, CatalogScope scope);
}
