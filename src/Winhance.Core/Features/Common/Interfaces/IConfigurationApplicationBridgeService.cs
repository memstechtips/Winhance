using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IConfigurationApplicationBridgeService
{
    Task<bool> ApplyConfigurationSectionAsync(
        ConfigSection section,
        string sectionName,
        Func<string, object?, Task<(bool confirmed, bool checkboxResult)>>? confirmationHandler = null);
}
