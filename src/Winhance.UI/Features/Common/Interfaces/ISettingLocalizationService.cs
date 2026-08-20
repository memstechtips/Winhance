using Winhance.Core.Features.Common.Catalog;

namespace Winhance.UI.Features.Common.Interfaces;

public interface ISettingLocalizationService
{
    string? BuildCrossGroupInfoMessage(Setting setting);
}
