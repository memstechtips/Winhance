using System;
using System.Windows;
using System.Windows.Interop;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Resources.Theme;
using Winhance.WPF.Features.Common.Services;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Common.Views
{
    public partial class LoadingWindow : Window, IThemeAwareWindow
    {
        private readonly IThemeManager _themeManager;
        private readonly WindowIconService _windowIconService;
        private readonly WindowEffectsService _windowEffectsService;

        public LoadingWindow()
        {
            InitializeComponent();
        }

        public LoadingWindow(IThemeManager themeManager, ITaskProgressService progressService)
        {
            _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
            _windowIconService = new WindowIconService(themeManager);
            _windowEffectsService = new WindowEffectsService();

            InitializeComponent();

            var viewModel = new LoadingWindowViewModel(progressService);
            DataContext = viewModel;

            UpdateThemeIcon();
        }

        public void OnThemeChanged(bool isDarkTheme)
        {
            UpdateThemeIcon();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            try
            {
                var helper = new WindowInteropHelper(this);
                if (helper.Handle != IntPtr.Zero)
                    _windowEffectsService.EnableBlur(this);
            }
            catch
            {
                // Ignore blur errors
            }
        }

        private void UpdateThemeIcon()
        {
            _windowIconService?.UpdateTitleBarIcon(AppIconImage);
        }
    }
}
