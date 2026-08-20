using System.ComponentModel;
using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;
using Winhance.Core.Features.Common.Services;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winhance.UI;

// Runs BEFORE WinUI 3 initialization to redirect duplicate instances.
public static class Program
{
    private const string AppKey = "Winhance-SingleInstance-Key";

    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            StartupLogger.Log("Program", "=== Application Starting ===");
            StartupLogger.Log("Program", $"Args: {string.Join(", ", args)}");
            StartupLogger.Log("Program", $"CurrentDirectory: {Environment.CurrentDirectory}");
            StartupLogger.Log("Program", $"BaseDirectory: {AppContext.BaseDirectory}");

            StartupLogger.Log("Program", "Checking single instance...");
            if (!HandleSingleInstance())
            {
                StartupLogger.Log("Program", "Another instance detected - exiting");
                return;
            }
            StartupLogger.Log("Program", "Single instance check passed");

            StartupLogger.Log("Program", "Initializing COM wrappers...");
            WinRT.ComWrappersSupport.InitializeComWrappers();
            StartupLogger.Log("Program", "COM wrappers initialized");

            StartupLogger.Log("Program", "Starting WinUI 3 Application.Start...");
            Microsoft.UI.Xaml.Application.Start(p =>
            {
                StartupLogger.Log("Program", "Inside Application.Start callback");
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                StartupLogger.Log("Program", "Creating App instance...");
                _ = new App();
                StartupLogger.Log("Program", "App instance created");
            });
            StartupLogger.Log("Program", "Application.Start completed (app closed)");
        }
        catch (Exception ex)
        {
            StartupLogger.Log("Program", $"FATAL EXCEPTION: {ex}");
            throw;
        }
    }

    private static bool HandleSingleInstance()
    {
        var keyInstance = AppInstance.FindOrRegisterForKey(AppKey);

        if (!keyInstance.IsCurrent)
        {
            RedirectActivationTo(keyInstance);
            return false;
        }

        keyInstance.Activated += OnActivated;
        return true;
    }

    private static void RedirectActivationTo(AppInstance keyInstance)
    {
        var args = AppInstance.GetCurrent().GetActivatedEventArgs();

        // Run redirection on background thread (required for STA compliance)
        var redirectTask = Task.Run(async () =>
        {
            await keyInstance.RedirectActivationToAsync(args);
        });
        redirectTask.Wait();

        ActivateExistingWindow(keyInstance);
    }

    private static void ActivateExistingWindow(AppInstance keyInstance)
    {
        try
        {
            var process = Process.GetProcessById((int)keyInstance.ProcessId);
            var hwnd = new Windows.Win32.Foundation.HWND(process.MainWindowHandle);

            if (hwnd != IntPtr.Zero)
            {
                // Restore if minimized
                if (PInvoke.IsIconic(hwnd))
                {
                    PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_RESTORE);
                }

                // Use AllowSetForegroundWindow for reliable foreground activation
                PInvoke.AllowSetForegroundWindow((uint)keyInstance.ProcessId);
                PInvoke.SetForegroundWindow(hwnd);
            }
        }
        catch (Exception ex) when (ex is ArgumentException or Win32Exception)
        {
            // The other instance is gone (or the process list was unreadable); nothing to bring forward.
        }
    }

    private static void OnActivated(object? sender, AppActivationArguments args)
    {
        // This runs on the main instance when another instance redirects
        // The window is already being brought to foreground by the other instance
    }
}
