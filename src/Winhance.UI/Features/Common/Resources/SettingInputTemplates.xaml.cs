using Microsoft.UI.Xaml;
using Winhance.Core.Features.Common.Catalog;
using Winhance.UI.Features.Common.Controls;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Resources;

// Code-behind so x:Bind and the delete handler work inside the templates.
public sealed partial class SettingInputTemplates : ResourceDictionary
{
    public SettingInputTemplates()
    {
        this.InitializeComponent();
    }

    private void OnOptionDeleteRequested(object? sender, DynamicOption option)
    {
        if (sender is SettingComboBox { Setting.ParentFeatureViewModel: PowerOptimizationsViewModel power })
            power.DeletePowerPlanCommand.Execute(option);
    }
}
