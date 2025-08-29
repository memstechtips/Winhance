using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Customize.ViewModels
{
    public partial class CustomizeViewModel(
        IServiceProvider serviceProvider,
        ISearchTextCoordinationService searchTextCoordinationService)
        : BaseCategoryViewModel(serviceProvider, searchTextCoordinationService)
    {
        protected override string CategoryName => "Customize";
        protected override string DefaultStatusText => "Customize Your Windows Appearance and Behaviour";
    }
}