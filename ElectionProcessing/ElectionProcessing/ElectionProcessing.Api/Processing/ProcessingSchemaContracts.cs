namespace ElectionProcessing.Api.Processing;

/// <summary>
/// Frozen schema/version constants for outbound processing artifacts.
/// These values are contract versions, not runtime counters.
/// </summary>
public static class ProcessingSchemaContracts
{
    public const int BatchRecordSchemaVersion = 1;

    public const int TallyRecordSchemaVersion = 1;

    public const int ProofArtifactSchemaVersion = 1;
}
