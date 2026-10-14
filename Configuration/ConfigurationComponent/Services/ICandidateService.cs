using ConfigurationComponent.Domain;

namespace ConfigurationComponent.Services;

/// <summary>
/// Defines candidate list management operations for election drafts.
/// </summary>
public interface ICandidateService
{
    /// <summary>
    /// Creates a new candidate using the next available identifier and display order.
    /// </summary>
    /// <param name="existingCandidates">Existing candidates in the draft election.</param>
    /// <param name="name">Candidate display name.</param>
    /// <param name="party">Candidate party or affiliation.</param>
    /// <param name="description">Candidate description text.</param>
    /// <param name="image">Optional image URL or resource reference.</param>
    /// <returns>Newly created candidate instance.</returns>
    Candidate CreateCandidate(IReadOnlyList<Candidate> existingCandidates, string name, string party, string description, string image);

    /// <summary>
    /// Removes a candidate by identifier from the current candidate list.
    /// </summary>
    /// <param name="existingCandidates">Current candidate collection.</param>
    /// <param name="candidateId">Candidate identifier to remove.</param>
    /// <returns>Updated candidate collection after removal.</returns>
    IReadOnlyList<Candidate> RemoveCandidate(IReadOnlyList<Candidate> existingCandidates, int candidateId);
}
