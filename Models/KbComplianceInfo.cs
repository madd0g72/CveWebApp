namespace CveWebApp.Models
{
    /// <summary>
    /// Represents compliance information for a specific KB requirement
    /// </summary>
    public class KbComplianceInfo
    {
        public bool IsCompliant { get; set; }
        public string RequiredKb { get; set; } = string.Empty;
        public string? InstalledKb { get; set; }
        public bool IsSupersedence { get; set; }
        public List<string>? SupersedenceChain { get; set; }
    }

    /// <summary>
    /// Represents the result of a supersedence chain search
    /// </summary>
    public class SupersedenceChainResult
    {
        public string FinalKb { get; set; } = string.Empty;
        public List<string> Chain { get; set; } = new List<string>();
    }
}