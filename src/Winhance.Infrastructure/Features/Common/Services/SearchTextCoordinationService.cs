using System;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class SearchTextCoordinationService : ISearchTextCoordinationService
    {
        private readonly ILogService? _logService;
        private string _currentSearchText = string.Empty;
        public string CurrentSearchText => _currentSearchText;
        public event EventHandler<SearchTextChangedEventArgs>? SearchTextChanged;

        public SearchTextCoordinationService(ILogService? logService = null)
        {
            _logService = logService;
        }

        public void UpdateSearchText(string searchText)
        {
            var previousSearchText = _currentSearchText;
            _currentSearchText = searchText ?? string.Empty;

            _logService?.Log(LogLevel.Debug, $"[SearchTextCoordinationService] Search text changed from '{previousSearchText}' to '{_currentSearchText}'");

            SearchTextChanged?.Invoke(this, new SearchTextChangedEventArgs(_currentSearchText, previousSearchText));
        }
    }
}