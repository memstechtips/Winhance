using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Helpers;
using Winhance.UI.Helpers;

namespace Winhance.UI.Features.Common.ViewModels;

public partial class MoreMenuViewModel : ObservableObject, IDisposable
{
    private static readonly string[] LocalizedProperties =
    [
        nameof(MenuDocumentation), nameof(MenuReportBug), nameof(MenuCheckForUpdates), nameof(MenuWinhanceLogs),
        nameof(MenuChangeHistory), nameof(MenuWinhanceScripts), nameof(MenuSupportWinhance), nameof(MenuCloseWinhance),
    ];

    private bool _disposed;
    private readonly ILocalizationService _localizationService;
    private readonly IVersionService _versionService;
    private readonly ILogService _logService;
    private readonly IApplicationCloseService _applicationCloseService;
    private readonly IFileSystemService _fileSystemService;
    private readonly IExplorerWindowManager _explorerWindowManager;
    private readonly IChangeHistoryService _changeHistoryService;
    private readonly IProcessExecutor _processExecutor;
    private readonly IDialogService _dialogService;

    [ObservableProperty]
    public partial string VersionInfo { get; set; }

    public MoreMenuViewModel(
        ILocalizationService localizationService,
        IVersionService versionService,
        ILogService logService,
        IApplicationCloseService applicationCloseService,
        IFileSystemService fileSystemService,
        IExplorerWindowManager explorerWindowManager,
        IChangeHistoryService changeHistoryService,
        IProcessExecutor processExecutor,
        IDialogService dialogService)
    {
        _localizationService = localizationService;
        _versionService = versionService;
        _logService = logService;
        _applicationCloseService = applicationCloseService;
        _fileSystemService = fileSystemService;
        _explorerWindowManager = explorerWindowManager;
        _changeHistoryService = changeHistoryService;
        _processExecutor = processExecutor;
        _dialogService = dialogService;
        VersionInfo = "Winhance";

        _localizationService.LanguageChanged += OnLanguageChanged;

        InitializeVersionInfo();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _localizationService.LanguageChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        foreach (var name in LocalizedProperties)
            OnPropertyChanged(name);
    }

    private void InitializeVersionInfo()
    {
        try
        {
            var versionInfo = _versionService.GetCurrentVersion();
            VersionInfo = $"Winhance {versionInfo.Version}";
        }
        catch (Exception ex)
        {
            _logService.LogDebug($"Failed to get version info: {ex.Message}");
            VersionInfo = "Winhance";
        }
    }

    public string MenuDocumentation =>
        _localizationService.GetStringOrDefault("Tooltip_Documentation", "Documentation");

    public string MenuReportBug =>
        _localizationService.GetStringOrDefault("Tooltip_ReportBug", "Report a Bug");

    public string MenuCheckForUpdates =>
        _localizationService.GetStringOrDefault("Menu_CheckForUpdates", "Check for Updates");

    public string MenuWinhanceLogs =>
        _localizationService.GetStringOrDefault("Menu_WinhanceLogs", "Winhance Logs");

    public string MenuChangeHistory =>
        _localizationService.GetStringOrDefault("Menu_ChangeHistory", "Change History");

    public string MenuWinhanceScripts =>
        _localizationService.GetStringOrDefault("Menu_WinhanceScripts", "Winhance Scripts");

    public string MenuSupportWinhance =>
        _localizationService.GetStringOrDefault("Menu_SupportWinhance", "Support Winhance");

    public string MenuCloseWinhance =>
        _localizationService.GetStringOrDefault("Menu_CloseWinhance", "Close Winhance");

    [RelayCommand]
    private Task OpenDocsAsync(string? pageTag) =>
        RunAsync(async () => await Windows.System.Launcher.LaunchUriAsync(HelpLinks.DocsFor(pageTag)));

    [RelayCommand]
    private Task ReportBugAsync() =>
        RunAsync(async () => await Windows.System.Launcher.LaunchUriAsync(HelpLinks.BugReport));

    [RelayCommand]
    private Task OpenLogsAsync() => RunAsync(() => OpenFolderAsync(Path.Combine(AppDataPaths.MachineRoot, "Logs")));

    [RelayCommand]
    private Task OpenChangeHistoryAsync() =>
        RunAsync(() => _processExecutor.ShellExecuteAsync(_changeHistoryService.GetFilePath()));

    [RelayCommand]
    private Task OpenScriptsAsync() => RunAsync(() => OpenFolderAsync(ScriptPaths.ScriptsDirectory));

    [RelayCommand]
    private Task SupportWinhanceAsync() =>
        RunAsync(() => _dialogService.ShowSponsorsDialogAsync(SponsorsDialogMode.Normal));

    [RelayCommand]
    private Task CloseApplicationAsync() =>
        RunAsync(() =>
        {
            _logService.LogInformation("User requested application close from More menu");
            return _applicationCloseService.CheckOperationsAndCloseAsync();
        });

    private async Task OpenFolderAsync(string folder)
    {
        if (!_fileSystemService.DirectoryExists(folder))
            _fileSystemService.CreateDirectory(folder);

        await _explorerWindowManager.OpenFolderAsync(folder);
    }

    private Task RunAsync(Func<Task> action, [CallerMemberName] string callerName = "") =>
        _dialogService.RunOrShowErrorAsync(_logService, action,
            ex => _localizationService.GetString("Common_ActionFailed", ex.Message), callerName);
}
