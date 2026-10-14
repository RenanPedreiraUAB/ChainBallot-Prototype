using System.Security.Claims;
using CynetAuth.Api.Contracts;
using CynetAuth.Api.Services;
using Microsoft.AspNetCore.Authorization;

namespace CynetAuth.Api.Endpoints;

/// <summary>
/// Maps authentication-related HTTP endpoints.
/// </summary>
public static class AuthEndpoints
{
    /// <summary>
    /// Registers auth endpoints used by downstream components and clients.
    /// </summary>
    /// <param name="app">Endpoint route builder.</param>
    /// <returns>Updated endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", async (RegisterRequest request, IAuthService authService, CancellationToken cancellationToken) =>
        {
            var (created, error) = await authService.RegisterAsync(request, cancellationToken);
            return created
                ? Results.Created("/api/auth/login", new { message = "User registered." })
                : Results.BadRequest(new { error });
        })
        .AllowAnonymous();

        group.MapPost("/login", async (LoginRequest request, IAuthService authService, CancellationToken cancellationToken) =>
        {
            var response = await authService.LoginAsync(request, cancellationToken);
            return response is null
                ? Results.Unauthorized()
                : Results.Ok(response);
        })
        .AllowAnonymous();

        group.MapGet("/me", [Authorize] (ClaimsPrincipal principal) =>
        {
            // Keep fallback claim resolution explicit because different JWT handlers
            // may map NameIdentifier and Name differently.
            var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal.FindFirstValue("sub")
                ?? string.Empty;

            var username = principal.Identity?.Name
                ?? principal.FindFirstValue(ClaimTypes.Name)
                ?? principal.FindFirstValue("unique_name")
                ?? string.Empty;

            return Results.Ok(new CurrentUserResponse(subject, username));
        });

        return app;
    }
}
