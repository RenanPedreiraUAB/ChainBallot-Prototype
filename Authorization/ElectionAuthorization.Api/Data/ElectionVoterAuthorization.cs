namespace ElectionAuthorization.Api.Data;

/// <summary>
/// Stores election-specific eligibility and vote-consumption state for a VID.
/// </summary>
public sealed class ElectionVoterAuthorization
{
    /// <summary>
    /// Gets or sets internal record identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets election identifier.
    /// </summary>
    public string ElectionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets VID bound to this election state.
    /// </summary>
    public string Vid { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this VID is eligible to vote in the election.
    /// </summary>
    public bool IsEligible { get; set; }

    /// <summary>
    /// Gets or sets whether a vote has already been consumed.
    /// </summary>
    public bool HasVoted { get; set; }

    /// <summary>
    /// Gets or sets vote consumption timestamp in UTC.
    /// </summary>
    public DateTimeOffset? VotedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets creation timestamp in UTC.
    /// </summary>
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets last update timestamp in UTC.
    /// </summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
