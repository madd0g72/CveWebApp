using System.Text.Json.Serialization;

namespace CveWebApp.Models
{
    /// <summary>
    /// Root model for Microsoft CVRF/MSRC JSON data deserialization
    /// Based on the Common Vulnerability Reporting Framework (CVRF) specification
    /// </summary>
    public class CvrfMsrcModel
    {
        [JsonPropertyName("document_title")]
        public string? DocumentTitle { get; set; }

        [JsonPropertyName("document_type")]
        public string? DocumentType { get; set; }

        [JsonPropertyName("document_publisher")]
        public DocumentPublisher? DocumentPublisher { get; set; }

        [JsonPropertyName("document_tracking")]
        public DocumentTracking? DocumentTracking { get; set; }

        [JsonPropertyName("document_notes")]
        public List<DocumentNote> DocumentNotes { get; set; } = new List<DocumentNote>();

        [JsonPropertyName("document_references")]
        public List<DocumentReference> DocumentReferences { get; set; } = new List<DocumentReference>();

        [JsonPropertyName("product_tree")]
        public ProductTree? ProductTree { get; set; }

        [JsonPropertyName("vulnerability")]
        public List<Vulnerability> Vulnerabilities { get; set; } = new List<Vulnerability>();
    }

    public class DocumentPublisher
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("vendor_id")]
        public string? VendorId { get; set; }

        [JsonPropertyName("contact_details")]
        public string? ContactDetails { get; set; }

        [JsonPropertyName("issuing_authority")]
        public string? IssuingAuthority { get; set; }
    }

    public class DocumentTracking
    {
        [JsonPropertyName("identification")]
        public TrackingIdentification? Identification { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("revision_history")]
        public List<RevisionHistory> RevisionHistory { get; set; } = new List<RevisionHistory>();

        [JsonPropertyName("initial_release_date")]
        public DateTime? InitialReleaseDate { get; set; }

        [JsonPropertyName("current_release_date")]
        public DateTime? CurrentReleaseDate { get; set; }

        [JsonPropertyName("generator")]
        public Generator? Generator { get; set; }
    }

    public class TrackingIdentification
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("alias")]
        public List<string> Aliases { get; set; } = new List<string>();
    }

    public class RevisionHistory
    {
        [JsonPropertyName("number")]
        public string? Number { get; set; }

        [JsonPropertyName("date")]
        public DateTime? Date { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    public class Generator
    {
        [JsonPropertyName("engine")]
        public string? Engine { get; set; }

        [JsonPropertyName("date")]
        public DateTime? Date { get; set; }
    }

    public class DocumentNote
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("ordinal")]
        public string? Ordinal { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("audience")]
        public string? Audience { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    public class DocumentReference
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    public class ProductTree
    {
        [JsonPropertyName("branch")]
        public List<Branch> Branches { get; set; } = new List<Branch>();

        [JsonPropertyName("full_product_name")]
        public List<FullProductName> FullProductNames { get; set; } = new List<FullProductName>();

        [JsonPropertyName("relationship")]
        public List<Relationship> Relationships { get; set; } = new List<Relationship>();

        [JsonPropertyName("product_groups")]
        public List<ProductGroup> ProductGroups { get; set; } = new List<ProductGroup>();
    }

    public class Branch
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("full_product_name")]
        public List<FullProductName> FullProductNames { get; set; } = new List<FullProductName>();

        [JsonPropertyName("branch")]
        public List<Branch> Branches { get; set; } = new List<Branch>();
    }

    public class FullProductName
    {
        [JsonPropertyName("product_id")]
        public string? ProductId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("cpe")]
        public string? Cpe { get; set; }
    }

    public class Relationship
    {
        [JsonPropertyName("product_reference")]
        public string? ProductReference { get; set; }

        [JsonPropertyName("relation_type")]
        public string? RelationType { get; set; }

        [JsonPropertyName("relates_to_product_reference")]
        public string? RelatesToProductReference { get; set; }

        [JsonPropertyName("full_product_name")]
        public FullProductName? FullProductName { get; set; }
    }

    public class ProductGroup
    {
        [JsonPropertyName("group_id")]
        public string? GroupId { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("product_ids")]
        public List<string> ProductIds { get; set; } = new List<string>();
    }

    public class Vulnerability
    {
        [JsonPropertyName("ordinal")]
        public string? Ordinal { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("notes")]
        public List<VulnerabilityNote> Notes { get; set; } = new List<VulnerabilityNote>();

        [JsonPropertyName("discovery_date")]
        public DateTime? DiscoveryDate { get; set; }

        [JsonPropertyName("release_date")]
        public DateTime? ReleaseDate { get; set; }

        [JsonPropertyName("involvements")]
        public List<Involvement> Involvements { get; set; } = new List<Involvement>();

        [JsonPropertyName("cve")]
        public string? Cve { get; set; }

        [JsonPropertyName("cwe")]
        public CweInfo? Cwe { get; set; }

        [JsonPropertyName("product_status")]
        public ProductStatus? ProductStatus { get; set; }

        [JsonPropertyName("threats")]
        public List<Threat> Threats { get; set; } = new List<Threat>();

        [JsonPropertyName("cvss_score_sets")]
        public List<CvssScoreSet> CvssScoreSets { get; set; } = new List<CvssScoreSet>();

        [JsonPropertyName("remediations")]
        public List<Remediation> Remediations { get; set; } = new List<Remediation>();

        [JsonPropertyName("references")]
        public List<VulnerabilityReference> References { get; set; } = new List<VulnerabilityReference>();

        [JsonPropertyName("acknowledgments")]
        public List<Acknowledgment> Acknowledgments { get; set; } = new List<Acknowledgment>();
    }

    public class VulnerabilityNote
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("ordinal")]
        public string? Ordinal { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("audience")]
        public string? Audience { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    public class Involvement
    {
        [JsonPropertyName("party")]
        public string? Party { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    public class CweInfo
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    public class ProductStatus
    {
        [JsonPropertyName("first_affected")]
        public List<string> FirstAffected { get; set; } = new List<string>();

        [JsonPropertyName("known_affected")]
        public List<string> KnownAffected { get; set; } = new List<string>();

        [JsonPropertyName("known_not_affected")]
        public List<string> KnownNotAffected { get; set; } = new List<string>();

        [JsonPropertyName("first_fixed")]
        public List<string> FirstFixed { get; set; } = new List<string>();

        [JsonPropertyName("recommended")]
        public List<string> Recommended { get; set; } = new List<string>();

        [JsonPropertyName("last_affected")]
        public List<string> LastAffected { get; set; } = new List<string>();
    }

    public class Threat
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("date")]
        public DateTime? Date { get; set; }

        [JsonPropertyName("product_ids")]
        public List<string> ProductIds { get; set; } = new List<string>();

        [JsonPropertyName("group_ids")]
        public List<string> GroupIds { get; set; } = new List<string>();
    }

    public class CvssScoreSet
    {
        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("base_score")]
        public decimal? BaseScore { get; set; }

        [JsonPropertyName("temporal_score")]
        public decimal? TemporalScore { get; set; }

        [JsonPropertyName("environmental_score")]
        public decimal? EnvironmentalScore { get; set; }

        [JsonPropertyName("vector")]
        public string? Vector { get; set; }

        [JsonPropertyName("product_ids")]
        public List<string> ProductIds { get; set; } = new List<string>();
    }

    public class Remediation
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("date")]
        public DateTime? Date { get; set; }

        [JsonPropertyName("entitlement")]
        public List<string> Entitlements { get; set; } = new List<string>();

        [JsonPropertyName("restart_required")]
        public RemediationRestartRequired? RestartRequired { get; set; }

        [JsonPropertyName("sub_type")]
        public string? SubType { get; set; }

        [JsonPropertyName("supercedence")]
        public string? Supercedence { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("product_ids")]
        public List<string> ProductIds { get; set; } = new List<string>();

        [JsonPropertyName("group_ids")]
        public List<string> GroupIds { get; set; } = new List<string>();
    }

    public class RemediationRestartRequired
    {
        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("details")]
        public string? Details { get; set; }
    }

    public class VulnerabilityReference
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    public class Acknowledgment
    {
        [JsonPropertyName("name")]
        public List<string> Names { get; set; } = new List<string>();

        [JsonPropertyName("organization")]
        public List<string> Organizations { get; set; } = new List<string>();

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("url")]
        public List<string> Urls { get; set; } = new List<string>();
    }
}