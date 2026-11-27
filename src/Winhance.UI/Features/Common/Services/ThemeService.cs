using Microsoft.UI.Xaml;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Settings;
using Winhance.Core.Features.Common.Extensions;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Windows.UI.ViewManagement;


namespace Winhance.UI.Features.Common.Services;

public class ThemeService : IThemeService
{
    private readonly IUserPreferencesService _userPreferences;
    private readonly IWindowsRegistryService _registryService;
    private readonly IInteractiveUserService _interactiveUserService;
    private readonly IMainWindowProvider _mainWindowProvider;
    private readonly IDispatcherService _dispatcherService;
    private readonly ILogService _logService;
    private readonly UISettings _uiSettings;
    private WinhanceTheme _currentTheme = WinhanceTheme.System;

    public WinhanceTheme CurrentTheme => _currentTheme;

    public event EventHandler<WinhanceTheme>? ThemeChanged;

    public ThemeService(
        IUserPreferencesService userPreferences,
        IWindowsRegistryService registryService,
        IInteractiveUserService interactiveUserService,
        IEventBus eventBus,
        IMainWindowProvider mainWindowProvider,
        IDispatcherService dispatcherService,
        ILogService logService)
    {
        _userPreferences = userPreferences;
        _registryService = registryService;
        _interactiveUserService = interactiveUserService;
        _mainWindowProvider = mainWindowProvider;
        _dispatcherService = dispatcherService;
        _logService = logService;
        _uiSettings = new UISettings();

        _uiSettings.ColorValuesChanged += OnWindowsThemeChanged;

        // Under OTS, UISettings.ColorValuesChanged tracks the admin's theme.
        // Listen for the theme setting being applied so we can update the window.
        if (_interactiveUserService.IsOtsElevation)
        {
            eventBus.Subscribe<SettingAppliedEvent>(OnSettingApplied);
        }
    }

    public void SetTheme(WinhanceTheme theme)
    {
        _currentTheme = theme;
        ApplyTheme(theme);

        _userPreferences.SetPreferenceAsync(UserPreferenceKeys.Theme, theme.ToString()).FireAndForget(_logService);

        ThemeChanged?.Invoke(this, theme);
    }

    public void LoadSavedTheme()
    {
        // Load theme preference synchronously to avoid async/await deadlock on UI thread
        _currentTheme = LoadThemePreferenceSync();
        ApplyTheme(_currentTheme);
    }

    private WinhanceTheme LoadThemePreferenceSync()
    {
        try
        {
            var themeString = _userPreferences.GetPreference<string>(UserPreferenceKeys.Theme, string.Empty);

            if (string.IsNullOrEmpty(themeString))
            {
                return WinhanceTheme.System;
            }

            if (Enum.TryParse<WinhanceTheme>(themeString, out var theme))
            {
                return theme;
            }
        }
        catch
        {
            // Fall through to default
        }

        return WinhanceTheme.System;
    }

    public ElementTheme GetEffectiveTheme()
    {
        return _currentTheme switch
        {
            WinhanceTheme.System => IsWindowsDarkTheme() ? ElementTheme.Dark : ElementTheme.Light,
            WinhanceTheme.LightNative => ElementTheme.Light,
            WinhanceTheme.DarkNative => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }

    private void ApplyTheme(WinhanceTheme theme)
    {
        if (_mainWindowProvider.MainWindow?.Content is not FrameworkElement rootElement)
            return;

        switch (theme)
        {
            case WinhanceTheme.System:
                // Under OTS, ElementTheme.Default follows the admin's theme.
                // Explicitly set based on the interactive user's registry instead.
                if (_interactiveUserService.IsOtsElevation)
                    rootElement.RequestedTheme = IsWindowsDarkTheme() ? ElementTheme.Dark : ElementTheme.Light;
                else
                    rootElement.RequestedTheme = ElementTheme.Default;
                break;

            case WinhanceTheme.LightNative:
                rootElement.RequestedTheme = ElementTheme.Light;
                break;

            case WinhanceTheme.DarkNative:
                rootElement.RequestedTheme = ElementTheme.Dark;
                break;
        }
    }

    private bool IsWindowsDarkTheme()
    {
        if (_interactiveUserService.IsOtsElevation)
        {
            // Under OTS elevation, UISettings reflects the admin's theme.
            // Read from the interactive user's registry hive instead.
            var value = _registryService.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme");
            if (value is int intVal)
                return intVal == 0;
        }

        var foreground = _uiSettings.GetColorValue(UIColorType.Foreground);
        return foreground.R > 128 && foreground.G > 128 && foreground.B > 128;
    }

    private void OnSettingApplied(SettingAppliedEvent evt)
    {
        // No window yet means the dispatcher is not initialized and there is nothing to theme.
        if (evt.SettingId != "theme-mode-windows" || _currentTheme != WinhanceTheme.System || _mainWindowProvider.MainWindow is null)
            return;

        _dispatcherService.Enqueue(() =>
        {
            ApplyTheme(WinhanceTheme.System);
            ThemeChanged?.Invoke(this, WinhanceTheme.System);
        });
    }

    private void OnWindowsThemeChanged(UISettings sender, object args)
    {
        if (_currentTheme == WinhanceTheme.System && _mainWindowProvider.MainWindow is not null)
        {
            _dispatcherService.Enqueue(() =>
            {
                // Under OTS, re-apply explicitly since ElementTheme.Default tracks the admin
                if (_interactiveUserService.IsOtsElevation)
                    ApplyTheme(WinhanceTheme.System);

                ThemeChanged?.Invoke(this, WinhanceTheme.System);
            });
        }
    }
}
