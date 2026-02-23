using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class ConfigImportState : IConfigImportState
    {
        public bool IsActive { get; set; }
    }
}
