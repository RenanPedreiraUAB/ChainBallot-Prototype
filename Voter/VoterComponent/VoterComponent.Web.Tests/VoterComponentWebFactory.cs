using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ChainBallot.PublicContracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VoterComponent.Web.Contracts;
using VoterComponent.Web.Services;

namespace VoterComponent.Web.Tests;

public sealed class VoterComponentWebFactory : WebApplicationFactory<Program>
{
    public CapturingQueuePublisher QueuePublisher { get; } = new();
    public InMemoryBallotSubmissionOutbox SubmissionOutbox { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IAuthProvider>(new FakeAuthProvider());
            services.AddSingleton<IElectionAuthorizationClient>(new FakeElectionAuthorizationClient());
            services.AddSingleton<IElectionMetadataClient>(new FakeElectionMetadataClient());
            services.AddSingleton<IQueuePublisher>(QueuePublisher);
            services.AddSingleton<IBallotSubmissionOutbox>(SubmissionOutbox);
        });
    }

    private sealed class FakeAuthProvider : IAuthProvider
    {
        public Task<AuthResponse?> AuthenticateAsync(LoginRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult<AuthResponse?>(new AuthResponse(
                AccessToken: "test-access-token",
                ExpiresAtUtc: DateTimeOffset.UtcNow.AddHours(1),
                TokenType: "Bearer",
                Subject: "subject-alice",
                Username: request.Username));
        }
    }

    private sealed class FakeElectionAuthorizationClient : IElectionAuthorizationClient
    {
        private static readonly VoterAuthorizationProfile Profile = new("subject-alice", "VID-ALICE-001", DateTimeOffset.UtcNow.AddMinutes(-10));
        private static readonly ElectionAuthorizationState State = new("election-demo", "VID-ALICE-001", ElectionAuthorizationStatus.EligibleNotVoted, null);

        public Task<VoterAuthorizationProfile?> GetProfileAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<VoterAuthorizationProfile?>(Profile);
        }

        public Task<ElectionAuthorizationState?> GetElectionStateAsync(string electionId, CancellationToken cancellationToken)
        {
            return Task.FromResult<ElectionAuthorizationState?>(State with { ElectionId = electionId });
        }

        public Task<AuthorizationGrant?> IssueGrantAsync(string electionId, CancellationToken cancellationToken)
        {
            return Task.FromResult<AuthorizationGrant?>(new AuthorizationGrant(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                electionId,
                Profile.Vid,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddMinutes(2)));
        }

        public Task<ConsumeAuthorizationResponse?> ConsumeGrantAsync(string electionId, Guid grantId, string submissionNonce, CancellationToken cancellationToken)
        {
            return Task.FromResult<ConsumeAuthorizationResponse?>(new ConsumeAuthorizationResponse(
                electionId,
                Profile.Vid,
                AuthorizationConsumeOutcome.Consumed,
                DateTimeOffset.UtcNow,
                "Authorization consumed."));
        }
    }

    private sealed class FakeElectionMetadataClient : IElectionMetadataClient
    {
        public Task<ElectionMetadataDocument?> GetElectionMetadataAsync(string electionId, CancellationToken cancellationToken)
        {
            var document = new ElectionMetadataDocument(
                new ElectionMetadataElection(electionId, "City Election", "Local office ballot", "1.0.0"),
                new ElectionMetadataVoting(1000, 2000),
                new ElectionMetadataBlockchain("devnet", "program-1", "pda-1"),
                new[]
                {
                    new ElectionMetadataCandidate(1, "Ada Lovelace", "Science", "Independent candidate", "ada.png"),
                    new ElectionMetadataCandidate(2, "Alan Turing", "Logic", "Independent candidate", "alan.png")
                },
                new ElectionMetadataVerification("abc123", 1234567890));

            return Task.FromResult<ElectionMetadataDocument?>(document);
        }
    }
}

public sealed class CapturingQueuePublisher : IQueuePublisher
{
    private readonly ConcurrentQueue<BallotSubmissionPackage> _packages = new();

    public IReadOnlyCollection<BallotSubmissionPackage> Snapshot() => _packages.ToArray();

    public Task<QueueReceipt> PublishAsync(BallotSubmissionPackage package, CancellationToken cancellationToken)
    {
        _packages.Enqueue(package);
        return Task.FromResult(new QueueReceipt(Guid.NewGuid(), DateTimeOffset.UtcNow, "InMemory"));
    }
}

public static class AntiforgeryTokenReader
{
    public static string ReadToken(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"(?<token>[^\"]+)\"");
        if (!match.Success)
        {
            throw new InvalidOperationException("Antiforgery token not found in HTML.");
        }

        return match.Groups["token"].Value;
    }
}