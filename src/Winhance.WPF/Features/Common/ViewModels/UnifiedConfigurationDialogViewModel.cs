using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

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

            // Create section view models
            foreach (var section in sections)
            {
                Sections.Add(new UnifiedConfigurationSectionViewModel
                {
                    Name = GetSectionDisplayName(section.Key),
                    Description = GetSectionDescription(section.Key),
                    IsSelected = section.Value.IsSelected,
                    IsAvailable = section.Value.IsAvailable,
                    ItemCount = section.Value.ItemCount,
                    SectionKey = section.Key
                });
            }

            // Commands will be set by the dialog
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
                result[section.SectionKey] = section.IsSelected;
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
            set => SetProperty(ref _isSelected, value);
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
    }
}