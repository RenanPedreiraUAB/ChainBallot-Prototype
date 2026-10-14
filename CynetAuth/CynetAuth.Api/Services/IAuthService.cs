using CynetAuth.Api.Contracts;

namespace CynetAuth.Api.Services;

/// <summary>
/// Defines account registration and authentication operations.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Registers a new local user account.
    /// </summary>
    /// <param name="request">Registration payload.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>Creation result and optional failure reason.</returns>
    Task<(bool Created, string? Error)> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Authenticates a user and issues an access token when credentials are valid.
    /// </summary>
    /// <param name="request">Login payload.</param>
    /// <param name="cancellationToken">Cancellation token for cooperative cancellation.</param>
    /// <returns>Authentication response when successful; otherwise null.</returns>
    Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}
