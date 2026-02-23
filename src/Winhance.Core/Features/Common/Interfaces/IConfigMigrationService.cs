using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IConfigMigrationService
{
    void MigrateConfig(UnifiedConfigurationFile config);
}
