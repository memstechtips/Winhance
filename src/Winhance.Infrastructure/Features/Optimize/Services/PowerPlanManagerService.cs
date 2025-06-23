using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Services;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Core.Features.Optimize.Services;

namespace Winhance.Infrastructure.Features.Optimize.Services
{
    /// <summary>
    /// Service for managing power plans.
    /// </summary>
    public class PowerPlanManagerService : IPowerPlanManagerService
    {
        private readonly ICommandService _commandService;
        private string? _cachedActivePowerPlanGuid;

        /// <summary>
        /// Initializes a new instance of the <see cref="PowerPlanManagerService"/> class.
        /// </summary>
        /// <param name="commandService">The command service.</param>
        public PowerPlanManagerService(ICommandService commandService)
        {
            _commandService = commandService;
        }

        /// <summary>
        /// Gets all available power plans.
        /// </summary>
        /// <returns>A list of power plans.</returns>
        public async Task<List<PowerPlan>> GetPowerPlansAsync()
        {
            var result = await _commandService.ExecuteCommandAsync("powercfg /list");
            return ParsePowerPlans(result.Output);
        }

        /// <summary>
        /// Gets the active power plan.
        /// </summary>
        /// <returns>The active power plan, or null if not found.</returns>
        public async Task<PowerPlan?> GetActivePowerPlanAsync()
        {
            var result = await _commandService.ExecuteCommandAsync("powercfg /getactivescheme");
            var powerPlans = ParsePowerPlans(result.Output);
            return powerPlans.FirstOrDefault();
        }

        /// <summary>
        /// Gets the active power plan GUID.
        /// </summary>
        /// <returns>The active power plan GUID, or null if not found.</returns>
        public async Task<string> GetActivePowerPlanGuidAsync()
        {
            if (!string.IsNullOrEmpty(_cachedActivePowerPlanGuid))
            {
                return _cachedActivePowerPlanGuid;
            }

            var activePlan = await GetActivePowerPlanAsync();
            _cachedActivePowerPlanGuid = activePlan?.Guid ?? string.Empty;
            return _cachedActivePowerPlanGuid;
        }

        /// <summary>
        /// Sets the active power plan.
        /// </summary>
        /// <param name="powerPlanGuid">The power plan GUID to set as active.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public async Task<bool> SetActivePowerPlanAsync(string powerPlanGuid)
        {
            var result = await _commandService.ExecuteCommandAsync($"powercfg /setactive {powerPlanGuid}");
            bool success = result.Success && !result.Output.Contains("Error");
            
            if (success)
            {
                _cachedActivePowerPlanGuid = powerPlanGuid;
            }
            
            return success;
        }

        /// <summary>
        /// Creates a new power plan.
        /// </summary>
        /// <param name="name">The name of the new power plan.</param>
        /// <param name="sourceGuid">The source power plan GUID to duplicate from.</param>
        /// <returns>The GUID of the new power plan, or null if creation failed.</returns>
        public async Task<string?> CreatePowerPlanAsync(string name, string sourceGuid)
        {
            var result = await _commandService.ExecuteCommandAsync($"powercfg /duplicatescheme {sourceGuid} {name}");
            
            if (!result.Success)
                return null;
                
            // Extract the new GUID from the output
            var match = Regex.Match(result.Output, @"([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})");
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>
        /// Deletes a power plan.
        /// </summary>
        /// <param name="powerPlanGuid">The power plan GUID to delete.</param>
        /// <returns>True if successful, false otherwise.</returns>
        public async Task<bool> DeletePowerPlanAsync(string powerPlanGuid)
        {
            var result = await _commandService.ExecuteCommandAsync($"powercfg /delete {powerPlanGuid}");
            return result.Success && !result.Output.Contains("Error");
        }

        /// <summary>
        /// Parses power plans from powercfg output.
        /// </summary>
        /// <param name="output">The powercfg output.</param>
        /// <returns>A list of power plans.</returns>
        private List<PowerPlan> ParsePowerPlans(string output)
        {
            var powerPlans = new List<PowerPlan>();
            
            // Regular expression to match power plan GUIDs and names
            var regex = new Regex(@"([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})\s*\(([^)]*)\)");
            
            var matches = regex.Matches(output);
            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 3)
                {
                    string guid = match.Groups[1].Value;
                    string name = match.Groups[2].Value.Trim();
                    bool isActive = output.Contains($"*") && output.Contains(guid);
                    
                    powerPlans.Add(new PowerPlan
                    {
                        Guid = guid,
                        Name = name,
                        IsActive = isActive
                    });
                }
            }
            
            return powerPlans;
        }
    }
}
