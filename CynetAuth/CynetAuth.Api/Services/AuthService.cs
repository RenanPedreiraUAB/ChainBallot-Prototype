using CynetAuth.Api.Contracts;
using CynetAuth.Api.Data;
using CynetAuth.Api.Repositories;
using CynetAuth.Api.Security;

namespace CynetAuth.Api.Services;

/// <summary>
/// Implements local credential registration and login for the current authenticator.
/// </summary>
public sealed class AuthService(
    IUserCredentialRepository userCredentialRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService) : IAuthService
{
    /// <inheritdoc />
    public async Task<(bool Created, string? Error)> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var username = request.Username.Trim();
        // Normalize once so uniqueness checks are case-insensitive and deterministic.
        var normalizedUsername = username.ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(username) || request.Password.Length < 8)
        {
            return (false, "Username is required and password must have at least 8 characters.");
        }

        var exists = await userCredentialRepository.ExistsByNormalizedUsernameAsync(normalizedUsername, cancellationToken);

        if (exists)
        {
            return (false, "Username already exists.");
        }

        var (hash, salt) = passwordHasher.HashPassword(request.Password);
        var user = new UserCredential
        {
            Id = Guid.NewGuid(),
            Username = username,
            NormalizedUsername = normalizedUsername,
            PasswordHash = hash,
            PasswordSalt = salt,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        await userCredentialRepository.AddAsync(user, cancellationToken);
        return (true, null);
    }

    /// <inheritdoc />
    public async Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var username = request.Username.Trim();
        var normalizedUsername = username.ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return null;
        }

        var user = await userCredentialRepository.GetActiveByNormalizedUsernameAsync(normalizedUsername, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var isValid = passwordHasher.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt);
        if (!isValid)
        {
            return null;
        }

        var (token, expiresAtUtc) = jwtTokenService.CreateToken(user);

        return new AuthResponse(
            token,
            expiresAtUtc,
            "Bearer",
            user.Id.ToString("N"),
            user.Username);
    }
}
