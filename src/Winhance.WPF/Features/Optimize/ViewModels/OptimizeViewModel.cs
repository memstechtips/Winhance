using Winhance.Core.Features.Common.Interfaces;
using Winhance.WPF.Features.Common.ViewModels;

namespace Winhance.WPF.Features.Optimize.ViewModels
{
    public partial class OptimizeViewModel(
        IServiceProvider serviceProvider,
        ISearchTextCoordinationService searchTextCoordinationService)
        : BaseCategoryViewModel(serviceProvider, searchTextCoordinationService)
    {
        protected override string CategoryName => "Optimize";
        protected override string DefaultStatusText => "Optimize Your Windows Settings and Performance";
    }
}