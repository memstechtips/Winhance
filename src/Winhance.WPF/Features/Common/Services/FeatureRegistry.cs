using System;
using System.Collections.Generic;
using System.Linq;
using MahApps.Metro.IconPacks;
using Winhance.Core.Features.Common.Constants;
using Winhance.WPF.Features.Common.Constants;
using Winhance.WPF.Features.Common.Models;
using Winhance.WPF.Features.Customize.ViewModels;
using Winhance.WPF.Features.Customize.Views;
using Winhance.WPF.Features.Optimize.ViewModels;
using Winhance.WPF.Features.Optimize.Views;
using Winhance.WPF.Features.SoftwareApps.ViewModels;
using Winhance.WPF.Features.SoftwareApps.Views;

namespace Winhance.WPF.Features.Common.Services
{
    public static class FeatureRegistry
    {
        private static readonly Dictionary<string, (Type Vm, Type View)> TypeMap = new()
        {
            [FeatureIds.WindowsTheme] = (typeof(WindowsThemeCustomizationsViewModel), typeof(WindowsThemeCustomizationsView)),
            [FeatureIds.Taskbar] = (typeof(TaskbarCustomizationsViewModel), typeof(TaskbarCustomizationsView)),
            [FeatureIds.StartMenu] = (typeof(StartMenuCustomizationsViewModel), typeof(StartMenuCustomizationsView)),
            [FeatureIds.ExplorerCustomization] = (typeof(ExplorerCustomizationsViewModel), typeof(ExplorerCustomizationsView)),
            [FeatureIds.Privacy] = (typeof(PrivacyAndSecurityOptimizationsViewModel), typeof(PrivacyAndSecurityOptimizationsView)),
            [FeatureIds.Power] = (typeof(PowerOptimizationsViewModel), typeof(PowerOptimizationsView)),
            [FeatureIds.GamingPerformance] = (typeof(GamingandPerformanceOptimizationsViewModel), typeof(GamingandPerformanceOptimizationsView)),
            [FeatureIds.Update] = (typeof(UpdateOptimizationsViewModel), typeof(UpdateOptimizationsView)),
            [FeatureIds.Notifications] = (typeof(NotificationOptimizationsViewModel), typeof(NotificationOptimizationsView)),
            [FeatureIds.Sound] = (typeof(SoundOptimizationsViewModel), typeof(SoundOptimizationsView)),
            [FeatureIds.WindowsApps] = (typeof(WindowsAppsViewModel), typeof(WindowsAppsView)),
            [FeatureIds.ExternalApps] = (typeof(ExternalAppsViewModel), typeof(ExternalAppsView))
        };

        public static readonly List<FeatureMetadata> AllFeatures = FeatureDefinitions.All
            .Where(def => TypeMap.ContainsKey(def.Id))
            .Select(def =>
            {
                var (vmType, viewType) = TypeMap[def.Id];
                var iconEnum = Enum.TryParse<PackIconMaterialKind>(def.IconName, out var icon) ? icon : PackIconMaterialKind.Cog;
                var locKey = GetLocalizationKey(def.Id);

                return new FeatureMetadata(
                    def.Id,
                    locKey,
                    iconEnum,
                    vmType,
                    viewType,
                    def.Category,
                    def.SortOrder
                );
            })
            .ToList();

        private static string GetLocalizationKey(string featureId)
        {
            return featureId switch
            {
                FeatureIds.Notifications => StringKeys.Features.Notifications_Name,
                FeatureIds.Power => StringKeys.Features.Power_Name,
                FeatureIds.Privacy => StringKeys.Features.Privacy_Name,
                FeatureIds.GamingPerformance => StringKeys.Features.GamingPerformance_Name,
                FeatureIds.Sound => StringKeys.Features.Sound_Name,
                FeatureIds.Update => StringKeys.Features.Update_Name,
                FeatureIds.WindowsTheme => StringKeys.Features.WindowsTheme_Name,
                FeatureIds.Taskbar => StringKeys.Features.Taskbar_Name,
                FeatureIds.StartMenu => StringKeys.Features.StartMenu_Name,
                FeatureIds.ExplorerCustomization => StringKeys.Features.Explorer_Name,
                // Fallback for apps if keys don't exist in StringKeys yet
                _ => featureId 
            };
        }

        public static IEnumerable<FeatureMetadata> GetFeaturesForCategory(string category) 
            => AllFeatures.Where(f => f.Category == category).OrderBy(f => f.SortOrder);

        public static FeatureMetadata? GetFeatureById(string id)
            => AllFeatures.FirstOrDefault(f => f.Id == id);

        public static PackIconMaterialKind GetIcon(string nameOrId)
        {
            if (string.IsNullOrEmpty(nameOrId)) return PackIconMaterialKind.Cog;

            // 1. Check Category Names
            if (nameOrId == "Software & Apps" || nameOrId == "SoftwareApps") return PackIconMaterialKind.PackageVariant;
            if (nameOrId == "Optimization Settings" || nameOrId == "Optimize") return PackIconMaterialKind.RocketLaunch;
            if (nameOrId == "Customization Settings" || nameOrId == "Customize") return PackIconMaterialKind.Palette;

            // 2. Check Features via Definition
            var featureById = FeatureDefinitions.Get(nameOrId);
            if (featureById != null)
            {
                return Enum.TryParse<PackIconMaterialKind>(featureById.IconName, out var icon) ? icon : PackIconMaterialKind.Cog;
            }

            // 3. Fallback for names (matching legacy behavior)
            var featureByDefaultName = FeatureDefinitions.All.FirstOrDefault(f => f.DefaultName == nameOrId);
            if (featureByDefaultName != null)
            {
                return Enum.TryParse<PackIconMaterialKind>(featureByDefaultName.IconName, out var icon) ? icon : PackIconMaterialKind.Cog;
            }

            return PackIconMaterialKind.Cog;
        }
    }
}
