using System.Collections.Generic;
using System.Linq;
using Winhance.WPF.Features.SoftwareApps.ViewModels;

namespace Winhance.WPF.Features.SoftwareApps.Models
{
    public class SelectedItemsCollection
    {
        public List<AppItemViewModel> Apps { get; set; } = new();
        public List<AppItemViewModel> Capabilities { get; set; } = new();
        public List<AppItemViewModel> Features { get; set; } = new();

        public bool HasItems => TotalCount > 0;
        public int TotalCount => Apps.Count + Capabilities.Count + Features.Count;
        public IEnumerable<AppItemViewModel> AllItems => Apps.Concat(Capabilities).Concat(Features);
        public IEnumerable<string> AllNames => AllItems.Select(i => i.Name);
    }
}