namespace Winhance.UI.Features.Common.Interfaces;

/// <summary>
/// Abstracts access to XAML application resources for testability.
/// </summary>
public interface IResourceService
{
    /// <summary>
    /// Gets a string resource path by key from Application.Current.Resources.
    /// Returns empty string if the key is not found or the value is not a string.
    /// </summary>
    string GetResourceIconPath(string resourceKey);
}
