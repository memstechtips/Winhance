using Microsoft.UI.Xaml;

namespace Winhance.UI.Features.Common.Resources;

// Code-behind so x:Bind works inside DataTemplates (the WinUI Gallery pattern).
public sealed partial class SettingTemplates : ResourceDictionary
{
    public SettingTemplates()
    {
        this.InitializeComponent();
    }
}
