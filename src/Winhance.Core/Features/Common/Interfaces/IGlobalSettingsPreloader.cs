using System.Threading.Tasks;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IGlobalSettingsPreloader
{
    Task PreloadAllSettingsAsync();
    bool IsPreloaded { get; }
}
