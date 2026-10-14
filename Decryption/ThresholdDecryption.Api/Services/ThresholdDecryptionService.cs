using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ThresholdDecryption.Api.Application;
using ThresholdDecryption.Api.Domain;

namespace ThresholdDecryption.Api.Services;

/// <summary>
/// Default implementation of <see cref="IThresholdDecryptionService"/>. Persists ceremony state via
/// <see cref="IThresholdCeremonyRepository"/> and enforces the governance state machine described in
/// Architecture.md § 3.11 (independent approvals + independent trustee shares required before finalize).
/// </summary>
public sealed class ThresholdDecryptionService : IThresholdDecryptionService
{
    private readonly IThresholdCeremonyRepository _repository;
    private readonly ICanonicalElectionStateClient _canonicalElectionStateClient;
    private readonly ThresholdOptions _options;

    public ThresholdDecryptionService(
        IThresholdCeremonyRepository repository,
        ICanonicalElectionStateClient canonicalElectionStateClient,
        IOptions<ThresholdOptions> options)
    {
        _repository = repository;
        _canonicalElectionStateClient = canonicalElectionStateClient;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<ThresholdCeremony> ProposeCeremonyAsync(
        string electionId,
        int? requiredShares,
        int? requiredApprovals,
        string actor,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(electionId))
        {
            throw new ArgumentException("Election id is required.", nameof(electionId));
        }

        var existing = await _repository.GetByElectionAsync(electionId, cancellationToken).ConfigureAwait(false);
        if (existing is not null && existing.Status is CeremonyStatus.Proposed or CeremonyStatus.Approved or CeremonyStatus.Decrypting)
        {
            return existing;
        }

        // PRECONDITION (Architecture.md § 4.3 "Confirm election closed"): a ceremony may not be proposed
        // until the canonical election state confirms closure. See ICanonicalElectionStateClient for the
        // prototype-simplification note on why IsFinalized is the signal used here.
        if (!await IsCanonicalCloseConfirmedAsync(electionId, cancellationToken).ConfigureAwait(false))
        {
            var rejected = new ThresholdCeremony(
                Guid.NewGuid().ToString("N"),
                electionId,
                Math.Max(1, requiredShares ?? _options.DefaultRequiredShares),
                Math.Max(1, requiredApprovals ?? _options.DefaultRequiredApprovals),
                CeremonyStatus.Failed,
                [],
                [],
                [new DecryptionEvent("closure-check-rejected", actor, "Canonical election state does not confirm Closed/Finalized; ceremony proposal rejected.", DateTimeOffset.UtcNow)],
                null,
                null,
                DateTimeOffset.UtcNow,
                null,
                "Canonical election state is not confirmed Closed/Finalized.",
                null);

            return await _repository.SaveAsync(rejected, cancellationToken).ConfigureAwait(false);
        }

        var ceremony = new ThresholdCeremony(
            Guid.NewGuid().ToString("N"),
            electionId,
            Math.Max(1, requiredShares ?? _options.DefaultRequiredShares),
            Math.Max(1, requiredApprovals ?? _options.DefaultRequiredApprovals),
            CeremonyStatus.Proposed,
            [],
            [],
            [new DecryptionEvent("proposed", actor, "Threshold decryption ceremony proposed.", DateTimeOffset.UtcNow)],
            null,
            null,
            DateTimeOffset.UtcNow,
            null,
            null,
            null);

        return await _repository.SaveAsync(ceremony, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ThresholdCeremony?> ApproveCeremonyAsync(string electionId, string approverId, string note, CancellationToken cancellationToken)
    {
        var ceremony = await _repository.GetByElectionAsync(electionId, cancellationToken).ConfigureAwait(false);
        if (ceremony is null || ceremony.Status is CeremonyStatus.Completed or CeremonyStatus.Failed)
        {
            return ceremony;
        }

        if (!IsAllowed(_options.GovernanceApprovers, approverId))
        {
            return ceremony with
            {
                Events = ceremony.Events.Append(new DecryptionEvent("approval-rejected", approverId, "Approver is not authorized.", DateTimeOffset.UtcNow)).ToArray()
            };
        }

        var exists = ceremony.Approvals.Any(a => string.Equals(a.ApproverId, approverId, StringComparison.OrdinalIgnoreCase));
        if (exists)
        {
            return ceremony;
        }

        var approvals = ceremony.Approvals.Append(new GovernanceApproval(approverId, note, DateTimeOffset.UtcNow)).ToArray();
        var status = approvals.Length >= ceremony.RequiredApprovals ? CeremonyStatus.Approved : CeremonyStatus.Proposed;
        var updated = ceremony with
        {
            Approvals = approvals,
            Status = status,
            Events = ceremony.Events.Append(new DecryptionEvent("approved", approverId, "Governance approval recorded.", DateTimeOffset.UtcNow)).ToArray()
        };

        return await _repository.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ThresholdCeremony?> SubmitShareAsync(string electionId, string trusteeId, string shareDigest, CancellationToken cancellationToken)
    {
        var ceremony = await _repository.GetByElectionAsync(electionId, cancellationToken).ConfigureAwait(false);
        if (ceremony is null || ceremony.Status is CeremonyStatus.Completed or CeremonyStatus.Failed)
        {
            return ceremony;
        }

        if (ceremony.Approvals.Count < ceremony.RequiredApprovals)
        {
            return ceremony with
            {
                Events = ceremony.Events.Append(new DecryptionEvent("share-rejected", trusteeId, "Governance approvals are not yet satisfied.", DateTimeOffset.UtcNow)).ToArray()
            };
        }

        if (!IsAllowed(_options.AllowedTrustees, trusteeId))
        {
            return ceremony with
            {
                Events = ceremony.Events.Append(new DecryptionEvent("share-rejected", trusteeId, "Trustee is not authorized.", DateTimeOffset.UtcNow)).ToArray()
            };
        }

        var exists = ceremony.Shares.Any(s => string.Equals(s.TrusteeId, trusteeId, StringComparison.OrdinalIgnoreCase));
        if (exists)
        {
            return ceremony;
        }

        var shares = ceremony.Shares.Append(new TrusteeShare(trusteeId, shareDigest, DateTimeOffset.UtcNow)).ToArray();
        var status = shares.Length >= ceremony.RequiredShares ? CeremonyStatus.Decrypting : CeremonyStatus.Approved;

        var updated = ceremony with
        {
            Shares = shares,
            Status = status,
            Events = ceremony.Events.Append(new DecryptionEvent("share-submitted", trusteeId, "Trustee share submitted.", DateTimeOffset.UtcNow)).ToArray()
        };

        return await _repository.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ThresholdCeremony?> FinalizeDecryptionAsync(
        string electionId,
        IReadOnlyDictionary<string, long> decryptedTallies,
        string actor,
        CancellationToken cancellationToken)
    {
        var ceremony = await _repository.GetByElectionAsync(electionId, cancellationToken).ConfigureAwait(false);
        if (ceremony is null || ceremony.Status is CeremonyStatus.Completed or CeremonyStatus.Failed)
        {
            return ceremony;
        }

        if (ceremony.Approvals.Count < ceremony.RequiredApprovals || ceremony.Shares.Count < ceremony.RequiredShares)
        {
            var rejected = ceremony with
            {
                Events = ceremony.Events.Append(new DecryptionEvent("finalize-rejected", actor, "Threshold requirements are not yet satisfied.", DateTimeOffset.UtcNow)).ToArray()
            };

            return await _repository.SaveAsync(rejected, cancellationToken).ConfigureAwait(false);
        }

        // Same canonical-Closed precondition as proposal: bypassing it here would let finalisation
        // proceed for a ceremony that was proposed before the check existed, or if canonical state
        // regresses. Never overwrite an already-verified ceremony with an unconfirmed one, though -
        // only the transition into Completed is gated.
        if (!await IsCanonicalCloseConfirmedAsync(electionId, cancellationToken).ConfigureAwait(false))
        {
            var rejected = ceremony with
            {
                Events = ceremony.Events.Append(new DecryptionEvent("closure-check-rejected", actor, "Canonical election state does not confirm Closed/Finalized; finalisation rejected.", DateTimeOffset.UtcNow)).ToArray()
            };

            return await _repository.SaveAsync(rejected, cancellationToken).ConfigureAwait(false);
        }

        var digest = ComputeTallyDigest(electionId, decryptedTallies);
        var updated = ceremony with
        {
            Status = CeremonyStatus.Completed,
            DecryptedTallies = new Dictionary<string, long>(decryptedTallies, StringComparer.Ordinal),
            ResultDigest = digest,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            Events = ceremony.Events.Append(new DecryptionEvent("finalized", actor, "Threshold decryption finalized.", DateTimeOffset.UtcNow)).ToArray()
        };

        return await _repository.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> IsCanonicalCloseConfirmedAsync(string electionId, CancellationToken cancellationToken)
    {
        var state = await _canonicalElectionStateClient.GetElectionStateAsync(electionId, cancellationToken).ConfigureAwait(false);
        return state is not null && state.IsFinalized;
    }

    public async Task<ThresholdCeremony?> PublishFinalTallyAsync(string electionId, string actor, CancellationToken cancellationToken)
    {
        var ceremony = await _repository.GetByElectionAsync(electionId, cancellationToken).ConfigureAwait(false);
        if (ceremony is null || ceremony.Status != CeremonyStatus.Completed || ceremony.DecryptedTallies is null)
        {
            return ceremony;
        }

        if (ceremony.Publication is not null)
        {
            return ceremony;
        }

        var publication = new FinalTallyPublication(
            Guid.NewGuid().ToString("N"),
            ceremony.ElectionId,
            ceremony.CeremonyId,
            ceremony.DecryptedTallies,
            ceremony.ResultDigest ?? ComputeTallyDigest(ceremony.ElectionId, ceremony.DecryptedTallies),
            $"threshold://{ceremony.ElectionId}/{ceremony.CeremonyId}",
            DateTimeOffset.UtcNow);

        var updated = ceremony with
        {
            Publication = publication,
            Events = ceremony.Events.Append(new DecryptionEvent("published", actor, "Final tally publication emitted.", DateTimeOffset.UtcNow)).ToArray()
        };

        return await _repository.SaveAsync(updated, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsAllowed(IEnumerable<string> allowed, string actor)
        => allowed.Any(x => string.Equals(x?.Trim(), actor, StringComparison.OrdinalIgnoreCase));

    private static string ComputeTallyDigest(string electionId, IReadOnlyDictionary<string, long> tallies)
    {
        var canonical = string.Join("|", tallies.OrderBy(k => k.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}:{kv.Value}"));
        var payload = Encoding.UTF8.GetBytes($"{electionId}|{canonical}");
        var hash = SHA256.HashData(payload);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
