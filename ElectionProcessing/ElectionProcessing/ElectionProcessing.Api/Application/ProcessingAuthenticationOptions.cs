namespace ElectionProcessing.Api.Application;

/// <summary>
/// Header-token credentials protecting Processing's privileged operational routes (Architecture.md § 3.6):
/// pause/resume/retry-failed-batch require <see cref="ProcessingRoles.Operator"/>; direct ingest requires
/// <see cref="ProcessingRoles.Ingest"/>. Prototype-grade shared-secret auth, consistent with the
/// <c>X-Operator-Key</c> pattern already used by the Watcher and Threshold Decryption components.
/// </summary>
public sealed class ProcessingAuthenticationOptions
{
    public string HeaderName { get; set; } = "X-Processing-Auth";

    public List<ProcessingApiCredential> Credentials { get; set; } = [];
}

public sealed class ProcessingApiCredential
{
    public string Token { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public List<string> Roles { get; set; } = [];
}

/// <summary>Role names granted by a matched <see cref="ProcessingApiCredential"/>.</summary>
public static class ProcessingRoles
{
    public const string Operator = "processing.operator";

    public const string Ingest = "processing.ingest";
}
