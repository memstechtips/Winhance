using CommunityToolkit.Mvvm.ComponentModel;

namespace Winhance.UI.Features.Common.ViewModels;

/// <summary>
/// Base class for all ViewModels in the application.
/// </summary>
public abstract class BaseViewModel : ObservableObject, IDisposable
{
    private bool _isDisposed;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_isDisposed && disposing)
        {
            _isDisposed = true;
        }
    }

    ~BaseViewModel()
    {
        Dispose(false);
    }
}
