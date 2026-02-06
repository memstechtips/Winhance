using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.UI.Features.SoftwareApps.Views;

public sealed partial class ExternalAppsHelpContent : UserControl
{
    public ExternalAppsHelpContent(ILocalizationService localizationService)
    {
        this.InitializeComponent();
        HelpContentText.Text = localizationService.GetString("Help_ExternalApps_Content");
    }
}
