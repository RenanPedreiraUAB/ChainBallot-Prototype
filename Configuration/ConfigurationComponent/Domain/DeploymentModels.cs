namespace ConfigurationComponent.Domain;

/// <summary>
/// Represents Deployment Recommendation.
/// </summary>
public sealed record DeploymentRecommendation(
    int RecommendedConsumerNodes,
    int RecommendedKafkaPartitions,
    int RecommendedBatchSize,
    TimeSpan RecommendedSettlementInterval,
    decimal EstimatedBlockchainCostSol);

/// <summary>
/// Represents Deployment Result.
/// </summary>
public sealed record DeploymentResult(
    string ProgramId,
    string ElectionPda,
    string MetadataUri,
    string TransactionSignature,
    string MetadataHash);
