using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Services;
using Winhance.UI.Features.Common.Controls;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Optimize;

public sealed partial class OptimizePage : SectionPage
{
    public OptimizeViewModel ViewModel { get; }

    protected override ISectionPageViewModel PageViewModel => ViewModel;

    protected override string LogTag => "OptimizePage";

    public OptimizePage()
    {
        try
        {
            StartupLogger.Log("OptimizePage", "Constructor starting...");
            this.InitializeComponent();
            StartupLogger.Log("OptimizePage", "InitializeComponent done, getting ViewModel...");

            ViewModel = App.Services.GetRequiredService<OptimizeViewModel>();

            InitializeSectionPage(Shell);

            StartupLogger.Log("OptimizePage", "ViewModel obtained, constructor complete");
        }
        catch (Exception ex)
        {
            StartupLogger.Log("OptimizePage", $"Constructor EXCEPTION: {ex}");
            throw;
        }
    }
}
