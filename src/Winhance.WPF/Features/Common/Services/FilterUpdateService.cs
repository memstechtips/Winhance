using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events.Features;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.WPF.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.ViewModels;
using Winhance.Core.Features.Common.Events;

namespace Winhance.WPF.Features.Common.Services
{
    public class FilterUpdateService : IFilterUpdateService
    {
        private readonly ICompatibleSettingsRegistry _registry;
        private readonly IServiceProvider _serviceProvider;
        private readonly IEventBus _eventBus;
        private readonly ILogService _logService;

        public FilterUpdateService(
            ICompatibleSettingsRegistry registry,
            IServiceProvider serviceProvider,
            IEventBus eventBus,
            ILogService logService)
        {
            _registry = registry;
            _serviceProvider = serviceProvider;
            _eventBus = eventBus;
            _logService = logService;
        }

        public async Task UpdateFeatureSettingsAsync(ISettingsFeatureViewModel feature)
        {
            try
            {
                var currentSettingIds = feature.Settings.Select(s => s.SettingId).ToHashSet();
                var newDefinitions = _registry.GetFilteredSettings(feature.ModuleId).ToList();
                var newSettingIds = newDefinitions.Select(s => s.Id).ToHashSet();

                var toRemove = feature.Settings.Where(s => !newSettingIds.Contains(s.SettingId)).ToList();
                var toAdd = newDefinitions.Where(d => !currentSettingIds.Contains(d.Id)).ToList();
                var toUpdate = feature.Settings.Where(s => newSettingIds.Contains(s.SettingId)).ToList();

                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    foreach (var setting in toRemove)
                    {
                        feature.Settings.Remove(setting);
                        (setting as IDisposable)?.Dispose();
                    }

                    UpdateCompatibilityMessages(toUpdate, newDefinitions);

                    if (toAdd.Any())
                    {
                        try
                        {
                            var newViewModels = await PrepareNewSettingsAsync(toAdd, feature);
                            InsertNewSettings(feature, newViewModels, newDefinitions);
                            _eventBus.Publish(new FeatureComposedEvent(feature.ModuleId, toAdd));

                            _logService.Log(LogLevel.Info,
                                $"Successfully added {newViewModels.Count} new settings to {feature.ModuleId}");
                        }
                        catch (Exception prepareEx)
                        {
                            _logService.Log(LogLevel.Warning,
                                $"Failed to add settings incrementally for {feature.ModuleId}, attempting full refresh: {prepareEx.Message}");

                            try
                            {
                                await feature.RefreshSettingsAsync();
                                _logService.Log(LogLevel.Info, $"Successfully refreshed {feature.ModuleId} after incremental add failed");
                            }
                            catch (Exception refreshEx)
                            {
                                _logService.Log(LogLevel.Error,
                                    $"Failed to refresh {feature.ModuleId} after incremental add failed: {refreshEx.Message}");
                                throw;
                            }
                        }
                    }
                }, System.Windows.Threading.DispatcherPriority.Normal);

                _logService.Log(LogLevel.Info,
                    $"Updated filter for {feature.ModuleId}: Removed {toRemove.Count}, Added {toAdd.Count}, Updated {toUpdate.Count}");
            }
            catch (Exception ex)
            {
                _logService.Log(LogLevel.Error,
                    $"Error updating filter for {feature.ModuleId}: {ex.Message}");
            }
        }

        private void UpdateCompatibilityMessages(
            List<SettingItemViewModel> settingsToUpdate,
            List<SettingDefinition> newDefinitions)
        {
            foreach (var setting in settingsToUpdate)
            {
                var definition = newDefinitions.FirstOrDefault(d => d.Id == setting.SettingId);
                if (definition?.CustomProperties?.TryGetValue(
                    Core.Features.Common.Constants.CustomPropertyKeys.VersionCompatibilityMessage,
                    out var compatMessage) == true && compatMessage is string messageText)
                {
                    setting.WarningText = messageText;
                }
                else
                {
                    setting.WarningText = null;
                }
            }
        }

        private async Task<List<SettingItemViewModel>> PrepareNewSettingsAsync(
            List<SettingDefinition> definitions,
            ISettingsFeatureViewModel parentFeature)
        {
            return await Task.Run(async () =>
            {
                var discoveryService = _serviceProvider.GetRequiredService<ISystemSettingsDiscoveryService>();
                var settingsLoadingService = _serviceProvider.GetRequiredService<ISettingsLoadingService>();
                var comboBoxSetupService = _serviceProvider.GetRequiredService<IComboBoxSetupService>();
                var comboBoxResolver = _serviceProvider.GetRequiredService<IComboBoxResolver>();

                var states = await discoveryService.GetSettingStatesAsync(definitions);

                foreach (var setting in definitions.Where(s => s.InputType == InputType.Selection))
                {
                    if (states.TryGetValue(setting.Id, out var state) && state.RawValues != null)
                    {
                        try
                        {
                            var resolvedValue = await comboBoxResolver.ResolveCurrentValueAsync(setting, state.RawValues);
                            state.CurrentValue = resolvedValue;
                        }
                        catch (Exception ex)
                        {
                            _logService.Log(LogLevel.Warning,
                                $"Failed to resolve combo box value for '{setting.Id}': {ex.Message}");
                        }
                    }
                }

                var viewModels = new List<SettingItemViewModel>();
                var comboBoxTasks = new Dictionary<string, Task>();

                foreach (var definition in definitions)
                {
                    var vm = await settingsLoadingService.CreateSettingViewModelAsync(definition, states, parentFeature);
                    viewModels.Add(vm);

                    if (definition.InputType == InputType.Selection)
                    {
                        var currentState = states.TryGetValue(definition.Id, out var state)
                            ? state
                            : new SettingStateResult();

                        comboBoxTasks[definition.Id] = vm.SetupComboBoxAsync(
                            definition,
                            currentState.CurrentValue,
                            comboBoxSetupService,
                            _logService,
                            currentState.RawValues);
                    }
                }

                if (comboBoxTasks.Any())
                {
                    await Task.WhenAll(comboBoxTasks.Values);
                }

                return viewModels;
            });
        }

        private void InsertNewSettings(
            ISettingsFeatureViewModel feature,
            List<SettingItemViewModel> newViewModels,
            List<SettingDefinition> allDefinitions)
        {
            var settingIdToIndex = allDefinitions
                .Select((def, idx) => new { def.Id, idx })
                .ToDictionary(x => x.Id, x => x.idx);

            foreach (var vm in newViewModels)
            {
                var targetIndex = settingIdToIndex[vm.SettingId];
                int insertAt = feature.Settings.Count;

                for (int i = 0; i < feature.Settings.Count; i++)
                {
                    var existingIndex = settingIdToIndex.TryGetValue(feature.Settings[i].SettingId, out var idx)
                        ? idx
                        : int.MaxValue;

                    if (existingIndex > targetIndex)
                    {
                        insertAt = i;
                        break;
                    }
                }

                feature.Settings.Insert(insertAt, vm);
            }
        }
    }
}
