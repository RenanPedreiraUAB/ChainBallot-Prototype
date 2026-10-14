namespace ElectionAuthorization.Api.Contracts;

/// <summary>
/// Represents admin request payload to set election eligibility for a subject.
/// </summary>
/// <param name="Subject">Trusted authenticator subject identifier.</param>
/// <param name="IsEligible">Eligibility value to set.</param>
public sealed record SeedEligibilityRequest(string Subject, bool IsEligible);
