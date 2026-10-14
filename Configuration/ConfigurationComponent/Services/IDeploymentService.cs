using ConfigurationComponent.Domain;

namespace ConfigurationComponent.Services;

/// <summary>
/// Defines the orchestration contract for end-to-end election deployment.
/// </summary>
public interface IDeploymentService
{
    /// <summary>
    /// Validates, publishes metadata, deploys to blockchain, and persists final election state.
    /// </summary>
    /// <param name="election">Draft election to deploy.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>Updated election plus blockchain deployment result.</returns>
    Task<(ElectionConfiguration UpdatedElection, DeploymentResult Result)> DeployAsync(
        ElectionConfiguration election,
        CancellationToken cancellationToken = default);
}
