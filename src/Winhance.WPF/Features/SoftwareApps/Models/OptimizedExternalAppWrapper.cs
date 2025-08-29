using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Winhance.Core.Features.SoftwareApps.Models;

namespace Winhance.WPF.Features.SoftwareApps.Models
{
    /// <summary>
    /// Optimized wrapper for ExternalApp items in table view
    /// Implements proper memory management and efficient property change notifications
    /// </summary>
    public class OptimizedExternalAppWrapper : INotifyPropertyChanged, IDisposable
    {
        private readonly ExternalApp _item;
        private readonly Action _selectionChangedCallback;
        private bool _disposed = false;

        public OptimizedExternalAppWrapper(ExternalApp item, Action selectionChangedCallback = null)
        {
            _item = item ?? throw new ArgumentNullException(nameof(item));
            _selectionChangedCallback = selectionChangedCallback;

            // Subscribe to property changes from the wrapped item
            if (_item is INotifyPropertyChanged notifyItem)
            {
                notifyItem.PropertyChanged += OnItemPropertyChanged;
            }
        }

        #region Properties

        public string Name => _item.Name;
        public string Description => _item.Description;
        public string PackageName => _item.PackageName;
        public string Category => _item.Category;
        public string Publisher => "External"; // Default for external apps
        public string Source => "winget/msstore"; // Static for external apps
        public bool IsInstalled => _item.IsInstalled;

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

        /// <summary>
        /// Notifies that the IsSelected property has changed (for external updates)
        /// </summary>
        public void NotifyIsSelectedChanged()
        {
            if (_disposed) return;
            OnPropertyChanged(nameof(IsSelected));
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
            return obj is OptimizedExternalAppWrapper other &&
                   ReferenceEquals(_item, other._item);
        }

        public override int GetHashCode()
        {
            return _item?.GetHashCode() ?? 0;
        }

        #endregion
    }
}
