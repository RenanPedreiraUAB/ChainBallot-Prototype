namespace ConfigurationComponent.Models;

/// <summary>
/// Represents Candidate Editor Item.
/// </summary>
public sealed class CandidateEditorItem
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    public required string Party { get; init; }

    public required string Description { get; init; }

    public required string Image { get; init; }

    public required int DisplayOrder { get; init; }
}
