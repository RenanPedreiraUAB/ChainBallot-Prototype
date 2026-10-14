using ChainBallot.PublicContracts;
using ElectionAuthorization.Api.Data;
using ElectionAuthorization.Api.Repositories;
using ElectionAuthorization.Api.Security;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace ElectionAuthorization.Api.Services;

/// <summary>
/// Implements VID provisioning and one-time election authorization semantics.
/// </summary>
public sealed class ElectionAuthorizationService(
    IVoterIdentityRepository voterIdentityRepository,
    IElectionAuthorizationRepository electionAuthorizationRepository,
    IAuthorizationGrantRepository authorizationGrantRepository,
    IAuthorizationAuditRepository authorizationAuditRepository,
    AuthorizationDbContext dbContext,
    IVidGenerator vidGenerator) : IElectionAuthorizationService
{
    private static readonly TimeSpan GrantLifetime = TimeSpan.FromMinutes(2);

    /// <inheritdoc />
    public async Task<VoterAuthorizationProfile> GetOrCreateProfileAsync(string subject, CancellationToken cancellationToken)
    {
        var existing = await voterIdentityRepository.GetBySubjectAsync(subject, cancellationToken);
        if (existing is not null)
        {
            return new VoterAuthorizationProfile(existing.Subject, existing.Vid, existing.CreatedAtUtc);
        }

        var mapping = new VoterIdentityMapping
        {
            Id = Guid.NewGuid(),
            Subject = subject,
            Vid = vidGenerator.GenerateVid(),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        await voterIdentityRepository.AddAsync(mapping, cancellationToken);

        await AppendAuditAsync(
            eventType: "vid.profile.created",
            subject: subject,
            vid: mapping.Vid,
            electionId: null,
            outcome: "created",
            details: null,
            cancellationToken: cancellationToken);

        return new VoterAuthorizationProfile(mapping.Subject, mapping.Vid, mapping.CreatedAtUtc);
    }

    /// <inheritdoc />
    public async Task<ElectionAuthorizationState> GetElectionStateAsync(string subject, string electionId, CancellationToken cancellationToken)
    {
        var profile = await GetOrCreateProfileAsync(subject, cancellationToken);
        var state = await EnsureElectionStateAsync(electionId, profile.Vid, cancellationToken);

        if (!state.IsEligible)
        {
            return new ElectionAuthorizationState(electionId, profile.Vid, ElectionAuthorizationStatus.NotEligible, state.VotedAtUtc);
        }

        return state.HasVoted
            ? new ElectionAuthorizationState(electionId, profile.Vid, ElectionAuthorizationStatus.AlreadyVoted, state.VotedAtUtc)
            : new ElectionAuthorizationState(electionId, profile.Vid, ElectionAuthorizationStatus.EligibleNotVoted, null);
    }

    /// <inheritdoc />
    public async Task<AuthorizationGrant?> IssueGrantAsync(string subject, string electionId, CancellationToken cancellationToken)
    {
        var profile = await GetOrCreateProfileAsync(subject, cancellationToken);
        var state = await EnsureElectionStateAsync(electionId, profile.Vid, cancellationToken);
        if (!state.IsEligible || state.HasVoted)
        {
            await AppendAuditAsync(
                eventType: "authorization.grant.issued",
                subject: subject,
                vid: profile.Vid,
                electionId: electionId,
                outcome: "rejected",
                details: state.HasVoted ? "already-voted" : "not-eligible",
                cancellationToken: cancellationToken);

            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var grant = new AuthorizationGrantRecord
        {
            GrantId = Guid.NewGuid(),
            ElectionId = electionId,
            Vid = profile.Vid,
            IssuedAtUtc = now,
            ExpiresAtUtc = now.Add(GrantLifetime),
            IsConsumed = false
        };

        await authorizationGrantRepository.AddAsync(grant, cancellationToken);

        await AppendAuditAsync(
            eventType: "authorization.grant.issued",
            subject: subject,
            vid: profile.Vid,
            electionId: electionId,
            outcome: "issued",
            details: grant.GrantId.ToString("N"),
            cancellationToken: cancellationToken);

        return new AuthorizationGrant(
            grant.GrantId,
            grant.ElectionId,
            grant.Vid,
            grant.IssuedAtUtc,
            grant.ExpiresAtUtc);
    }

    /// <inheritdoc />
    public async Task<ConsumeAuthorizationResponse> ConsumeGrantAsync(
        string subject,
        string electionId,
        ConsumeAuthorizationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SubmissionNonce))
        {
            await AppendAuditAsync(
                eventType: "authorization.grant.consumed",
                subject: subject,
                vid: null,
                electionId: electionId,
                outcome: "invalid-request",
                details: "missing-nonce",
                cancellationToken: cancellationToken);

            return new ConsumeAuthorizationResponse(
                electionId,
                string.Empty,
                AuthorizationConsumeOutcome.InvalidGrant,
                null,
                "Submission nonce is required.");
        }

        var profile = await GetOrCreateProfileAsync(subject, cancellationToken);

        var grant = await authorizationGrantRepository.GetAsync(request.GrantId, cancellationToken);
        if (grant is null || grant.ElectionId != electionId || grant.Vid != profile.Vid)
        {
            await AppendAuditAsync(
                eventType: "authorization.grant.consumed",
                subject: subject,
                vid: profile.Vid,
                electionId: electionId,
                outcome: "invalid-grant",
                details: request.GrantId.ToString("N"),
                cancellationToken: cancellationToken);

            return new ConsumeAuthorizationResponse(
                electionId,
                profile.Vid,
                AuthorizationConsumeOutcome.InvalidGrant,
                null,
                "Grant is invalid for this election or voter.");
        }

        // Idempotent replay support: if same nonce replays a consumed grant, return the previous success.
        if (grant.IsConsumed)
        {
            if (string.Equals(grant.SubmissionNonce, request.SubmissionNonce, StringComparison.Ordinal)
                && grant.ConsumedAtUtc is not null)
            {
                return new ConsumeAuthorizationResponse(
                    electionId,
                    profile.Vid,
                    AuthorizationConsumeOutcome.Consumed,
                    grant.ConsumedAtUtc,
                    "Authorization already consumed for this nonce.");
            }

            return new ConsumeAuthorizationResponse(
                electionId,
                profile.Vid,
                AuthorizationConsumeOutcome.InvalidGrant,
                null,
                "Grant is no longer valid.");
        }

        var state = await EnsureElectionStateAsync(electionId, profile.Vid, cancellationToken);
        if (!state.IsEligible)
        {
            await AppendAuditAsync(
                eventType: "authorization.grant.consumed",
                subject: subject,
                vid: profile.Vid,
                electionId: electionId,
                outcome: "not-eligible",
                details: null,
                cancellationToken: cancellationToken);

            return new ConsumeAuthorizationResponse(
                electionId,
                profile.Vid,
                AuthorizationConsumeOutcome.NotEligible,
                null,
                "Voter is not eligible for this election.");
        }

        if (state.HasVoted)
        {
            await AppendAuditAsync(
                eventType: "authorization.grant.consumed",
                subject: subject,
                vid: profile.Vid,
                electionId: electionId,
                outcome: "already-voted",
                details: null,
                cancellationToken: cancellationToken);

            return new ConsumeAuthorizationResponse(
                electionId,
                profile.Vid,
                AuthorizationConsumeOutcome.AlreadyVoted,
                state.VotedAtUtc,
                "Vote has already been consumed.");
        }

        var now = DateTimeOffset.UtcNow;
        if (grant.ExpiresAtUtc <= now)
        {
            await AppendAuditAsync(
                eventType: "authorization.grant.consumed",
                subject: subject,
                vid: profile.Vid,
                electionId: electionId,
                outcome: "expired-grant",
                details: request.GrantId.ToString("N"),
                cancellationToken: cancellationToken);

            return new ConsumeAuthorizationResponse(
                electionId,
                profile.Vid,
                AuthorizationConsumeOutcome.ExpiredGrant,
                null,
                "Grant has expired.");
        }

        // Consume vote eligibility and grant atomically to prevent parallel tab/device races.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var consumedVote = await electionAuthorizationRepository.TryConsumeVoteAsync(electionId, profile.Vid, now, cancellationToken);
        if (!consumedVote)
        {
            await transaction.RollbackAsync(cancellationToken);
            await AppendAuditAsync(
                eventType: "authorization.grant.consumed",
                subject: subject,
                vid: profile.Vid,
                electionId: electionId,
                outcome: "already-voted",
                details: "race-lost",
                cancellationToken: cancellationToken);

            return new ConsumeAuthorizationResponse(
                electionId,
                profile.Vid,
                AuthorizationConsumeOutcome.AlreadyVoted,
                now,
                "Vote has already been consumed.");
        }

        var consumedGrant = await authorizationGrantRepository.TryConsumeAsync(request.GrantId, request.SubmissionNonce, now, cancellationToken);
        if (!consumedGrant)
        {
            await transaction.RollbackAsync(cancellationToken);
            await AppendAuditAsync(
                eventType: "authorization.grant.consumed",
                subject: subject,
                vid: profile.Vid,
                electionId: electionId,
                outcome: "invalid-grant",
                details: "consume-failed",
                cancellationToken: cancellationToken);

            return new ConsumeAuthorizationResponse(
                electionId,
                profile.Vid,
                AuthorizationConsumeOutcome.InvalidGrant,
                null,
                "Grant is no longer valid.");
        }

        await transaction.CommitAsync(cancellationToken);

        await AppendAuditAsync(
            eventType: "authorization.grant.consumed",
            subject: subject,
            vid: profile.Vid,
            electionId: electionId,
            outcome: "consumed",
            details: request.GrantId.ToString("N"),
            cancellationToken: cancellationToken);

        return new ConsumeAuthorizationResponse(
            electionId,
            profile.Vid,
            AuthorizationConsumeOutcome.Consumed,
            now,
            "Authorization consumed.");
    }

    /// <inheritdoc />
    public async Task<ElectionAuthorizationState> SetEligibilityAsync(
        string subject,
        string electionId,
        bool isEligible,
        CancellationToken cancellationToken)
    {
        var profile = await GetOrCreateProfileAsync(subject, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var updated = await electionAuthorizationRepository.UpsertEligibilityAsync(
            electionId,
            profile.Vid,
            isEligible,
            now,
            cancellationToken);

        await AppendAuditAsync(
            eventType: "authorization.eligibility.seeded",
            subject: subject,
            vid: profile.Vid,
            electionId: electionId,
            outcome: "updated",
            details: isEligible ? "eligible" : "not-eligible",
            cancellationToken: cancellationToken);

        var status = !updated.IsEligible
            ? ElectionAuthorizationStatus.NotEligible
            : updated.HasVoted
                ? ElectionAuthorizationStatus.AlreadyVoted
                : ElectionAuthorizationStatus.EligibleNotVoted;

        return new ElectionAuthorizationState(updated.ElectionId, updated.Vid, status, updated.VotedAtUtc);
    }

    private async Task<ElectionVoterAuthorization> EnsureElectionStateAsync(
        string electionId,
        string vid,
        CancellationToken cancellationToken)
    {
        var existing = await electionAuthorizationRepository.GetAsync(electionId, vid, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var now = DateTimeOffset.UtcNow;
        var created = new ElectionVoterAuthorization
        {
            Id = Guid.NewGuid(),
            ElectionId = electionId,
            Vid = vid,
            IsEligible = true,
            HasVoted = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        try
        {
            await electionAuthorizationRepository.AddAsync(created, cancellationToken);
            return created;
        }
        catch (DbUpdateException)
        {
            // Another concurrent request initialized this row first.
            var retry = await electionAuthorizationRepository.GetAsync(electionId, vid, cancellationToken);
            if (retry is null)
            {
                throw;
            }

            return retry;
        }
    }

    private async Task AppendAuditAsync(
        string eventType,
        string subject,
        string? vid,
        string? electionId,
        string outcome,
        string? details,
        CancellationToken cancellationToken)
    {
        var subjectHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(subject))).ToLowerInvariant();

        await authorizationAuditRepository.AddAsync(
            new AuthorizationAuditEvent
            {
                Id = Guid.NewGuid(),
                EventType = eventType,
                SubjectHash = subjectHash,
                Vid = vid,
                ElectionId = electionId,
                Outcome = outcome,
                Details = details,
                OccurredAtUtc = DateTimeOffset.UtcNow
            },
            cancellationToken);
    }
}
