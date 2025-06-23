using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Utilities;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Common.Controls
{
    /// <summary>
    /// Interaction logic for MoreMenu.xaml
    /// </summary>
    public partial class MoreMenu : UserControl
    {
        public MoreMenu()
        {
            try
            {
                LogToStartupLog("MoreMenu constructor starting");
                InitializeComponent();
                LogToStartupLog("MoreMenu constructor completed successfully");
            }
            catch (Exception ex)
            {
                LogToStartupLog($"ERROR in MoreMenu constructor: {ex.Message}");
                LogToStartupLog($"Stack trace: {ex.StackTrace}");
                throw; // Re-throw to ensure the error is properly handled
            }
        }

        /// <summary>
        /// Logs a message directly to the WinhanceStartupLog.txt file
        /// </summary>
        private void LogToStartupLog(string message)
        {
#if DEBUG
            try
            {
                string logsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Winhance",
                    "Logs"
                );
                string logFile = Path.Combine(logsDir, "WinhanceStartupLog.txt");

                // Ensure the logs directory exists
                if (!Directory.Exists(logsDir))
                {
                    Directory.CreateDirectory(logsDir);
                }

                // Format the log message with timestamp
                string formattedMessage =
                    $"[{DateTime.Now:yyyy/MM/dd HH:mm:ss}] [MoreMenu] {message}";

                // Append to the log file
                using (StreamWriter writer = File.AppendText(logFile))
                {
                    writer.WriteLine(formattedMessage);
                }
            }
            catch
            {
                // Ignore errors in logging to avoid cascading issues
            }
#endif
        }

        /// <summary>
        /// Shows the context menu at the specified placement target
        /// </summary>
        /// <param name="placementTarget">The UI element that the context menu should be positioned relative to</param>
        public void ShowMenu(UIElement placementTarget)
        {
            try
            {
                LogToStartupLog("ShowMenu method called");
                LogToStartupLog($"placementTarget exists: {placementTarget != null}");
                LogToStartupLog($"DataContext exists: {DataContext != null}");
                LogToStartupLog(
                    $"DataContext type: {(DataContext != null ? DataContext.GetType().FullName : "null")}"
                );

                // Get the context menu from resources
                var contextMenu = Resources["MoreButtonContextMenu"] as ContextMenu;
                LogToStartupLog($"ContextMenu from resources exists: {contextMenu != null}");

                if (contextMenu != null)
                {
                    try
                    {
                        // Ensure the context menu has the same DataContext as the control
                        LogToStartupLog("Setting context menu DataContext");
                        contextMenu.DataContext = this.DataContext;
                        LogToStartupLog(
                            $"Context menu DataContext set: {contextMenu.DataContext != null}"
                        );

                        // Add a handler to log when menu items are clicked
                        foreach (var item in contextMenu.Items)
                        {
                            if (item is MenuItem menuItem)
                            {
                                LogToStartupLog(
                                    $"Menu item: {menuItem.Header}, Command bound: {menuItem.Command != null}"
                                );

                                // Add a Click handler to log when the item is clicked
                                menuItem.Click += (s, e) =>
                                {
                                    LogToStartupLog($"Menu item clicked: {menuItem.Header}");
                                    if (menuItem.Command != null)
                                    {
                                        LogToStartupLog(
                                            $"Command can execute: {menuItem.Command.CanExecute(null)}"
                                        );
                                    }
                                };
                            }
                        }

                        // Set the placement target
                        LogToStartupLog("Setting placement target");
                        contextMenu.PlacementTarget = placementTarget;
                        LogToStartupLog("Placement target set successfully");

                        // Set placement mode to Right to show menu to the right of the button
                        LogToStartupLog("Setting placement mode");
                        contextMenu.Placement = System
                            .Windows
                            .Controls
                            .Primitives
                            .PlacementMode
                            .Right;
                        LogToStartupLog("Placement mode set successfully");

                        // No horizontal or vertical offset
                        LogToStartupLog("Setting offset values");
                        contextMenu.HorizontalOffset = 0;
                        contextMenu.VerticalOffset = 0;
                        LogToStartupLog("Offset values set successfully");

                        // Register for the Closed event to reset navigation button selection
                        contextMenu.Closed += ContextMenu_Closed;
                        LogToStartupLog("Registered for context menu Closed event");

                        // Open the context menu
                        LogToStartupLog("About to open context menu");
                        contextMenu.IsOpen = true;
                        LogToStartupLog("Context menu opened successfully");
                    }
                    catch (Exception menuEx)
                    {
                        LogToStartupLog($"ERROR configuring context menu: {menuEx.Message}");
                        LogToStartupLog($"Stack trace: {menuEx.StackTrace}");
                    }
                }
                else
                {
                    LogToStartupLog("ERROR: ContextMenu from resources is null, cannot show menu");
                }
            }
            catch (Exception ex)
            {
                LogToStartupLog($"ERROR in ShowMenu method: {ex.Message}");
                LogToStartupLog($"Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Handles the context menu closing event to reset the navigation button selection
        /// </summary>
        private void ContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            try
            {
                LogToStartupLog("ContextMenu_Closed event handler called");
                
                // Find the parent window to access the MainViewModel
                Window parentWindow = Window.GetWindow(this);
                if (parentWindow != null && parentWindow.DataContext is MainViewModel mainViewModel)
                {
                    LogToStartupLog("Found MainViewModel, resetting navigation selection");
                    
                    // Reset the selected navigation item to the current view
                    string currentView = mainViewModel.CurrentViewName;
                    LogToStartupLog($"Current view is: {currentView}");
                    
                    // Update the selected navigation item to match the current view
                    mainViewModel.SelectedNavigationItem = currentView;
                    LogToStartupLog($"Reset SelectedNavigationItem to: {currentView}");
                }
                else
                {
                    LogToStartupLog("Could not find MainViewModel to reset navigation selection");
                }
            }
            catch (Exception ex)
            {
                LogToStartupLog($"ERROR in ContextMenu_Closed: {ex.Message}");
                LogToStartupLog($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}
