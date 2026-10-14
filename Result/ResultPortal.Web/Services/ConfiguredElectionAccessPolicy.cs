using System.Security.Claims;
using Microsoft.Extensions.Options;
using ResultPortal.Web.Application;

namespace ResultPortal.Web.Services;

/// <summary>
/// Default <see cref="IElectionAccessPolicy"/> implementation, driven by <see cref="ResultPortalOptions"/>:
/// resolves per-election public/private visibility and the configured authentication mode
/// (<c>Open</c>/<c>HeaderToken</c>/<c>Oidc</c>), optionally hiding the existence of private elections entirely.
/// </summary>
public sealed class ConfiguredElectionAccessPolicy : IElectionAccessPolicy
{
    private readonly ResultPortalOptions _options;

    public ConfiguredElectionAccessPolicy(IOptions<ResultPortalOptions> options)
    {
        _options = options.Value;
    }

    public ElectionAccessDecision EvaluateResultsAccess(string electionId, ClaimsPrincipal user)
    {
        return EvaluateCore(electionId, user, requiresPrivateScopeOnly: true);
    }

    public ElectionAccessDecision EvaluateVerificationAccess(string electionId, ClaimsPrincipal user)
    {
        return EvaluateCore(electionId, user, requiresPrivateScopeOnly: true);
    }

    private ElectionAccessDecision EvaluateCore(string electionId, ClaimsPrincipal user, bool requiresPrivateScopeOnly)
    {
        if (string.IsNullOrWhiteSpace(electionId))
        {
            return ElectionAccessDecision.Allow();
        }

        var visibility = ResolveVisibility(electionId);
        if (visibility == ElectionVisibility.Public && requiresPrivateScopeOnly)
        {
            return ElectionAccessDecision.Allow();
        }

        var isAuthenticated = user.Identity?.IsAuthenticated == true;
        if (!isAuthenticated)
        {
            if (_options.HidePrivateElectionExistence)
            {
                return ElectionAccessDecision.Deny(ElectionAccessDenyMode.NotFound, "Election not found.");
            }

            return ElectionAccessDecision.Deny(ElectionAccessDenyMode.Challenge, "Authentication is required for this election.");
        }

        var role = (_options.PrivateElectionViewerRole ?? string.Empty).Trim();
        if (role.Length == 0 || user.IsInRole(role))
        {
            return ElectionAccessDecision.Allow();
        }

        if (_options.HidePrivateElectionExistence)
        {
            return ElectionAccessDecision.Deny(ElectionAccessDenyMode.NotFound, "Election not found.");
        }

        return ElectionAccessDecision.Deny(ElectionAccessDenyMode.Forbid, "You are not authorized to view this election.");
    }

    private ElectionVisibility ResolveVisibility(string electionId)
    {
        if (Contains(_options.PublicElectionIds, electionId))
        {
            return ElectionVisibility.Public;
        }

        if (Contains(_options.PrivateElectionIds, electionId))
        {
            return ElectionVisibility.Private;
        }

        if (string.Equals(_options.DefaultElectionVisibility, "Private", StringComparison.OrdinalIgnoreCase))
        {
            return ElectionVisibility.Private;
        }

        return ElectionVisibility.Public;
    }

    private static bool Contains(IEnumerable<string>? values, string target)
    {
        if (values is null)
        {
            return false;
        }

        foreach (var value in values)
        {
            if (string.Equals(value?.Trim(), target, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private enum ElectionVisibility
    {
        Public = 0,
        Private = 1
    }
}
