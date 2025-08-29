using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Winhance.WPF.Features.Common.ViewModels
{
    /// <summary>
    /// ViewModel for the UnifiedConfigurationDialog.
    /// </summary>
    public class UnifiedConfigurationDialogViewModel : ObservableObject
    {
        private string _title;
        private string _description;
        private bool _isSaveDialog;

        /// <summary>
        /// Gets or sets the title of the dialog.
        /// </summary>
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <summary>
        /// Gets or sets the description of the dialog.
        /// </summary>
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        /// <summary>
        /// Gets a value indicating whether this is a save dialog.
        /// </summary>
        public bool IsSaveDialog => _isSaveDialog;

        /// <summary>
        /// Gets the collection of configuration sections.
        /// </summary>
        public ObservableCollection<UnifiedConfigurationSectionViewModel> Sections { get; } = new ObservableCollection<UnifiedConfigurationSectionViewModel>();

        /// <summary>
        /// Gets or sets the command to confirm the selection.
        /// </summary>
        public ICommand OkCommand { get; set; }

        /// <summary>
        /// Gets or sets the command to cancel the selection.
        /// </summary>
        public ICommand CancelCommand { get; set; }


        /// <summary>
        /// Initializes a new instance of the <see cref="UnifiedConfigurationDialogViewModel"/> class.
        /// </summary>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="description">The description of the dialog.</param>
        /// <param name="sections">The dictionary of section names, their availability, and item counts.</param>
        /// <param name="isSaveDialog">Whether this is a save dialog (true) or an import dialog (false).</param>
        public UnifiedConfigurationDialogViewModel(
            string title,
            string description,
            Dictionary<string, (bool IsSelected, bool IsAvailable, int ItemCount)> sections,
            bool isSaveDialog)
        {
            Title = title;
            Description = description;
            _isSaveDialog = isSaveDialog;

            // Create parent section for Windows Apps and External Apps
            var softwareAndAppsSection = new UnifiedConfigurationSectionViewModel
            {
                Name = "Software & Apps",
                Description = "Settings for Windows built-in and third-party applications",
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
                softwareAndAppsSection.SubSections.Add(new UnifiedConfigurationSectionViewModel
                {
                    Name = GetSectionDisplayName("WindowsApps"),
                    Description = GetSectionDescription("WindowsApps"),
                    IsSelected = sections["WindowsApps"].IsSelected,
                    IsAvailable = sections["WindowsApps"].IsAvailable,
                    ItemCount = sections["WindowsApps"].ItemCount,
                    SectionKey = "WindowsApps"
                });
            }

            // Add External Apps subsection if available
            if (sections.ContainsKey("ExternalApps"))
            {
                softwareAndAppsSection.SubSections.Add(new UnifiedConfigurationSectionViewModel
                {
                    Name = GetSectionDisplayName("ExternalApps"),
                    Description = GetSectionDescription("ExternalApps"),
                    IsSelected = sections["ExternalApps"].IsSelected,
                    IsAvailable = sections["ExternalApps"].IsAvailable,
                    ItemCount = sections["ExternalApps"].ItemCount,
                    SectionKey = "ExternalApps"
                });
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

                // Add optimization subsections
                var optimizationSubsections = new[]
                {
                    ("GamingPerformance", "Gaming and Performance"),
                    ("PowerSettings", "Power Settings"),
                    ("WindowsSecurity", "Windows Security Settings"),
                    ("PrivacySettings", "Privacy Settings"),
                    ("WindowsUpdates", "Windows Updates"),
                    ("Explorer", "Explorer"),
                    ("Notifications", "Notifications"),
                    ("Sound", "Sound")
                };

                foreach (var (key, name) in optimizationSubsections)
                {
                    optimizeSection.SubSections.Add(new UnifiedConfigurationSectionViewModel
                    {
                        Name = name,
                        Description = $"{name} optimization settings",
                        IsSelected = sections["Optimize"].IsSelected,
                        IsAvailable = sections["Optimize"].IsAvailable,
                        ItemCount = 0, // We don't have individual counts for subsections
                        SectionKey = $"Optimize_{key}"
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

                // Add customization subsections
                var customizationSubsections = new[]
                {
                    ("WindowsTheme", "Windows Theme"),
                    ("Taskbar", "Taskbar"),
                    ("StartMenu", "Start Menu"),
                    ("Explorer", "Explorer")
                };

                foreach (var (key, name) in customizationSubsections)
                {
                    customizeSection.SubSections.Add(new UnifiedConfigurationSectionViewModel
                    {
                        Name = name,
                        Description = $"{name} customization settings",
                        IsSelected = sections["Customize"].IsSelected,
                        IsAvailable = sections["Customize"].IsAvailable,
                        ItemCount = 0, // We don't have individual counts for subsections
                        SectionKey = $"Customize_{key}"
                    });
                }

                Sections.Add(customizeSection);
            }

            // Set up commands
            OkCommand = null;
            CancelCommand = null;
        }

        /// <summary>
        /// Gets the result of the dialog as a dictionary of section names and their selection state.
        /// </summary>
        /// <returns>A dictionary of section names and their selection state.</returns>
        public Dictionary<string, bool> GetResult()
        {
            var result = new Dictionary<string, bool>();

            foreach (var section in Sections)
            {
                // For parent sections with subsections, we need to get the individual subsection results
                if (section.HasSubSections)
                {
                    // Special handling for Software & Apps section
                    if (section.SectionKey == "SoftwareAndApps")
                    {
                        foreach (var subSection in section.SubSections)
                        {
                            result[subSection.SectionKey] = subSection.IsSelected;
                        }
                    }
                    // For Optimize and Customize sections, we need to map the subsection keys back to the main section
                    else
                    {
                        // Add the main section selection state
                        result[section.SectionKey] = section.IsSelected;

                        // Add subsection selection states with their specialized keys
                        foreach (var subSection in section.SubSections)
                        {
                            result[subSection.SectionKey] = subSection.IsSelected;
                        }
                    }
                }
                else
                {
                    // For sections without subsections, just add the section key and selection state
                    result[section.SectionKey] = section.IsSelected;
                }
            }

            return result;
        }


        private string GetSectionDisplayName(string sectionKey)
        {
            return sectionKey switch
            {
                "WindowsApps" => "Windows Apps",
                "ExternalApps" => "External Apps",
                "Customize" => "Customization Settings",
                "Optimize" => "Optimization Settings",
                _ => sectionKey
            };
        }

        private string GetSectionDescription(string sectionKey)
        {
            return sectionKey switch
            {
                "WindowsApps" => "Settings for Windows built-in applications",
                "ExternalApps" => "Settings for third-party applications",
                "Customize" => "Windows UI customization settings",
                "Optimize" => "Windows optimization settings",
                _ => string.Empty
            };
        }
    }

    /// <summary>
    /// ViewModel for a unified configuration section.
    /// </summary>
    public class UnifiedConfigurationSectionViewModel : ObservableObject
    {
        private string _name;
        private string _description;
        private bool _isSelected;
        private bool _isAvailable;
        private int _itemCount;
        private string _sectionKey;
        private bool _hasSubSections;

        /// <summary>
        /// Gets or sets the name of the section.
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        /// <summary>
        /// Gets or sets the description of the section.
        /// </summary>
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the section is selected.
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetProperty(ref _isSelected, value))
                {
                    // Update all subsections when parent is selected/deselected
                    if (HasSubSections)
                    {
                        foreach (var subSection in SubSections)
                        {
                            subSection.IsSelected = value;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the section is available.
        /// </summary>
        public bool IsAvailable
        {
            get => _isAvailable;
            set => SetProperty(ref _isAvailable, value);
        }

        /// <summary>
        /// Gets or sets the number of items in the section.
        /// </summary>
        public int ItemCount
        {
            get => _itemCount;
            set => SetProperty(ref _itemCount, value);
        }

        /// <summary>
        /// Gets or sets the section key.
        /// </summary>
        public string SectionKey
        {
            get => _sectionKey;
            set => SetProperty(ref _sectionKey, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether this section has subsections.
        /// </summary>
        public bool HasSubSections
        {
            get => _hasSubSections;
            set => SetProperty(ref _hasSubSections, value);
        }

        /// <summary>
        /// Gets the collection of subsections.
        /// </summary>
        public ObservableCollection<UnifiedConfigurationSectionViewModel> SubSections { get; } = new ObservableCollection<UnifiedConfigurationSectionViewModel>();

        /// <summary>
        /// Updates the IsSelected property based on the state of subsections.
        /// </summary>
        public void UpdateSelectionFromSubSections()
        {
            if (HasSubSections && SubSections.Any())
            {
                // If all subsections are selected, select the parent
                // If none are selected, deselect the parent
                // If some are selected, select the parent (partial selection)
                bool allSelected = SubSections.All(s => s.IsSelected);
                bool noneSelected = SubSections.All(s => !s.IsSelected);

                _isSelected = allSelected || (!noneSelected); // Don't trigger the setter to avoid recursion
                OnPropertyChanged(nameof(IsSelected));
            }
        }
    }
}