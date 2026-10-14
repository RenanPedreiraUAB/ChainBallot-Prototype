using Microsoft.Extensions.Options;
using VoterComponent.Web.Application;
using VoterComponent.Web.Services;

namespace VoterComponent.Web.Endpoints;

/// <summary>
/// Operator-facing endpoints for the Voter Component's durable submission retry outbox
/// (Architecture.md § 3.4 / Prototype.md § 4.4): state inspection, manual retry, compaction and
/// targeted removal of pending ballot submissions.
/// </summary>
public static class OutboxEndpoints
{
    /// <summary>Registers voter outbox management endpoints.</summary>
    public static IEndpointRouteBuilder MapOutboxEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/voter/outbox")
            .WithTags("VoterOutbox")
            .RequireAuthorization();

        group.MapGet("/state", (IBallotSubmissionOutbox outbox, IOptions<VoterOptions> options) =>
        {
            var policy = ResolvePolicy(options.Value);
            var stats = outbox.GetStatistics(DateTimeOffset.UtcNow, policy.EntryTtl, policy.MaxRetryCount);
            var items = outbox
                .Snapshot()
                .OrderBy(item => item.FailedAtUtc)
                .Select(item => new
                {
                    item.Package.ElectionId,
                    item.Package.Vid,
                    item.Package.BallotNullifier,
                    item.Package.SubmissionNonce,
                    item.Status,
                    item.Reason,
                    item.LastError,
                    item.FailedAtUtc,
                    item.LastAttemptAtUtc,
                    item.RetryCount
                })
                .ToArray();

            return Results.Ok(new
            {
                policy.EntryTtlMinutes,
                policy.MaxRetryCount,
                policy.MaxEntries,
                stats,
                pendingItems = items
            });
        });

        group.MapPost("/retry-next", async (IBallotSubmissionOutbox outbox, IQueuePublisher queuePublisher, CancellationToken cancellationToken) =>
        {
            if (!outbox.TryTakeNext(out var pending))
            {
                return Results.Ok(new { processed = false, message = "No pending submissions." });
            }

            try
            {
                var receipt = await queuePublisher.PublishAsync(pending.Package, cancellationToken).ConfigureAwait(false);
                return Results.Ok(new
                {
                    processed = true,
                    succeeded = true,
                    pending.Package.ElectionId,
                    pending.Package.BallotNullifier,
                    receipt.QueueMode,
                    receipt.QueueItemId,
                    receipt.QueuedAtUtc
                });
            }
            catch (Exception ex)
            {
                outbox.Requeue(pending, $"manual retry failed: {ex.Message}");
                return Results.Ok(new
                {
                    processed = true,
                    succeeded = false,
                    pending.Package.ElectionId,
                    pending.Package.BallotNullifier,
                    error = ex.Message
                });
            }
        });

        group.MapPost("/compact", (IBallotSubmissionOutbox outbox, IOptions<VoterOptions> options) =>
        {
            var policy = ResolvePolicy(options.Value);
            var removed = outbox.Compact(DateTimeOffset.UtcNow, policy.EntryTtl, policy.MaxRetryCount, policy.MaxEntries);
            var stats = outbox.GetStatistics(DateTimeOffset.UtcNow, policy.EntryTtl, policy.MaxRetryCount);

            return Results.Ok(new { removed, stats });
        });

        group.MapDelete("/elections/{electionId}/nullifiers/{ballotNullifier}", (string electionId, string ballotNullifier, IBallotSubmissionOutbox outbox) =>
        {
            var removed = outbox.TryRemove(electionId, ballotNullifier, out var removedEntry);
            if (!removed)
            {
                return Results.NotFound();
            }

            return Results.Ok(new
            {
                removed = true,
                removedEntry.Package.ElectionId,
                removedEntry.Package.BallotNullifier,
                removedEntry.RetryCount,
                removedEntry.FailedAtUtc
            });
        });

        return app;
    }

    private static OutboxPolicy ResolvePolicy(VoterOptions options)
    {
        var ttlMinutes = Math.Max(1, options.SubmissionOutboxEntryTtlMinutes);
        var maxRetryCount = Math.Max(0, options.SubmissionOutboxMaxRetryCount);
        var maxEntries = Math.Max(1, options.SubmissionOutboxMaxEntries);
        return new OutboxPolicy(ttlMinutes, TimeSpan.FromMinutes(ttlMinutes), maxRetryCount, maxEntries);
    }

    private sealed record OutboxPolicy(int EntryTtlMinutes, TimeSpan EntryTtl, int MaxRetryCount, int MaxEntries);
}
