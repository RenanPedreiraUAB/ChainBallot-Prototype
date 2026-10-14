using VoterComponent.Web.Contracts;

namespace VoterComponent.Web.Services;

public interface IAuthProvider
{
    Task<AuthResponse?> AuthenticateAsync(LoginRequest request, CancellationToken cancellationToken);
}