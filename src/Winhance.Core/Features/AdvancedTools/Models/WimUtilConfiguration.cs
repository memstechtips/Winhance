namespace Winhance.Core.Features.AdvancedTools.Models
{
    public class WimUtilConfiguration
    {
        public string IsoPath { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
        public string XmlPath { get; set; } = string.Empty;
        public string OutputIsoPath { get; set; } = string.Empty;
        public bool AddDrivers { get; set; }
        public string? DriverSourcePath { get; set; }
        public bool UseRecommendedDrivers { get; set; }
    }
}
