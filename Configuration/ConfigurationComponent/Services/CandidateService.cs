using ConfigurationComponent.Domain;

namespace ConfigurationComponent.Services;

/// <summary>
/// Manages the candidate list of a DRAFT election (Architecture.md § 3.1): assigns stable ids and
/// display order on add/remove so the resulting list always satisfies deployment validation rules
/// (unique ids/names, contiguous display order).
/// </summary>
public sealed class CandidateService : ICandidateService
{
    /// <summary>Creates a new candidate with the next available id and display order.</summary>
    public Candidate CreateCandidate(IReadOnlyList<Candidate> existingCandidates, string name, string party, string description, string image)
    {
        var nextId = existingCandidates.Count == 0 ? 1 : existingCandidates.Max(c => c.Id) + 1;
        var nextOrder = existingCandidates.Count == 0 ? 1 : existingCandidates.Max(c => c.DisplayOrder) + 1;

        return new Candidate(
            Id: nextId,
            Name: name.Trim(),
            Party: party.Trim(),
            Description: description.Trim(),
            Image: image.Trim(),
            DisplayOrder: nextOrder);
    }

    /// <summary>Removes a candidate and re-numbers the remaining candidates' display order to stay contiguous.</summary>
    public IReadOnlyList<Candidate> RemoveCandidate(IReadOnlyList<Candidate> existingCandidates, int candidateId)
    {
        var filtered = existingCandidates
            .Where(c => c.Id != candidateId)
            .OrderBy(c => c.DisplayOrder)
            .ToList();

        return filtered
            .Select((candidate, index) => candidate with { DisplayOrder = index + 1 })
            .ToList();
    }
}
