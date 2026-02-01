using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Optimize;

/// <summary>
/// Page for Windows optimization settings (Sound, Update, Notifications, Privacy, Power, Gaming).
/// </summary>
public sealed partial class OptimizePage : Page
{
    private static readonly string LogFile = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "startup-debug.log");
    private static void Log(string msg) { try { File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss.fff}] [OptimizePage] {msg}{Environment.NewLine}"); } catch { } }

    public OptimizeViewModel ViewModel { get; }

    public OptimizePage()
    {
        try
        {
            Log("Constructor starting...");
            this.InitializeComponent();
            Log("InitializeComponent done, getting ViewModel...");
            ViewModel = App.Services.GetRequiredService<OptimizeViewModel>();
            Log("ViewModel obtained");
            this.NavigationCacheMode = NavigationCacheMode.Required;
            Log("Constructor complete");
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
