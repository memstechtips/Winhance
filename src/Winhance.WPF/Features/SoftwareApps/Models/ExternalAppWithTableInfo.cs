using System;
using System.ComponentModel;

namespace Winhance.WPF.Features.SoftwareApps.Models
{
    /// <summary>
    /// Wrapper class for ExternalApp items in the table view
    /// </summary>
    public class ExternalAppWithTableInfo : INotifyPropertyChanged
    {
        private readonly ExternalApp _app;
        private readonly Action _selectionChangedCallback;
        
        public ExternalAppWithTableInfo(ExternalApp app, Action selectionChangedCallback = null)
        {
            _app = app;
            _selectionChangedCallback = selectionChangedCallback;
            
            // Forward property change events from the wrapped item
            // Skip properties that are handled locally to prevent double notifications
            if (_app is INotifyPropertyChanged notifyItem)
            {
                notifyItem.PropertyChanged += (sender, args) =>
                {
                    // Don't forward property changes for properties we handle locally
                    if (args.PropertyName != nameof(IsSelected))
                    {
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(args.PropertyName));
                    }
                };
            }
        }
        
        // Forward properties from the wrapped ExternalApp
        public string Name => _app.Name;
        public string Description => _app.Description;
        public string PackageName => _app.PackageName;
        public string Category => _app.Category;
        public bool IsInstalled => _app.IsInstalled;
        
        // Hardcoded source property as requested
        public string Source => "winget/msstore";
        
        public bool IsSelected
        {
            get => _app.IsSelected;
            set
            {
                if (_app.IsSelected != value)
                {
                    // Log to desktop file
                    ViewModels.DebugLogger.Log($"[DEBUG] ExternalAppWithTableInfo: IsSelected changing from {_app.IsSelected} to {value} for {_app.Name}");
                    
                    _app.IsSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                    
                    // Notify ViewModel that selection has changed - ensure this happens on the UI thread
                    ViewModels.DebugLogger.Log($"[DEBUG] ExternalAppWithTableInfo: Calling selection callback for {_app.Name}");
                    
                    // First immediate callback
                    _selectionChangedCallback?.Invoke();
                    
                    // Also dispatch a delayed callback to ensure UI updates correctly
                    // This helps when multiple items are being selected in quick succession
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => 
                    {
                        ViewModels.DebugLogger.Log($"[DEBUG] ExternalAppWithTableInfo: Calling delayed selection callback for {_app.Name}");
                        _selectionChangedCallback?.Invoke();
                    }));
                }
            }
        }
        
        /// <summary>
        /// Public method to notify that IsSelected property has changed (for ViewModel use)
        /// </summary>
        public void NotifyIsSelectedChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
        
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
