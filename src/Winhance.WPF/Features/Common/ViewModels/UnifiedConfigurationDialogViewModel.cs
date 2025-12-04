using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.Constants;

namespace Winhance.WPF.Features.Common.ViewModels
{
    public class ImportActionOption : ObservableObject
    {
        private string _key;
        private string _label;
        private bool _isSelected;
        private bool _isRadioButton;
        private string _groupName;
        private UnifiedConfigurationSectionViewModel _parentSection;

        public string Key
        {
            get => _key;
            set => SetProperty(ref _key, value);
        }

        public string Label
        {
            get => _label;
            set => SetProperty(ref _label, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    if (value && RequiresParentSelection && _parentSection != null && !_parentSection.IsSelected)
                    {
                        _parentSection.IsSelected = true;
                    }
                }
            }
        }

        public bool IsRadioButton
        {
            get => _isRadioButton;
            set => SetProperty(ref _isRadioButton, value);
        }

        public string GroupName
        {
            get => _groupName;
            set => SetProperty(ref _groupName, value);
        }

        public bool RequiresParentSelection { get; set; }

        public void SetParentSection(UnifiedConfigurationSectionViewModel parent)
        {
            _parentSection = parent;
        }
    }

    public class UnifiedConfigurationDialogViewModel : ObservableObject
    {
        private readonly ILocalizationService _localization;
        private string _title;
        private string _description;
        private bool _isSaveDialog;

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public bool IsSaveDialog => _isSaveDialog;

        public ObservableCollection<UnifiedConfigurationSectionViewModel> Sections { get; } = new ObservableCollection<UnifiedConfigurationSectionViewModel>();

        public ICommand OkCommand { get; set; }

        public ICommand CancelCommand { get; set; }

        public UnifiedConfigurationDialogViewModel(
            string title,
            string description,
            Dictionary<string, (bool IsSelected, bool IsAvailable, int ItemCount)> sections,
            bool isSaveDialog,
            ILocalizationService localization)
        {
            _localization = localization;
            Title = title;
            Description = description;
            _isSaveDialog = isSaveDialog;

            // Create parent section for Windows Apps and External Apps
            var softwareAndAppsSection = new UnifiedConfigurationSectionViewModel
            {
                Name = _localization.GetString("Dialog_Unified_Section_SoftwareAndApps"),
                Description = _localization.GetString("Dialog_Unified_Description_SoftwareAndApps"),
                IsSelected = sections.ContainsKey("WindowsApps") && sections.ContainsKey("ExternalApps") &&
                            (sections["WindowsApps"].IsSelected || sections["ExternalApps"].IsSelected),
                IsAvailable = sections.ContainsKey("WindowsApps") && sections.ContainsKey("ExternalApps") &&
                              (sections["WindowsApps"].IsAvailable || sections["ExternalApps"].IsAvailable),
                ItemCount = (sections.ContainsKey("WindowsApps") ? sections["WindowsApps"].ItemCount : 0) +
                           (sections.ContainsKey("ExternalApps") ? sections["ExternalApps"].ItemCount : 0),
                SectionKey = "SoftwareAndApps",
                HasSubSections = true
            };

            // Add Windows Apps subsection if available
            if (sections.ContainsKey("WindowsApps"))
            {
                var windowsAppsSection = new UnifiedConfigurationSectionViewModel
                {
                    Name = GetSectionDisplayName("WindowsApps"),
                    Description = GetSectionDescription("WindowsApps"),
                    IsSelected = sections["WindowsApps"].IsSelected,
                    IsAvailable = sections["WindowsApps"].IsAvailable,
                    ItemCount = sections["WindowsApps"].ItemCount,
                    SectionKey = "WindowsApps",
                    HasActionOptions = sections["WindowsApps"].ItemCount > 0
                };

                if (windowsAppsSection.HasActionOptions)
                {
                    windowsAppsSection.ActionOptions.Add(new ImportActionOption
                    {
                        Key = "ProcessRemoval",
                        Label = _localization.GetString("Dialog_Unified_Option_ProcessRemoval"),
                        IsSelected = true,
                        IsRadioButton = true,
                        GroupName = "WindowsAppsAction"
                    });

                    windowsAppsSection.ActionOptions.Add(new ImportActionOption
                    {
                        Key = "ManualRemoval",
                        Label = _localization.GetString("Dialog_Unified_Option_ManualRemoval"),
                        IsSelected = false,
                        IsRadioButton = true,
                        GroupName = "WindowsAppsAction"
                    });
                }

                softwareAndAppsSection.SubSections.Add(windowsAppsSection);
            }

            // Add External Apps subsection if available
            if (sections.ContainsKey("ExternalApps"))
            {
                var externalAppsSection = new UnifiedConfigurationSectionViewModel
                {
                    Name = GetSectionDisplayName("ExternalApps"),
                    Description = GetSectionDescription("ExternalApps"),
                    IsSelected = sections["ExternalApps"].IsSelected,
                    IsAvailable = sections["ExternalApps"].IsAvailable,
                    ItemCount = sections["ExternalApps"].ItemCount,
                    SectionKey = "ExternalApps",
                    HasActionOptions = sections["ExternalApps"].ItemCount > 0
                };

                if (externalAppsSection.HasActionOptions)
                {
                    externalAppsSection.ActionOptions.Add(new ImportActionOption
                    {
                        Key = "ProcessInstallation",
                        Label = _localization.GetString("Dialog_Unified_Option_ProcessInstallation"),
                        IsSelected = true,
                        IsRadioButton = true,
                        GroupName = "ExternalAppsAction"
                    });

                    externalAppsSection.ActionOptions.Add(new ImportActionOption
                    {
                        Key = "ManualInstallation",
                        Label = _localization.GetString("Dialog_Unified_Option_ManualInstallation"),
                        IsSelected = false,
                        IsRadioButton = true,
                        GroupName = "ExternalAppsAction"
                    });
                }

                softwareAndAppsSection.SubSections.Add(externalAppsSection);
            }

            // Add the Software & Apps parent section
            if (softwareAndAppsSection.SubSections.Any())
            {
                Sections.Add(softwareAndAppsSection);
            }

            // Create Optimization Settings section with subsections
            if (sections.ContainsKey("Optimize"))
            {
                var optimizeSection = new UnifiedConfigurationSectionViewModel
                {
                    Name = GetSectionDisplayName("Optimize"),
                    Description = GetSectionDescription("Optimize"),
                    IsSelected = sections["Optimize"].IsSelected,
                    IsAvailable = sections["Optimize"].IsAvailable,
                    ItemCount = sections["Optimize"].ItemCount,
                    SectionKey = "Optimize",
                    HasSubSections = true
                };

                foreach (var featureId in FeatureIds.OptimizeFeatures)
                {
                    var displayName = GetLocalizedFeatureName(featureId);
                    optimizeSection.SubSections.Add(new UnifiedConfigurationSectionViewModel
                    {
                        Name = displayName,
                        Description = $"{displayName} optimization settings",
                        IsSelected = sections["Optimize"].IsSelected,
                        IsAvailable = sections["Optimize"].IsAvailable,
                        ItemCount = 0,
                        SectionKey = $"Optimize_{featureId}"
                    });
                }

                Sections.Add(optimizeSection);
            }

            // Create Customization Settings section with subsections
            if (sections.ContainsKey("Customize"))
            {
                var customizeSection = new UnifiedConfigurationSectionViewModel
                {
                    Name = GetSectionDisplayName("Customize"),
                    Description = GetSectionDescription("Customize"),
                    IsSelected = sections["Customize"].IsSelected,
                    IsAvailable = sections["Customize"].IsAvailable,
                    ItemCount = sections["Customize"].ItemCount,
                    SectionKey = "Customize",
                    HasSubSections = true
                };

                foreach (var featureId in FeatureIds.CustomizeFeatures)
                {
                    var displayName = GetLocalizedFeatureName(featureId);
                    var subSection = new UnifiedConfigurationSectionViewModel
                    {
                        Name = displayName,
                        Description = $"{displayName} customization settings",
                        IsSelected = sections["Customize"].IsSelected,
                        IsAvailable = sections["Customize"].IsAvailable,
                        ItemCount = 0,
                        SectionKey = $"Customize_{featureId}"
                    };

                    if (featureId == FeatureIds.WindowsTheme)
                    {
                        subSection.HasActionOptions = true;
                        var wallpaperOption = new ImportActionOption
                        {
                            Key = "ApplyWallpaper",
                            Label = _localization.GetString("Dialog_Unified_Option_ApplyWallpaper"),
                            IsSelected = true,
                            IsRadioButton = false,
                            RequiresParentSelection = true
                        };
                        wallpaperOption.SetParentSection(subSection);
                        subSection.ActionOptions.Add(wallpaperOption);
                    }
                    else if (featureId == FeatureIds.Taskbar)
                    {
                        subSection.HasActionOptions = true;
                        subSection.ActionOptions.Add(new ImportActionOption
                        {
                            Key = "ApplyCleanTaskbar",
                            Label = _localization.GetString("Dialog_Unified_Option_CleanTaskbar"),
                            IsSelected = true,
                            IsRadioButton = false
                        });
                    }
                    else if (featureId == FeatureIds.StartMenu)
                    {
                        subSection.HasActionOptions = true;
                        subSection.ActionOptions.Add(new ImportActionOption
                        {
                            Key = "ApplyCleanStartMenu",
                            Label = _localization.GetString("Dialog_Unified_Option_CleanStartMenu"),
                            IsSelected = true,
                            IsRadioButton = false
                        });
                    }

                    customizeSection.SubSections.Add(subSection);
                }

                Sections.Add(customizeSection);
            }

            foreach (var section in Sections)
            {
                if (section.HasSubSections)
                {
                    foreach (var subSection in section.SubSections)
                    {
                        subSection.PropertyChanged += (s, e) =>
                        {
                            if (e.PropertyName == nameof(UnifiedConfigurationSectionViewModel.IsSelected))
                            {
                                section.UpdateSelectionFromSubSections();
                            }
                        };
                    }
                }
            }

            OkCommand = null;
            CancelCommand = null;
        }

        public (Dictionary<string, bool> sections, ImportOptions options) GetResult()
        {
            var sections = new Dictionary<string, bool>();
            var options = new ImportOptions();

            foreach (var section in Sections)
            {
                if (section.HasSubSections)
                {
                    if (section.SectionKey == "SoftwareAndApps")
                    {
                        foreach (var subSection in section.SubSections)
                        {
                            sections[subSection.SectionKey] = subSection.IsSelected;

                            if (subSection.HasActionOptions)
                            {
                                foreach (var actionOption in subSection.ActionOptions.Where(a => a.IsSelected))
                                {
                                    switch (actionOption.Key)
                                    {
                                        case "ProcessRemoval":
                                            options.ProcessWindowsAppsRemoval = true;
                                            break;
                                        case "ProcessInstallation":
                                            options.ProcessExternalAppsInstallation = true;
                                            break;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        sections[section.SectionKey] = section.IsSelected;

                        foreach (var subSection in section.SubSections)
                        {
                            bool hasSelectedActionOptions = subSection.HasActionOptions &&
                                                            subSection.ActionOptions.Any(a => a.IsSelected);

                            if (!subSection.IsSelected && hasSelectedActionOptions)
                            {
                                options.ActionOnlySubsections.Add(subSection.SectionKey);
                            }

                            sections[subSection.SectionKey] = subSection.IsSelected || hasSelectedActionOptions;

                            if (subSection.HasActionOptions)
                            {
                                foreach (var actionOption in subSection.ActionOptions.Where(a => a.IsSelected))
                                {
                                    switch (actionOption.Key)
                                    {
                                        case "ApplyWallpaper":
                                            options.ApplyThemeWallpaper = true;
                                            break;
                                        case "ApplyCleanTaskbar":
                                            options.ApplyCleanTaskbar = true;
                                            break;
                                        case "ApplyCleanStartMenu":
                                            options.ApplyCleanStartMenu = true;
                                            break;
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    sections[section.SectionKey] = section.IsSelected;
                }
            }

            return (sections, options);
        }


        private string GetSectionDisplayName(string sectionKey)
        {
            return sectionKey switch
            {
                "WindowsApps" => _localization.GetString("Dialog_Unified_Section_WindowsApps"),
                "ExternalApps" => _localization.GetString("Dialog_Unified_Section_ExternalApps"),
                "Customize" => _localization.GetString("Dialog_Unified_Section_Customize"),
                "Optimize" => _localization.GetString("Dialog_Unified_Section_Optimize"),
                _ => sectionKey
            };
        }

        private string GetSectionDescription(string sectionKey)
        {
            return sectionKey switch
            {
                "WindowsApps" => _localization.GetString("Dialog_Unified_Description_WindowsApps"),
                "ExternalApps" => _localization.GetString("Dialog_Unified_Description_ExternalApps"),
                "Optimize" => _localization.GetString("Dialog_Unified_Description_Optimize"),
                "Customize" => _localization.GetString("Dialog_Unified_Description_Customize"),
                _ => string.Empty
            };
        }

        private string GetLocalizedFeatureName(string featureId)
        {
            var key = featureId switch
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
                _ => null
            };

            return key != null ? _localization.GetString(key) : FeatureIds.GetDisplayName(featureId);
        }
    }

    public class UnifiedConfigurationSectionViewModel : ObservableObject
    {
        private string _name;
        private string _description;
        private bool _isSelected;
        private bool _isAvailable;
        private int _itemCount;
        private string _sectionKey;
        private bool _hasSubSections;
        private bool _hasActionOptions;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    if (HasSubSections)
                    {
                        foreach (var subSection in SubSections)
                        {
                            subSection.IsSelected = value;
                        }
                    }

                    if (HasActionOptions)
                    {
                        foreach (var actionOption in ActionOptions)
                        {
                            actionOption.IsSelected = value;
                        }
                    }
                }
            }
        }

        public bool IsAvailable
        {
            get => _isAvailable;
            set => SetProperty(ref _isAvailable, value);
        }

        public int ItemCount
        {
            get => _itemCount;
            set => SetProperty(ref _itemCount, value);
        }

        public string SectionKey
        {
            get => _sectionKey;
            set => SetProperty(ref _sectionKey, value);
        }

        public bool HasSubSections
        {
            get => _hasSubSections;
            set => SetProperty(ref _hasSubSections, value);
        }

        public bool HasActionOptions
        {
            get => _hasActionOptions;
            set => SetProperty(ref _hasActionOptions, value);
        }

        public ObservableCollection<UnifiedConfigurationSectionViewModel> SubSections { get; } = new ObservableCollection<UnifiedConfigurationSectionViewModel>();

        public ObservableCollection<ImportActionOption> ActionOptions { get; } = new ObservableCollection<ImportActionOption>();

        public void UpdateSelectionFromSubSections()
        {
            if (HasSubSections && SubSections.Any())
            {
                bool noneSelected = SubSections.All(s => !s.IsSelected);
                _isSelected = !noneSelected;
                OnPropertyChanged(nameof(IsSelected));
            }
        }
    }
}