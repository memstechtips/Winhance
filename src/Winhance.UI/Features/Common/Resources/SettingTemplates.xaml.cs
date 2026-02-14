using Microsoft.UI.Xaml;

namespace Winhance.UI.Features.Common.Resources;

/// <summary>
/// ResourceDictionary with code-behind to enable x:Bind in DataTemplates.
/// This pattern is used by WinUI Gallery and is officially supported by Microsoft.
/// </summary>
public sealed partial class SettingTemplates : ResourceDictionary
{
    public SettingTemplates()
    {
        this.InitializeComponent();
    }
}
