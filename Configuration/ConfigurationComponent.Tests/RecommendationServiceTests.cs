using ConfigurationComponent.Services;

namespace ConfigurationComponent.Tests;

/// <summary>
/// Represents Recommendation Service Tests.
/// </summary>
public sealed class RecommendationServiceTests
{
    private readonly RecommendationService _service = new();

    [Fact]
    /// <summary>
    /// Executes Recommend_With Typical Inputs_Returns Stable Heuristic Output.
    /// </summary>
    public void Recommend_WithTypicalInputs_ReturnsStableHeuristicOutput()
    {
        var result = _service.Recommend(expectedVoters: 10000, electionDurationHours: 24, peakParticipationPercent: 50);

        Assert.Equal(5, result.RecommendedConsumerNodes);
        Assert.Equal(8, result.RecommendedKafkaPartitions);
        Assert.Equal(400, result.RecommendedBatchSize);
        Assert.Equal(TimeSpan.FromSeconds(10), result.RecommendedSettlementInterval);
        Assert.Equal(0.004925m, result.EstimatedBlockchainCostSol);
    }

    [Fact]
    /// <summary>
    /// Executes Recommend_With High Peak Participation_Uses Fast Settlement.
    /// </summary>
    public void Recommend_WithHighPeakParticipation_UsesFastSettlement()
    {
        var result = _service.Recommend(expectedVoters: 1000, electionDurationHours: 12, peakParticipationPercent: 85);

        Assert.Equal(TimeSpan.FromSeconds(5), result.RecommendedSettlementInterval);
    }

    [Fact]
    /// <summary>
    /// Executes Recommend_With Low Peak Participation_Uses Slow Settlement.
    /// </summary>
    public void Recommend_WithLowPeakParticipation_UsesSlowSettlement()
    {
        var result = _service.Recommend(expectedVoters: 1000, electionDurationHours: 12, peakParticipationPercent: 25);

        Assert.Equal(TimeSpan.FromSeconds(20), result.RecommendedSettlementInterval);
    }

    [Fact]
    /// <summary>
    /// Executes Recommend_Always Respects Bounds.
    /// </summary>
    public void Recommend_AlwaysRespectsBounds()
    {
        var result = _service.Recommend(expectedVoters: 1_000_000_000, electionDurationHours: 24, peakParticipationPercent: 100);

        Assert.InRange(result.RecommendedConsumerNodes, 1, 64);
        Assert.InRange(result.RecommendedKafkaPartitions, 1, 128);
        Assert.InRange(result.RecommendedBatchSize, 100, 2000);
    }
}
