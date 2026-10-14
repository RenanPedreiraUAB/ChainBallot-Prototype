using System.Security.Claims;

namespace ResultPortal.Web.Services;

/// <summary>
/// Election visibility policy boundary for the Result Portal (Architecture.md § 9.1): decides whether
/// the current caller may view results/verification for a given election (public vs. private, and
/// which authentication mode gates private access), evaluated before any Transparency query is made.
/// </summary>
public interface IElectionAccessPolicy
{
    /// <summary>Evaluates whether the caller may view the results snapshot for an election.</summary>
    ElectionAccessDecision EvaluateResultsAccess(string electionId, ClaimsPrincipal user);

    /// <summary>Evaluates whether the caller may perform VID verification for an election.</summary>
    ElectionAccessDecision EvaluateVerificationAccess(string electionId, ClaimsPrincipal user);
}
