using VoterComponent.Web.Contracts;

namespace VoterComponent.Web.Services;

/// <summary>
/// Orchestrates the Voter Component's side of UIS-01/UIS-02 (Architecture.md § 3.4): resolving the
/// voter's dashboard state (VID, eligibility, election metadata) and driving ballot encryption,
/// commitment/nullifier generation, submission to the message broker, and authorization-grant consumption.
/// </summary>
public interface IVoterWorkflowService
{
    /// <summary>
    /// Resolves the authenticated voter's VID profile, election eligibility and election metadata,
    /// so the ballot UI can be rendered (or an authorization failure can be shown).
    /// </summary>
    Task<VoterDashboardState> LoadDashboardAsync(string electionId, CancellationToken cancellationToken);

    /// <summary>
    /// Requests a one-time authorization grant, encrypts the selected candidate into a ballot,
    /// computes its commitment/nullifier, publishes the resulting <c>BallotSubmissionPackage</c> to
    /// the message broker, and consumes the authorization grant once publication succeeds.
    /// </summary>
    Task<VoterSubmissionOutcome> SubmitBallotAsync(BallotSelectionRequest request, CancellationToken cancellationToken);
}