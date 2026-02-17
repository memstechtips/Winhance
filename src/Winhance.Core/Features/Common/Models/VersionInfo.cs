using System;

namespace Winhance.Core.Features.Common.Models
{
    public class VersionInfo
    {
        public string Version { get; set; } = string.Empty;
        public DateTime ReleaseDate { get; set; }
        public string DownloadUrl { get; set; } = string.Empty;
        public bool IsUpdateAvailable { get; set; }
        public bool IsBeta { get; set; }

        public static VersionInfo FromTag(string tag)
        {
            // Parse version tag in format v25.05.02 or v25.05.02-beta
            if (string.IsNullOrEmpty(tag) || !tag.StartsWith("v"))
                return new VersionInfo();

            string versionString = tag.Substring(1); // Remove 'v' prefix

            // Check if it's a beta version and extract the base version
            bool isBeta = versionString.Contains("-beta");
            if (isBeta)
            {
                versionString = versionString.Split('-')[0]; // Get the part before -beta
            }

            string[] parts = versionString.Split('.');

            if (parts.Length != 3)
                return new VersionInfo();

            if (!int.TryParse(parts[0], out int year) ||
                !int.TryParse(parts[1], out int month) ||
                !int.TryParse(parts[2], out int day))
                return new VersionInfo();

            // Construct a date from the version components
            DateTime releaseDate;
            try
            {
                releaseDate = new DateTime(2000 + year, month, day);
            }
            catch
            {
                // Invalid date components
                return new VersionInfo();
            }

            return new VersionInfo
            {
                Version = tag, // Keep the original tag with -beta if present
                ReleaseDate = releaseDate,
                IsBeta = isBeta
            };
        }

        public bool IsNewerThan(VersionInfo other)
        {
            if (other == null)
                return true;

            return ReleaseDate > other.ReleaseDate;
        }

        public override string ToString()
        {
            return Version;
        }
    }
}
