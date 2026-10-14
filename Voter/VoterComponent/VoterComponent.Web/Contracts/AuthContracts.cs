namespace VoterComponent.Web.Contracts;

public sealed record LoginRequest(string Username, string Password);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    string TokenType,
    string Subject,
    string Username);

public sealed record CurrentUserResponse(string Subject, string Username);

public static class SessionClaimNames
{
    public const string AccessToken = "chainballot_access_token";
    public const string AccessTokenExpiresUtc = "chainballot_access_token_expires_utc";
}