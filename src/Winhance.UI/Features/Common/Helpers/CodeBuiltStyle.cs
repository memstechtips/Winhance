using Microsoft.UI.Xaml;

namespace Winhance.UI.Features.Common.Helpers;

// UI built in C# asks for a Style, never a Brush: a brush fetched in code resolves against the application theme,
// which follows Windows, while a ThemeResource inside a Style setter follows the Winhance theme and a theme switch.
internal static class CodeBuiltStyle
{
    public static Style Named(string key) => (Style)Application.Current.Resources[key];
}
