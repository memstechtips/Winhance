using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.Models;

namespace Winhance.WPF.Features.Common.ViewModels
{
    public abstract class BaseViewModel : ObservableObject, IDisposable
    {
        private bool _isDisposed;
        public CancellationTokenSource? _disposalCancellationTokenSource;

        protected BaseViewModel()
        {
            _disposalCancellationTokenSource = new CancellationTokenSource();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed && disposing)
            {
                _disposalCancellationTokenSource?.Cancel();
                _disposalCancellationTokenSource?.Dispose();
                _disposalCancellationTokenSource = null;
                _isDisposed = true;
            }
        }

        ~BaseViewModel()
        {
            Dispose(false);
        }

        public virtual void OnNavigatedTo(object? parameter = null)
        {
        }

        public virtual void OnNavigatedFrom()
        {
        }

    }
}