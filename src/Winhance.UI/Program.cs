using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.Windows.AppLifecycle;
using Winhance.Core.Features.Common.Services;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Winhance.UI;

/// <summary>
/// Custom program entry point for single-instance enforcement using AppLifecycle API.
/// This runs BEFORE WinUI 3 initialization to redirect duplicate instances.
/// </summary>
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

            // Handle single-instance BEFORE any WinUI 3 initialization
            StartupLogger.Log("Program", "Checking single instance...");
            if (!HandleSingleInstance())
            {
                StartupLogger.Log("Program", "Another instance detected - exiting");
                return;
            }
            StartupLogger.Log("Program", "Single instance check passed");

            // This is the first (or only) instance - proceed with normal WinUI 3 startup
            StartupLogger.Log("Program", "Initializing COM wrappers...");
            WinRT.ComWrappersSupport.InitializeComWrappers();
            StartupLogger.Log("Program", "COM wrappers initialized");

            // Initialize the WinUI 3 application
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

    /// <summary>
    /// Handles single-instance enforcement using AppLifecycle API.
    /// </summary>
    /// <returns>True if this is the first instance and should continue, false if another instance is running.</returns>
    private static bool HandleSingleInstance()
    {
        // Declare this instance with a unique key
        var keyInstance = AppInstance.FindOrRegisterForKey(AppKey);

        if (!keyInstance.IsCurrent)
        {
            // Another instance owns this key - redirect to it
            RedirectActivationTo(keyInstance);
            return false;
        }

        // This is the first instance - register for activation from other instances
        keyInstance.Activated += OnActivated;
        return true;
    }

    /// <summary>
    /// Redirects activation to an existing instance.
    /// </summary>
    private static void RedirectActivationTo(AppInstance keyInstance)
    {
        // Get activation args and redirect to existing instance
        var args = AppInstance.GetCurrent().GetActivatedEventArgs();

        // Run redirection on background thread (required for STA compliance)
        var redirectTask = Task.Run(async () =>
        {
            await keyInstance.RedirectActivationToAsync(args);
        });
        redirectTask.Wait();

        // Bring existing window to foreground using reliable Win32 approach
        ActivateExistingWindow(keyInstance);
    }

    /// <summary>
    /// Brings the existing instance's main window to the foreground.
    /// </summary>
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
        catch (Exception)
        {
            // Process may have exited - ignore gracefully
        }
    }

    /// <summary>
    /// Handles activation from redirected instances.
    /// </summary>
    private static void OnActivated(object? sender, AppActivationArguments args)
    {
        // This runs on the main instance when another instance redirects
        // The window is already being brought to foreground by the other instance
        // Additional handling can be added here if needed (e.g., process command line args)
    }
}
