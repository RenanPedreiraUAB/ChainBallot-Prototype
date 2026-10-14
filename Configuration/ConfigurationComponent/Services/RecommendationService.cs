using ConfigurationComponent.Domain;

namespace ConfigurationComponent.Services;

/// <summary>
/// Realizes the Election Configuration Component's scale-recommendation model (Architecture.md § 3.1 /
/// Prototype.md § 4.1): derives suggested consumer count, message-broker partition count, batch size,
/// and settlement interval from expected voter volume, election duration and peak-participation
/// assumptions, so operational configuration is informed by the same values used to validate the election.
/// </summary>
public sealed class RecommendationService : IRecommendationService
{
    /// <summary>Computes a deployment/scale recommendation for the given election sizing assumptions.</summary>
    public DeploymentRecommendation Recommend(int expectedVoters, int electionDurationHours, int peakParticipationPercent)
    {
        var peakLoadFactor = Math.Max(1.0, peakParticipationPercent / 20.0);

        var recommendedConsumers = Math.Clamp((int)Math.Ceiling(expectedVoters / 5000.0 * peakLoadFactor), 1, 64);
        var recommendedPartitions = Math.Clamp((int)Math.Ceiling(recommendedConsumers * 1.5), 1, 128);
        var recommendedBatch = Math.Clamp((int)Math.Round(1000.0 / peakLoadFactor), 100, 2000);

        var settlementSeconds = peakParticipationPercent >= 70 ? 5 : peakParticipationPercent >= 40 ? 10 : 20;
        var totalTx = Math.Max(1, expectedVoters / Math.Max(1, recommendedBatch));

        var estimatedCost = (decimal)totalTx * 0.000005m + (decimal)electionDurationHours * 0.0002m;

        return new DeploymentRecommendation(
            RecommendedConsumerNodes: recommendedConsumers,
            RecommendedKafkaPartitions: recommendedPartitions,
            RecommendedBatchSize: recommendedBatch,
            RecommendedSettlementInterval: TimeSpan.FromSeconds(settlementSeconds),
            EstimatedBlockchainCostSol: decimal.Round(estimatedCost, 6));
    }
}
