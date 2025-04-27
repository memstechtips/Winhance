using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.WPF.Features.Common.ViewModels
{
    public class LoadingWindowViewModel : INotifyPropertyChanged
    {
        private readonly ITaskProgressService _progressService;

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        // Progress properties
        private double _progress;
        public double Progress
        {
            get => _progress;
            set => SetProperty(ref _progress, value);
        }

        private string _progressText = string.Empty;
        public string ProgressText
        {
            get => _progressText;
            set => SetProperty(ref _progressText, value);
        }

        private bool _isIndeterminate = true;
        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            set => SetProperty(ref _isIndeterminate, value);
        }

        private bool _showProgressText;
        public bool ShowProgressText
        {
            get => _showProgressText;
            set => SetProperty(ref _showProgressText, value);
        }

        // Additional properties for more detailed loading information
        private string _statusMessage = "Initializing...";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private string _detailMessage = "Please wait while the application loads...";
        public string DetailMessage
        {
            get => _detailMessage;
            set => SetProperty(ref _detailMessage, value);
        }

        public LoadingWindowViewModel(ITaskProgressService progressService)
        {
            _progressService = progressService ?? throw new ArgumentNullException(nameof(progressService));
            
            // Subscribe to progress events
            _progressService.ProgressUpdated += ProgressService_ProgressUpdated;
        }

        private void ProgressService_ProgressUpdated(object? sender, TaskProgressDetail detail)
        {
            // Update UI properties based on progress events
            IsIndeterminate = detail.IsIndeterminate;
            Progress = detail.Progress ?? 0;
            
            // Update progress text with percentage and status
            ProgressText = detail.IsIndeterminate ? string.Empty : $"{detail.Progress:F0}% - {detail.StatusText}";
            ShowProgressText = !detail.IsIndeterminate && _progressService.IsTaskRunning;
            
            // Update status message with the current operation
            if (!string.IsNullOrEmpty(detail.StatusText))
            {
                StatusMessage = detail.StatusText;
                
                // Update detail message based on the current operation
                if (detail.StatusText.Contains("Loading installable apps"))
                {
                    DetailMessage = "Discovering available applications...";
                }
                else if (detail.StatusText.Contains("Loading removable apps"))
                {
                    DetailMessage = "Identifying Windows applications...";
                }
                else if (detail.StatusText.Contains("Checking installation status"))
                {
                    DetailMessage = "Verifying which applications are installed...";
                }
                else if (detail.StatusText.Contains("Organizing apps"))
                {
                    DetailMessage = "Sorting applications for display...";
                }
                else
                {
                    DetailMessage = "Please wait while the application loads...";
                }
            }
        }

        public void Cleanup()
        {
            // Unsubscribe from events to prevent memory leaks
            _progressService.ProgressUpdated -= ProgressService_ProgressUpdated;
        }
    }
}
