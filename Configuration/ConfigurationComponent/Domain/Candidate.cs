namespace ConfigurationComponent.Domain;

/// <summary>
/// Represents Candidate.
/// </summary>
public sealed record Candidate(
    int Id,
    string Name,
    string Party,
    string Description,
    string Image,
    int DisplayOrder);
