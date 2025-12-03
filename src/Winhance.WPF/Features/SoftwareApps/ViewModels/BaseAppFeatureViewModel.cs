using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Utils;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.SoftwareApps.ViewModels;

public interface ISelectable
{
    bool IsSelected { get; set; }
    string Name { get; }
}

public abstract partial class BaseAppFeatureViewModel<T>(
    ITaskProgressService progressService,
    ILogService logService,
    IEventBus eventBus,
    IDialogService dialogService,
    IInternetConnectivityService connectivityService,
    ILocalizationService localizationService) : BaseFeatureViewModel, IAppFeatureViewModel where T : class, ISelectable
{
    private bool _isDisposed;

    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private bool _isInitialized = false;
    [ObservableProperty] private bool _isTableViewMode;
    [ObservableProperty] protected string _currentSortProperty = "Name";
    [ObservableProperty] protected ListSortDirection _sortDirection = ListSortDirection.Ascending;

    public ObservableCollection<T> Items { get; } = new();

    public bool IsTaskRunning => progressService.IsTaskRunning;

    protected CancellationReason CurrentCancellationReason { get; set; } = CancellationReason.None;

    public override abstract string ModuleId { get; }
    public override abstract string DisplayName { get; }
    public override bool IsVisibleInSearch => Items.Any(item => MatchesSearchTerm(SearchText, item));

    public override void ApplySearchFilter(string searchText)
    {
        SearchText = searchText ?? string.Empty;
    }

    public Visibility GridViewVisibility => IsTableViewMode ? Visibility.Collapsed : Visibility.Visible;
    public Visibility TableViewVisibility => IsTableViewMode ? Visibility.Visible : Visibility.Collapsed;

    public bool IsSearchActive => !string.IsNullOrWhiteSpace(SearchText);


    private bool _hasSelectedItems;
    private bool _hasSelectedItemsCacheValid;

    public bool HasSelectedItems
    {
        get
        {
            if (!_hasSelectedItemsCacheValid)
            {
                _hasSelectedItems = Items?.Any(a => a.IsSelected) == true;
                _hasSelectedItemsCacheValid = true;
            }
            return _hasSelectedItems;
        }
    }

    protected void InvalidateHasSelectedItemsCache()
    {
        _hasSelectedItemsCacheValid = false;
    }

    [RelayCommand]
    private void ToggleViewMode(object parameter = null)
    {
        if (parameter != null)
        {
            if (parameter is bool tableViewMode)
            {
                IsTableViewMode = tableViewMode;
            }
            else if (parameter is string stringParam && bool.TryParse(stringParam, out bool result))
            {
                IsTableViewMode = result;
            }
        }
        else
        {
            IsTableViewMode = !IsTableViewMode;
        }

        OnPropertyChanged(nameof(GridViewVisibility));
        OnPropertyChanged(nameof(TableViewVisibility));
    }

    protected bool MatchesSearchTerm(string searchTerm, T item)
    {
        return SearchHelper.MatchesSearchTerm(searchTerm, GetSearchableFields(item));
    }

    protected IEnumerable<T> FilterItems(IEnumerable<T> items)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
            return items;

        return items.Where(item => MatchesSearchTerm(SearchText, item));
    }

    protected override void OnSearchTextChangedCore(string value)
    {
        ApplySearch();
        OnPropertyChanged(nameof(IsSearchActive));
    }

    protected async Task<bool> ShowConfirmationAsync(string operation, IEnumerable<T> selectedItems)
    {
        string title = $"Confirm {operation}";
        string headerText = $"The following items will be {GetPastTense(operation)}:";
        var itemNames = selectedItems.Select(GetItemName).ToList();

        string message = $"{headerText}\n";
        foreach (var name in itemNames)
        {
            message += $"{name}\n";
        }
        message += "\nDo you want to continue?";

        return await dialogService.ShowConfirmationAsync(message, title);
    }

    protected async Task ShowNoItemsSelectedDialogAsync(string action)
    {
        await dialogService.ShowWarningAsync(
            $"Please select at least one item for {action}.",
            "No Items Selected"
        );
    }

    protected async Task ShowNoInternetConnectionDialogAsync()
    {
        await dialogService.ShowWarningAsync(
            "An internet connection is required to install apps. Please check your connection and try again.",
            "No Internet Connection"
        );
    }

    protected async Task ShowOperationResultDialogAsync(
        string operationType,
        int successCount,
        int totalCount,
        IEnumerable<string> successItems,
        IEnumerable<string>? failedItems = null,
        IEnumerable<string>? skippedItems = null)
    {
        bool isUserCancelled = CurrentCancellationReason == CancellationReason.UserCancelled;
        bool isConnectivityIssue = CurrentCancellationReason == CancellationReason.InternetConnectivityLost;

        if (isUserCancelled)
        {
            CurrentCancellationReason = CancellationReason.None;
            return;
        }

        int failedCount = failedItems?.Count() ?? 0;
        int skippedCount = skippedItems?.Count() ?? 0;
        bool hasErrors = failedCount > 0 || skippedCount > 0 || isConnectivityIssue;

        if (!hasErrors)
        {
            return;
        }

        dialogService.ShowOperationResult(
            operationType,
            successCount,
            totalCount,
            successItems,
            failedItems,
            skippedItems,
            isConnectivityIssue,
            false
        );

        if (isConnectivityIssue)
        {
            CurrentCancellationReason = CancellationReason.None;
        }
    }

    protected string GetPastTense(string operationType)
    {
        return operationType.ToLower() switch
        {
            "install" => "installed",
            "remove" => "removed",
            "uninstall" => "uninstalled",
            "reinstall" => "reinstalled",
            "enable" => "enabled",
            "disable" => "disabled",
            _ => $"{operationType}ed"
        };
    }

    partial void OnIsTableViewModeChanged(bool value)
    {
        OnPropertyChanged(nameof(GridViewVisibility));
        OnPropertyChanged(nameof(TableViewVisibility));
        if (value)
        {
            OnTableViewModeChanged();
        }
    }

    protected virtual void OnTableViewModeChanged()
    {
    }

    protected virtual void ApplySearch()
    {
    }

    [RelayCommand]
    public void SortBy(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName)) return;

        if (propertyName == CurrentSortProperty)
        {
            SortDirection = SortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        }
        else
        {
            CurrentSortProperty = propertyName;
            SortDirection = ListSortDirection.Ascending;
        }

        ApplyOptimizedSorting();
    }

    [RelayCommand]
    public void HandleSelectionChanged()
    {
        OnOptimizedSelectionChanged();
    }

    [RelayCommand]
    public void HandleCheckboxSelectionChanged()
    {
        OnOptimizedSelectionChanged();
    }

    [RelayCommand]
    public virtual void ClearSelectedItems()
    {
        foreach (var item in Items)
            item.IsSelected = false;
        StatusText = localizationService.GetString("Progress_SelectionsCleared");
    }

    protected async Task<bool> CheckConnectivityAsync()
    {
        bool isConnected = await connectivityService.IsInternetConnectedAsync(true);
        if (!isConnected)
        {
            StatusText = localizationService.GetString("Progress_NoInternetConnection");
            await ShowNoInternetConnectionDialogAsync();
            return false;
        }
        return true;
    }

    protected async Task<bool?> ShowConfirmItemsDialogAsync(string operation, IEnumerable<string> itemNames, int count)
    {
        return await dialogService.ShowAppOperationConfirmationAsync(operation, itemNames, count);
    }


    protected virtual string[] GetSearchableFields(T item) => new[] { GetItemName(item) };
    protected abstract string GetItemName(T item);
    protected abstract void ApplyOptimizedSorting();
    protected abstract void OnOptimizedSelectionChanged();

    public abstract Task LoadItemsAsync();
    public abstract Task CheckInstallationStatusAsync();

    protected async Task ExecuteWithProgressAsync(
        Func<ITaskProgressService, Task> operation,
        string taskName,
        bool isIndeterminate = false)
    {
        try
        {
            progressService.StartTask(taskName, isIndeterminate);
            OnPropertyChanged(nameof(IsTaskRunning));
            await operation(progressService);
        }
        catch (Exception ex)
        {
            logService?.LogError($"Error in {taskName}: {ex.Message}", ex);
            throw;
        }
        finally
        {
            if (progressService.IsTaskRunning)
            {
                progressService.CompleteTask();
            }
            OnPropertyChanged(nameof(IsTaskRunning));
        }
    }

    protected async Task<T> ExecuteWithProgressAsync<T>(
        Func<ITaskProgressService, Task<T>> operation,
        string taskName,
        bool isIndeterminate = false)
    {
        try
        {
            progressService.StartTask(taskName, isIndeterminate);
            OnPropertyChanged(nameof(IsTaskRunning));
            return await operation(progressService);
        }
        catch (Exception ex)
        {
            logService?.LogError($"Error in {taskName}: {ex.Message}", ex);
            throw;
        }
        finally
        {
            if (progressService.IsTaskRunning)
            {
                progressService.CompleteTask();
            }
            OnPropertyChanged(nameof(IsTaskRunning));
        }
    }


    protected override void Dispose(bool disposing)
    {
        if (!_isDisposed && disposing)
        {
            _isDisposed = true;
        }
        base.Dispose(disposing);
    }
}