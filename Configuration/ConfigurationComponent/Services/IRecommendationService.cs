using ConfigurationComponent.Domain;

namespace ConfigurationComponent.Services;

/// <summary>
/// Defines sizing recommendations for deployment and processing parameters.
/// </summary>
public interface IRecommendationService
{
    /// <summary>
    /// Computes deployment recommendations from expected election load.
    /// </summary>
    /// <param name="expectedVoters">Estimated voter count.</param>
    /// <param name="electionDurationHours">Planned election duration in hours.</param>
    /// <param name="peakParticipationPercent">Estimated peak participation percentage.</param>
    /// <returns>Recommended deployment settings.</returns>
    DeploymentRecommendation Recommend(int expectedVoters, int electionDurationHours, int peakParticipationPercent);
}
