using System.Security.Claims;
using ChainBallot.PublicContracts;
using ElectionAuthorization.Api.Contracts;
using ElectionAuthorization.Api.Security;
using ElectionAuthorization.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace ElectionAuthorization.Api.Endpoints;

/// <summary>
/// Maps Election Authorization Component HTTP endpoints.
/// </summary>
public static class ElectionAuthorizationEndpoints
{
    /// <summary>
    /// Registers election authorization endpoints.
    /// </summary>
    /// <param name="app">Endpoint route builder.</param>
    /// <returns>Updated endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapElectionAuthorizationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/authorization").WithTags("ElectionAuthorization").RequireAuthorization();

        group.MapGet("/profile", async (ClaimsPrincipal principal, IElectionAuthorizationService service, CancellationToken cancellationToken) =>
        {
            var subject = ResolveSubject(principal);
            if (string.IsNullOrWhiteSpace(subject))
            {
                return Results.Unauthorized();
            }

            var profile = await service.GetOrCreateProfileAsync(subject, cancellationToken);
            return Results.Ok(profile);
        });

        group.MapGet("/elections/{electionId}/state", async (string electionId, ClaimsPrincipal principal, IElectionAuthorizationService service, CancellationToken cancellationToken) =>
        {
            var subject = ResolveSubject(principal);
            if (string.IsNullOrWhiteSpace(subject))
            {
                return Results.Unauthorized();
            }

            var state = await service.GetElectionStateAsync(subject, electionId, cancellationToken);
            return Results.Ok(state);
        });

        group.MapPost("/elections/{electionId}/grant", async (string electionId, ClaimsPrincipal principal, IElectionAuthorizationService service, CancellationToken cancellationToken) =>
        {
            var subject = ResolveSubject(principal);
            if (string.IsNullOrWhiteSpace(subject))
            {
                return Results.Unauthorized();
            }

            var grant = await service.IssueGrantAsync(subject, electionId, cancellationToken);
            return grant is null
                ? Results.Conflict(new { error = "Voter is not eligible or has already voted." })
                : Results.Ok(grant);
        });

        group.MapPost("/elections/{electionId}/consume", async (
            string electionId,
            ConsumeAuthorizationRequest request,
            ClaimsPrincipal principal,
            IElectionAuthorizationService service,
            CancellationToken cancellationToken) =>
        {
            var subject = ResolveSubject(principal);
            if (string.IsNullOrWhiteSpace(subject))
            {
                return Results.Unauthorized();
            }

            var result = await service.ConsumeGrantAsync(subject, electionId, request, cancellationToken);

            return result.Outcome switch
            {
                AuthorizationConsumeOutcome.Consumed => Results.Ok(result),
                AuthorizationConsumeOutcome.AlreadyVoted => Results.Conflict(result),
                AuthorizationConsumeOutcome.NotEligible => Results.Forbid(),
                AuthorizationConsumeOutcome.ExpiredGrant => Results.BadRequest(result),
                _ => Results.BadRequest(result)
            };
        });

        group.MapPost("/admin/elections/{electionId}/eligibility", async (
            string electionId,
            SeedEligibilityRequest request,
            HttpContext httpContext,
            IElectionAuthorizationService service,
            IOptions<AuthorizationAdminOptions> adminOptions,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Subject))
            {
                return Results.BadRequest(new { error = "Subject is required." });
            }

            if (!httpContext.Request.Headers.TryGetValue("X-Admin-Seed-Key", out var providedKey)
                || string.IsNullOrWhiteSpace(adminOptions.Value.SeedKey)
                || !string.Equals(providedKey.ToString(), adminOptions.Value.SeedKey, StringComparison.Ordinal))
            {
                return Results.Unauthorized();
            }

            var state = await service.SetEligibilityAsync(request.Subject, electionId, request.IsEligible, cancellationToken);
            return Results.Ok(state);
        }).AllowAnonymous();

        return app;
    }

    private static string ResolveSubject(ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub")
            ?? string.Empty;
    }
}
