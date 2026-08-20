using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

public class ResourceService : IResourceService
{
    public string GetResourceIconPath(string resourceKey)
    {
        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(resourceKey, out var value) && value is string path)
            return path;
        return string.Empty;
    }
}
