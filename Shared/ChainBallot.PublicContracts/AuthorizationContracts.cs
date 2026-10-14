namespace ChainBallot.PublicContracts;

/// <summary>
/// Represents the current authorization status for a voter within an election.
/// </summary>
public enum ElectionAuthorizationStatus
{
    EligibleNotVoted = 0,
    AlreadyVoted = 1,
    NotEligible = 2
}

/// <summary>
/// Represents the result when attempting to consume a one-time authorization grant.
/// </summary>
public enum AuthorizationConsumeOutcome
{
    Consumed = 0,
    AlreadyVoted = 1,
    InvalidGrant = 2,
    ExpiredGrant = 3,
    NotEligible = 4
}

/// <summary>
/// Represents the voter profile returned by the Election Authorization Component.
/// </summary>
/// <param name="Subject">Stable authenticated user subject from TTP.</param>
/// <param name="Vid">Virtual identity used in election flows.</param>
/// <param name="CreatedAtUtc">VID creation timestamp in UTC.</param>
public sealed record VoterAuthorizationProfile(
    string Subject,
    string Vid,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// Represents the election-specific authorization state for a VID.
/// </summary>
/// <param name="ElectionId">Election identifier.</param>
/// <param name="Vid">Virtual identity.</param>
/// <param name="Status">Current authorization status.</param>
/// <param name="VotedAtUtc">Vote consumption timestamp when already voted.</param>
public sealed record ElectionAuthorizationState(
    string ElectionId,
    string Vid,
    ElectionAuthorizationStatus Status,
    DateTimeOffset? VotedAtUtc);

/// <summary>
/// Represents a one-time grant issued to authorize vote submission.
/// </summary>
/// <param name="GrantId">One-time grant identifier.</param>
/// <param name="ElectionId">Election identifier.</param>
/// <param name="Vid">Virtual identity bound to the grant.</param>
/// <param name="IssuedAtUtc">Grant issuance timestamp in UTC.</param>
/// <param name="ExpiresAtUtc">Grant expiration timestamp in UTC.</param>
public sealed record AuthorizationGrant(
    Guid GrantId,
    string ElectionId,
    string Vid,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc);

/// <summary>
/// Represents a vote authorization grant consumption request.
/// </summary>
/// <param name="GrantId">Grant identifier to consume.</param>
/// <param name="SubmissionNonce">Client-generated nonce for replay-aware tracing.</param>
public sealed record ConsumeAuthorizationRequest(
    Guid GrantId,
    string SubmissionNonce);

/// <summary>
/// Represents a vote authorization grant consumption response.
/// </summary>
/// <param name="ElectionId">Election identifier.</param>
/// <param name="Vid">Virtual identity bound to the request.</param>
/// <param name="Outcome">Consumption outcome.</param>
/// <param name="ConsumedAtUtc">Consumption timestamp when successful.</param>
/// <param name="Message">Optional detail message.</param>
public sealed record ConsumeAuthorizationResponse(
    string ElectionId,
    string Vid,
    AuthorizationConsumeOutcome Outcome,
    DateTimeOffset? ConsumedAtUtc,
    string? Message);
