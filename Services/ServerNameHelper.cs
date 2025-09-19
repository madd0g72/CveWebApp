namespace CveWebApp.Services
{
    /// <summary>
    /// Helper class for server name normalization and matching
    /// </summary>
    public static class ServerNameHelper
    {
        /// <summary>
        /// Normalizes a server name by extracting only the hostname portion, 
        /// removing domain suffixes for consistent matching
        /// </summary>
        /// <param name="serverName">The server name that may include domain suffix</param>
        /// <returns>The hostname portion only, or the original name if no domain suffix is present</returns>
        public static string NormalizeServerName(string? serverName)
        {
            if (string.IsNullOrWhiteSpace(serverName))
                return string.Empty;

            // Remove domain suffix by taking everything before the first dot
            var dotIndex = serverName.IndexOf('.');
            return dotIndex > 0 ? serverName.Substring(0, dotIndex) : serverName;
        }

        /// <summary>
        /// Checks if two server names match by comparing their normalized (hostname-only) portions
        /// </summary>
        /// <param name="serverName1">First server name to compare</param>
        /// <param name="serverName2">Second server name to compare</param>
        /// <returns>True if the normalized names match (case-insensitive), false otherwise</returns>
        public static bool DoServerNamesMatch(string? serverName1, string? serverName2)
        {
            var normalized1 = NormalizeServerName(serverName1);
            var normalized2 = NormalizeServerName(serverName2);
            
            return string.Equals(normalized1, normalized2, StringComparison.OrdinalIgnoreCase);
        }
    }
}