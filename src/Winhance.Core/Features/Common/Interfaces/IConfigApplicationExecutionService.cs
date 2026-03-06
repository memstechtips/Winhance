using System.Collections.Generic;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IConfigApplicationExecutionService
{
    Task ExecuteConfigImportAsync(UnifiedConfigurationFile config, ImportOptions options);
    Task ApplyConfigurationWithOptionsAsync(
        UnifiedConfigurationFile config,
        List<string> selectedSections,
        ImportOptions options);
}
