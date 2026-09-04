using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Services;
using Winhance.UI.Features.Common.Controls;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Customize.ViewModels;

namespace Winhance.UI.Features.Customize;

public sealed partial class CustomizePage : SectionPage
{
    public CustomizeViewModel ViewModel { get; }

    protected override ISectionPageViewModel PageViewModel => ViewModel;

    public CustomizePage()
    {
        try
        {
            StartupLogger.Log("Constructor starting...");
            this.InitializeComponent();
            StartupLogger.Log("InitializeComponent done, getting ViewModel...");

            ViewModel = App.Services.GetRequiredService<CustomizeViewModel>();

            InitializeSectionPage(Shell);

            StartupLogger.Log("ViewModel obtained, constructor complete");
        }
        catch (Exception ex)
        {
            StartupLogger.Log($"Constructor EXCEPTION: {ex}");
            throw;
        }
    }
}
