using System;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface INavigationService
    {
        bool NavigateTo(string viewName);
        bool NavigateTo(string viewName, object parameter);
        bool NavigateBack();
        Task<bool> PreloadAndNavigateToAsync(string viewName);
        Task NavigateToAsync(string viewName);
        Task NavigateToAsync(string viewName, object parameter);

        string CurrentView { get; }

        event EventHandler<NavigationEventArgs>? Navigated;
        event EventHandler<NavigationEventArgs>? Navigating;
        event EventHandler<NavigationEventArgs>? NavigationFailed;
    }

    public class NavigationEventArgs : EventArgs
    {
        public string SourceView { get; }
        public string TargetView { get; }
        public string Route => TargetView;
        public Type? ViewModelType => Parameter?.GetType();
        public object? Parameter { get; }
        public bool CanCancel { get; }
        public bool Cancel { get; set; }

        public NavigationEventArgs(
            string sourceView,
            string targetView,
            object? parameter = null,
            bool canCancel = false
        )
        {
            SourceView = sourceView;
            TargetView = targetView;
            Parameter = parameter;
            CanCancel = canCancel;
        }
    }
}
