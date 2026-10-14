namespace CynetAuth.Api.Contracts;

/// <summary>
/// Represents a user registration request.
/// </summary>
/// <param name="Username">Desired account username.</param>
/// <param name="Password">Plain-text password provided by the user.</param>
public sealed record RegisterRequest(string Username, string Password);

/// <summary>
/// Represents a user login request.
/// </summary>
/// <param name="Username">Account username.</param>
/// <param name="Password">Plain-text password provided by the user.</param>
public sealed record LoginRequest(string Username, string Password);

/// <summary>
/// Represents a successful authentication response.
/// </summary>
/// <param name="AccessToken">JWT access token.</param>
/// <param name="ExpiresAtUtc">Token expiration timestamp in UTC.</param>
/// <param name="TokenType">Token scheme type.</param>
/// <param name="Subject">Stable subject identifier for the authenticated account.</param>
/// <param name="Username">Display username for the authenticated account.</param>
public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    string TokenType,
    string Subject,
    string Username);

/// <summary>
/// Represents the authenticated user identity resolved from a validated token.
/// </summary>
/// <param name="Subject">Stable account subject identifier.</param>
/// <param name="Username">Account username.</param>
public sealed record CurrentUserResponse(string Subject, string Username);
