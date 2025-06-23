using System.ComponentModel;

namespace Winhance.WPF.Features.SoftwareApps.Models
{
    /// <summary>
    /// Wrapper class for ExternalApp items in the table view
    /// </summary>
    public class ExternalAppWithTableInfo : INotifyPropertyChanged
    {
        private readonly ExternalApp _app;
        
        public ExternalAppWithTableInfo(ExternalApp app)
        {
            _app = app;
            
            // Forward property change events from the wrapped item
            if (_app is INotifyPropertyChanged notifyItem)
            {
                notifyItem.PropertyChanged += (sender, args) =>
                {
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(args.PropertyName));
                };
            }
        }
        
        // Forward properties from the wrapped ExternalApp
        public string Name => _app.Name;
        public string Description => _app.Description;
        public string PackageName => _app.PackageName;
        public string Category => _app.Category;
        public bool IsInstalled => _app.IsInstalled;
        
        public bool IsSelected
        {
            get => _app.IsSelected;
            set
            {
                if (_app.IsSelected != value)
                {
                    _app.IsSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }
        
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
