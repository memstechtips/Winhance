using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.WPF.Features.SoftwareApps.Models
{
    /// <summary>
    /// Optimized wrapper for WindowsApp items in table view
    /// Implements proper memory management and efficient property change notifications
    /// </summary>
    public class OptimizedItemWrapper : INotifyPropertyChanged, IDisposable
    {
        private readonly WindowsApp _item;
        private readonly Action _selectionChangedCallback;
        private bool _disposed = false;

        public OptimizedItemWrapper(WindowsApp item, string itemType, Action selectionChangedCallback = null)
        {
            _item = item ?? throw new ArgumentNullException(nameof(item));
            _selectionChangedCallback = selectionChangedCallback;
            ItemType = itemType;
            
            // Set TypeOrder based on the item type for custom sorting
            TypeOrder = itemType switch
            {
                "Windows App" => 1,
                "Capability" => 2,
                "Optional Feature" => 3,
                _ => 99
            };
            
            // Subscribe to property changes from the wrapped item
            if (_item is INotifyPropertyChanged notifyItem)
            {
                notifyItem.PropertyChanged += OnItemPropertyChanged;
            }
        }

        #region Properties

        public string Name => _item.Name;
        public string Description => _item.Description;
        public bool IsInstalled => _item.IsInstalled;
        public bool CanBeReinstalled => _item.CanBeReinstalled;
        public string PackageName => _item.PackageName;
        public string ItemType { get; }
        public int TypeOrder { get; }

        public bool IsSelected
        {
            get => _item.IsSelected;
            set
            {
                if (_item.IsSelected != value)
                {
                    _item.IsSelected = value;
                    OnPropertyChanged();
                    
                    // Notify ViewModel that selection has changed
                    _selectionChangedCallback?.Invoke();
                }
            }
        }

        /// <summary>
        /// Notifies that the IsSelected property has changed (for external updates)
        /// </summary>
        public void NotifyIsSelectedChanged()
        {
            if (_disposed) return;
            OnPropertyChanged(nameof(IsSelected));
        }

        #endregion

        #region Event Handling

        private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_disposed) return;

            // Forward property changes except for IsSelected (handled locally)
            if (e.PropertyName != nameof(IsSelected))
            {
                OnPropertyChanged(e.PropertyName);
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (_disposed) return;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            // Unsubscribe from property changes
            if (_item is INotifyPropertyChanged notifyItem)
            {
                notifyItem.PropertyChanged -= OnItemPropertyChanged;
            }

            GC.SuppressFinalize(this);
        }

        #endregion

        #region Equality and Hash Code

        public override bool Equals(object obj)
        {
            return obj is OptimizedItemWrapper other && 
                   ReferenceEquals(_item, other._item);
        }

        public override int GetHashCode()
        {
            return _item?.GetHashCode() ?? 0;
        }

        #endregion
    }
}
