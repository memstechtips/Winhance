using System;
using System.Collections.Generic;

namespace Winhance.WPF.Features.Common.Services
{
    public class ViewPoolConfiguration
    {
        public Dictionary<Type, int> PoolSizes { get; set; } = new();

        public static ViewPoolConfiguration CreateDefault()
        {
            return new ViewPoolConfiguration
            {
                PoolSizes = new Dictionary<Type, int>
                {
                    // Main category views - only need 1 each
                    { typeof(SoftwareApps.Views.SoftwareAppsView), 1 },
                    { typeof(Optimize.Views.OptimizeView), 1 },
                    { typeof(Customize.Views.CustomizeView), 1 },

                    // Software Apps feature views
                    { typeof(SoftwareApps.Views.WindowsAppsView), 1 },
                    { typeof(SoftwareApps.Views.WindowsAppsTableView), 1 },
                    { typeof(SoftwareApps.Views.ExternalAppsView), 1 },
                    { typeof(SoftwareApps.Views.ExternalAppsTableView), 1 },

                    // Optimize feature views
                    { typeof(Optimize.Views.PrivacyAndSecurityOptimizationsView), 1 },
                    { typeof(Optimize.Views.PowerOptimizationsView), 1 },
                    { typeof(Optimize.Views.GamingandPerformanceOptimizationsView), 1 },
                    { typeof(Optimize.Views.UpdateOptimizationsView), 1 },
                    { typeof(Optimize.Views.NotificationOptimizationsView), 1 },
                    { typeof(Optimize.Views.SoundOptimizationsView), 1 },

                    // Customize feature views
                    { typeof(Customize.Views.WindowsThemeCustomizationsView), 1 },
                    { typeof(Customize.Views.TaskbarCustomizationsView), 1 },
                    { typeof(Customize.Views.StartMenuCustomizationsView), 1 },
                    { typeof(Customize.Views.ExplorerCustomizationsView), 1 },

                    // Advanced Tools views
                    { typeof(AdvancedTools.Views.WimUtilView), 1 },
                }
            };
        }
    }
}
