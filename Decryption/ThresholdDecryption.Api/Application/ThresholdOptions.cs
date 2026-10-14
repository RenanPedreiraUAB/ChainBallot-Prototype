namespace ThresholdDecryption.Api.Application;

public sealed class ThresholdOptions
{
    public const string SectionName = "ThresholdDecryption";

    public int DefaultRequiredShares { get; set; } = 3;

    public int DefaultRequiredApprovals { get; set; } = 2;

    public string[] AllowedTrustees { get; set; } = ["trustee-a", "trustee-b", "trustee-c", "trustee-d", "trustee-e"];

    public string[] GovernanceApprovers { get; set; } = ["governor-a", "governor-b", "governor-c"];

    public string StorePath { get; set; } = string.Empty;

    public string OperatorAccessHeaderName { get; set; } = "X-Operator-Key";

    public string OperatorAccessKey { get; set; } = "decryption-operator-key";

    /// <summary>Base URL of the Transparency component, used to read the canonical election state precondition (Architecture.md § 4.3).</summary>
    public string TransparencyBaseUrl { get; set; } = "http://localhost:5264/";
}
