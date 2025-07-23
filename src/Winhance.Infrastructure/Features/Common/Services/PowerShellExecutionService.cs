using System;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Threading;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Infrastructure.Features.Common.Services
{
    /// <summary>
    /// Service that executes PowerShell scripts with progress reporting and cancellation support.
    /// </summary>
    public class PowerShellExecutionService : IPowerShellExecutionService, IDisposable
    {
        private readonly ILogService _logService;
        private readonly ISystemServices _systemServices;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="PowerShellExecutionService"/> class.
        /// </summary>
        /// <param name="logService">The log service.</param>
        /// <param name="systemServices">The system services.</param>
        public PowerShellExecutionService(ILogService logService, ISystemServices systemServices)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _systemServices = systemServices ?? throw new ArgumentNullException(nameof(systemServices));
        }
        
        /// <inheritdoc/>
        public async Task<string> ExecuteScriptAsync(
            string script, 
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(script))
            {
                throw new ArgumentException("Script cannot be null or empty.", nameof(script));
            }
            
            using var powerShell = Utilities.PowerShellFactory.CreateWindowsPowerShell(_logService, _systemServices);
            // No need to set execution policy as it's already done in the factory
            
            powerShell.AddScript(script);

            // Set up stream handlers
            // Explicitly type progressAdapter using full namespace
            System.IProgress<PowerShellProgressData>? progressAdapter = progress != null
                ? new System.Progress<PowerShellProgressData>(data => MapProgressData(data, progress)) // Also qualify Progress<T>
                : null;

            SetupStreamHandlers(powerShell, progressAdapter);

            // Execute PowerShell with cancellation support
            return await Task.Run(() => {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // Register cancellation callback to stop PowerShell execution
                    using var cancellationRegistration = cancellationToken.Register(() => {
                        try {
                            _logService.LogInformation("Cancellation requested - stopping PowerShell execution");
                            powerShell.Stop();
                        } catch (Exception ex) {
                            _logService.LogWarning($"Error stopping PowerShell execution: {ex.Message}");
                        }
                    });
                    
                    var invokeResult = powerShell.Invoke();
                    var resultText = string.Join(Environment.NewLine, 
                        invokeResult.Select(item => item.ToString()));
                    
                    // Check for errors
                    if (powerShell.HadErrors)
                    {
                        foreach (var error in powerShell.Streams.Error)
                        {
                            _logService.LogError($"PowerShell error: {error.Exception?.Message ?? error.ToString()}", error.Exception);
                            
                            // This call seems to be the source of CS1061, despite Progress<T> having Report.
                            // Let's ensure the object creation is correct.
                            progressAdapter?.Report(new PowerShellProgressData
                            {
                                Message = error.Exception?.Message ?? error.ToString(),
                                StreamType = Winhance.Core.Features.Common.Enums.PowerShellStreamType.Error
                            });
                        }
                    }
                    
                    return resultText;
                }
                catch (Exception ex) when (cancellationToken.IsCancellationRequested)
                {
                    _logService.LogWarning($"PowerShell execution cancelled: {ex.Message}");
                    throw new OperationCanceledException("PowerShell execution was cancelled.", ex, cancellationToken);
                }
            }, cancellationToken);
        }
        
        /// <inheritdoc/>
        public async Task<string> ExecuteScriptFileAsync(
            string scriptPath, 
            string arguments = "",
            IProgress<TaskProgressDetail>? progress = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(scriptPath))
            {
                throw new ArgumentException("Script path cannot be null or empty.", nameof(scriptPath));
            }
            
            if (!File.Exists(scriptPath))
            {
                throw new FileNotFoundException($"PowerShell script file not found: {scriptPath}");
            }
            
            string script = await File.ReadAllTextAsync(scriptPath, cancellationToken);
            
            // If we have arguments, add them as parameters
            if (!string.IsNullOrEmpty(arguments))
            {
                script = $"{script} {arguments}";
            }
            
            return await ExecuteScriptAsync(script, progress, cancellationToken);
        }
        
        private void SetupStreamHandlers(PowerShell powerShell, IProgress<PowerShellProgressData>? progress)
        {
            if (progress == null) return;
            
            // Handle progress records
            powerShell.Streams.Progress.DataAdded += (sender, e) => {
                var progressRecord = ((PSDataCollection<ProgressRecord>)sender)[e.Index];
                progress.Report(new PowerShellProgressData
                {
                    PercentComplete = progressRecord.PercentComplete >= 0 ? progressRecord.PercentComplete : null,
                    Activity = progressRecord.Activity,
                    StatusDescription = progressRecord.StatusDescription,
                    CurrentOperation = progressRecord.CurrentOperation,
                    StreamType = Winhance.Core.Features.Common.Enums.PowerShellStreamType.Progress
                });

                _logService?.LogInformation($"PowerShell Progress: {progressRecord.Activity} - {progressRecord.StatusDescription} ({progressRecord.PercentComplete}%)");
            };

            // Handle information stream
            powerShell.Streams.Information.DataAdded += (sender, e) => {
                var info = ((PSDataCollection<InformationRecord>)sender)[e.Index];
                progress.Report(new PowerShellProgressData
                {
                    Message = info.MessageData.ToString(),
                    StreamType = Winhance.Core.Features.Common.Enums.PowerShellStreamType.Information
                });

                _logService?.LogInformation($"PowerShell Info: {info.MessageData}");
            };

            // Handle verbose stream
            powerShell.Streams.Verbose.DataAdded += (sender, e) => {
                var verbose = ((PSDataCollection<VerboseRecord>)sender)[e.Index];
                progress.Report(new PowerShellProgressData
                {
                    Message = verbose.Message,
                    StreamType = Winhance.Core.Features.Common.Enums.PowerShellStreamType.Verbose
                });

                _logService?.Log(LogLevel.Debug, $"PowerShell Verbose: {verbose.Message}");
            };

            // Handle warning stream
            powerShell.Streams.Warning.DataAdded += (sender, e) => {
                var warning = ((PSDataCollection<WarningRecord>)sender)[e.Index];
                progress.Report(new PowerShellProgressData
                {
                    Message = warning.Message,
                    StreamType = Winhance.Core.Features.Common.Enums.PowerShellStreamType.Warning
                });

                _logService?.LogWarning($"PowerShell Warning: {warning.Message}");
            };

            // Handle error stream
            powerShell.Streams.Error.DataAdded += (sender, e) => {
                var error = ((PSDataCollection<ErrorRecord>)sender)[e.Index];
                progress.Report(new PowerShellProgressData
                {
                    Message = error.Exception?.Message ?? error.ToString(),
                    StreamType = Winhance.Core.Features.Common.Enums.PowerShellStreamType.Error
                });

                _logService?.Log(LogLevel.Error, $"PowerShell Error: {error.Exception?.Message ?? error.ToString()}");
            };

            // Handle debug stream
            powerShell.Streams.Debug.DataAdded += (sender, e) => {
                var debug = ((PSDataCollection<DebugRecord>)sender)[e.Index];
                progress.Report(new PowerShellProgressData
                {
                    Message = debug.Message,
                    StreamType = Winhance.Core.Features.Common.Enums.PowerShellStreamType.Debug
                });

                _logService?.Log(LogLevel.Debug, $"PowerShell Debug: {debug.Message}");
            };
        }
        
        private void MapProgressData(PowerShellProgressData source, IProgress<TaskProgressDetail> target)
        {
            var detail = new TaskProgressDetail();
            
            // Map PowerShell progress data to task progress detail
            if (source.PercentComplete.HasValue)
            {
                detail.Progress = source.PercentComplete.Value;
            }
            
            if (!string.IsNullOrEmpty(source.Activity))
            {
                detail.StatusText = source.Activity;
                if (!string.IsNullOrEmpty(source.StatusDescription))
                {
                    detail.StatusText += $": {source.StatusDescription}";
                }
            }
            
            detail.DetailedMessage = source.Message ?? source.CurrentOperation;
            // Map stream type to log level
            switch (source.StreamType)
            {
                case Winhance.Core.Features.Common.Enums.PowerShellStreamType.Error:
                    detail.LogLevel = LogLevel.Error;
                    break;
                case Winhance.Core.Features.Common.Enums.PowerShellStreamType.Warning:
                    detail.LogLevel = LogLevel.Warning;
                    break;
                case Winhance.Core.Features.Common.Enums.PowerShellStreamType.Verbose:
                case Winhance.Core.Features.Common.Enums.PowerShellStreamType.Debug:
                    detail.LogLevel = LogLevel.Debug;
                    break;
                default: // Includes Information and Progress
                    detail.LogLevel = LogLevel.Info;
                    break;
            }
            
            target.Report(detail);
        }
        
        // SetExecutionPolicy is now handled by PowerShellFactory
        
        /// <summary>
        /// Disposes resources used by the service.
        /// </summary>
        public void Dispose()
        {
            // Cleanup resources if needed
            GC.SuppressFinalize(this);
        }
    }
}
