using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Services;
using Winhance.UI.Features.Common.Controls;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Customize.ViewModels;

namespace Winhance.UI.Features.Customize;

/// <summary>
/// Customize overview. The chrome is <see cref="SectionPageShell"/> and the behaviour is
/// <see cref="SectionPage"/>; what remains here is this page's identity - its ViewModel and its
/// log tag.
/// </summary>
public sealed partial class CustomizePage : SectionPage
{
    public CustomizeViewModel ViewModel { get; }

    protected override ISectionPageViewModel PageViewModel => ViewModel;

    protected override string LogTag => "CustomizePage";

    public CustomizePage()
    {
        try
        {
            StartupLogger.Log("CustomizePage", "Constructor starting...");
            this.InitializeComponent();
            StartupLogger.Log("CustomizePage", "InitializeComponent done, getting ViewModel...");

            ViewModel = App.Services.GetRequiredService<CustomizeViewModel>();

            InitializeSectionPage(Shell);

            StartupLogger.Log("CustomizePage", "ViewModel obtained, constructor complete");
        }
        catch (Exception ex)
        {
            StartupLogger.Log("CustomizePage", $"Constructor EXCEPTION: {ex}");
            throw;
        }
    }
}
