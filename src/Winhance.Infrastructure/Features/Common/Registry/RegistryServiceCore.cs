using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;

namespace Winhance.Infrastructure.Features.Common.Registry
{
    /// <summary>
    /// Core implementation of the registry service.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public partial class RegistryService : IRegistryService, IRegistryEventPublisher
    {
        private readonly ILogService _logService;
        private readonly IEventBus _eventBus;
        private IRegistryEventPublisher _eventPublisher;

        /// <summary>
        /// Initializes a new instance of the <see cref="RegistryService"/> class.
        /// </summary>
        /// <param name="logService">The log service.</param>
        /// <param name="eventBus">The event bus.</param>
        public RegistryService(ILogService logService, IEventBus eventBus)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _eventPublisher = this; // Default to self for backward compatibility
        }

        /// <summary>
        /// Sets the event publisher for registry changes
        /// </summary>
        /// <param name="eventPublisher">The event publisher instance</param>
        public void SetEventPublisher(IRegistryEventPublisher eventPublisher)
        {
            _eventPublisher = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        }

        /// <summary>
        /// Checks if the current platform is Windows.
        /// </summary>
        /// <returns>True if the platform is Windows; otherwise, logs an error and returns false.</returns>
        private bool CheckWindowsPlatform()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logService.Log(LogLevel.Error, "Registry operations are only supported on Windows");
                return false;
            }
            return true;
        }
    }
}
