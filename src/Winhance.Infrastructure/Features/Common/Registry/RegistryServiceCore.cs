using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Infrastructure.Features.Common.Registry
{
    /// <summary>
    /// Core implementation of the registry service.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public partial class RegistryService : IRegistryService
    {
        private readonly ILogService _logService;

        /// <summary>
        /// Initializes a new instance of the <see cref="RegistryService"/> class.
        /// </summary>
        /// <param name="logService">The log service.</param>
        public RegistryService(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
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
