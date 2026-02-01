using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Winhance.UI.Features.Customize.ViewModels;

namespace Winhance.UI.Features.Customize;

/// <summary>
/// Page for customizing Windows appearance and behavior.
/// </summary>
public sealed partial class CustomizePage : Page
{
    private static readonly string LogFile = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "startup-debug.log");
    private static void Log(string msg) { try { File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss.fff}] [CustomizePage] {msg}{Environment.NewLine}"); } catch { } }

    public CustomizeViewModel ViewModel { get; }

    public CustomizePage()
    {
        try
        {
            Log("Constructor starting...");
            this.InitializeComponent();
            Log("InitializeComponent done, getting ViewModel...");
            ViewModel = App.Services.GetRequiredService<CustomizeViewModel>();
            Log("ViewModel obtained, constructor complete");
        }
        catch (Exception ex)
        {
            Log($"Constructor EXCEPTION: {ex}");
            throw;
        }
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        try
        {
            Log("OnNavigatedTo starting...");
            base.OnNavigatedTo(e);
            Log("Calling ViewModel.InitializeAsync...");
            await ViewModel.InitializeAsync();
            Log("OnNavigatedTo complete");
        }
        catch (Exception ex)
        {
            Log($"OnNavigatedTo EXCEPTION: {ex}");
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.OnNavigatedFrom();
    }
}
