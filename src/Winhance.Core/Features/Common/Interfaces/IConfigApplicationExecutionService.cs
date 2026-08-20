using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IConfigApplicationExecutionService
{
    Task ExecuteConfigImportAsync(WinhanceConfigFile config, ImportOptions options);
    Task ApplyConfigurationWithOptionsAsync(
        WinhanceConfigFile config,
        List<string> selectedSections,
        ImportOptions options);
}
