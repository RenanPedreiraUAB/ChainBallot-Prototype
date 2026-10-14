using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ChainBallot.PublicContracts;
using ElectionAuthorization.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ElectionAuthorization.Api.Tests;

/// <summary>
/// Integration tests for Election Authorization endpoints.
/// </summary>
public sealed class ElectionAuthorizationEndpointsTests(AuthorizationApiFactory factory) : IClassFixture<AuthorizationApiFactory>
{
    [Fact]
    public async Task Profile_ReturnsStableVid_ForSameSubject()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwtFactory.Create("subject-john"));

        var first = await client.GetFromJsonAsync<VoterAuthorizationProfile>("/api/authorization/profile");
        var second = await client.GetFromJsonAsync<VoterAuthorizationProfile>("/api/authorization/profile");

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first!.Vid, second!.Vid);
        Assert.Equal("subject-john", first.Subject);
    }

    [Fact]
    public async Task ParallelGrantConsume_AllowsOnlyOneSuccessfulVoteConsumption()
    {
        const string electionId = "election-2026";

        using var seedClient = factory.CreateClient();
        seedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwtFactory.Create("subject-jane"));

        var grantResponse = await seedClient.PostAsync($"/api/authorization/elections/{electionId}/grant", content: null);
        Assert.Equal(HttpStatusCode.OK, grantResponse.StatusCode);

        var grant = await grantResponse.Content.ReadFromJsonAsync<AuthorizationGrant>();
        Assert.NotNull(grant);

        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            using var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwtFactory.Create("subject-jane"));

            var request = new ConsumeAuthorizationRequest(grant!.GrantId, $"nonce-{i}");
            var response = await client.PostAsJsonAsync($"/api/authorization/elections/{electionId}/consume", request);
            return response.StatusCode;
        });

        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(x => x == HttpStatusCode.OK));
        Assert.True(results.Count(x => x == HttpStatusCode.Conflict || x == HttpStatusCode.BadRequest) >= 9);

        var state = await seedClient.GetFromJsonAsync<ElectionAuthorizationState>($"/api/authorization/elections/{electionId}/state");
        Assert.NotNull(state);
        Assert.Equal(ElectionAuthorizationStatus.AlreadyVoted, state!.Status);
    }

    [Fact]
    public async Task ConsumeGrant_SameNonceReplay_ReturnsIdempotentSuccess()
    {
        const string electionId = "election-idempotent";

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwtFactory.Create("subject-replay"));

        var grantResponse = await client.PostAsync($"/api/authorization/elections/{electionId}/grant", content: null);
        Assert.Equal(HttpStatusCode.OK, grantResponse.StatusCode);

        var grant = await grantResponse.Content.ReadFromJsonAsync<AuthorizationGrant>();
        Assert.NotNull(grant);

        var request = new ConsumeAuthorizationRequest(grant!.GrantId, "nonce-replay");

        var firstResponse = await client.PostAsJsonAsync($"/api/authorization/elections/{electionId}/consume", request);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var secondResponse = await client.PostAsJsonAsync($"/api/authorization/elections/{electionId}/consume", request);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        var payload = await secondResponse.Content.ReadFromJsonAsync<ConsumeAuthorizationResponse>();
        Assert.NotNull(payload);
        Assert.Equal(AuthorizationConsumeOutcome.Consumed, payload!.Outcome);
        Assert.NotNull(payload.ConsumedAtUtc);
    }

    [Fact]
    public async Task SeedEligibility_ThenGrantReflectsEligibility()
    {
        const string electionId = "election-seed";
        const string subject = "subject-seeded";

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwtFactory.Create(subject));

        client.DefaultRequestHeaders.Add("X-Admin-Seed-Key", "CHANGE_ME_AUTHZ_ADMIN_SEED_KEY");
        var seedNotEligible = await client.PostAsJsonAsync(
            $"/api/authorization/admin/elections/{electionId}/eligibility",
            new { subject, isEligible = false });
        Assert.Equal(HttpStatusCode.OK, seedNotEligible.StatusCode);

        var blockedGrant = await client.PostAsync($"/api/authorization/elections/{electionId}/grant", content: null);
        Assert.Equal(HttpStatusCode.Conflict, blockedGrant.StatusCode);

        var seedEligible = await client.PostAsJsonAsync(
            $"/api/authorization/admin/elections/{electionId}/eligibility",
            new { subject, isEligible = true });
        Assert.Equal(HttpStatusCode.OK, seedEligible.StatusCode);

        var allowedGrant = await client.PostAsync($"/api/authorization/elections/{electionId}/grant", content: null);
        Assert.Equal(HttpStatusCode.OK, allowedGrant.StatusCode);
    }

    [Fact]
    public async Task GrantAndConsume_PersistsAuditEvents()
    {
        const string electionId = "election-audit";

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwtFactory.Create("subject-audit"));

        var grantResponse = await client.PostAsync($"/api/authorization/elections/{electionId}/grant", content: null);
        Assert.Equal(HttpStatusCode.OK, grantResponse.StatusCode);

        var grant = await grantResponse.Content.ReadFromJsonAsync<AuthorizationGrant>();
        Assert.NotNull(grant);

        var consumeResponse = await client.PostAsJsonAsync(
            $"/api/authorization/elections/{electionId}/consume",
            new ConsumeAuthorizationRequest(grant!.GrantId, "nonce-audit"));

        Assert.Equal(HttpStatusCode.OK, consumeResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuthorizationDbContext>();
        var auditEvents = await dbContext.AuditEvents
            .Where(x => x.ElectionId == electionId)
            .ToListAsync();

        Assert.True(auditEvents.Count >= 2);
        Assert.Contains(auditEvents, x => x.EventType == "authorization.grant.issued" && x.Outcome == "issued");
        Assert.Contains(auditEvents, x => x.EventType == "authorization.grant.consumed" && x.Outcome == "consumed");
    }

    [Fact]
    public async Task ConsumeGrant_MissingNonce_ReturnsInvalidGrant()
    {
        const string electionId = "election-missing-nonce";

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwtFactory.Create("subject-missing-nonce"));

        var grantResponse = await client.PostAsync($"/api/authorization/elections/{electionId}/grant", content: null);
        var grant = await grantResponse.Content.ReadFromJsonAsync<AuthorizationGrant>();
        Assert.NotNull(grant);

        var response = await client.PostAsJsonAsync(
            $"/api/authorization/elections/{electionId}/consume",
            new ConsumeAuthorizationRequest(grant!.GrantId, string.Empty));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ConsumeAuthorizationResponse>();
        Assert.NotNull(payload);
        Assert.Equal(AuthorizationConsumeOutcome.InvalidGrant, payload!.Outcome);
    }

    [Fact]
    public async Task ConsumeGrant_ElectionMismatch_ReturnsInvalidGrant()
    {
        const string issuedElectionId = "election-mismatch-a";
        const string otherElectionId = "election-mismatch-b";

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwtFactory.Create("subject-mismatch"));

        var grantResponse = await client.PostAsync($"/api/authorization/elections/{issuedElectionId}/grant", content: null);
        var grant = await grantResponse.Content.ReadFromJsonAsync<AuthorizationGrant>();
        Assert.NotNull(grant);

        var response = await client.PostAsJsonAsync(
            $"/api/authorization/elections/{otherElectionId}/consume",
            new ConsumeAuthorizationRequest(grant!.GrantId, "nonce-mismatch"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ConsumeAuthorizationResponse>();
        Assert.NotNull(payload);
        Assert.Equal(AuthorizationConsumeOutcome.InvalidGrant, payload!.Outcome);
    }

    [Fact]
    public async Task ConsumeGrant_WhenExpired_ReturnsExpiredGrantOutcome()
    {
        const string electionId = "election-expired";

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwtFactory.Create("subject-expired"));

        var grantResponse = await client.PostAsync($"/api/authorization/elections/{electionId}/grant", content: null);
        var grant = await grantResponse.Content.ReadFromJsonAsync<AuthorizationGrant>();
        Assert.NotNull(grant);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AuthorizationDbContext>();
            var record = await dbContext.Grants.SingleAsync(g => g.GrantId == grant!.GrantId);
            record.ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
            await dbContext.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync(
            $"/api/authorization/elections/{electionId}/consume",
            new ConsumeAuthorizationRequest(grant!.GrantId, "nonce-expired"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ConsumeAuthorizationResponse>();
        Assert.NotNull(payload);
        Assert.Equal(AuthorizationConsumeOutcome.ExpiredGrant, payload!.Outcome);
    }

    [Fact]
    public async Task ConsumeGrant_DifferentNonceAfterAlreadyConsumed_ReturnsInvalidGrant()
    {
        const string electionId = "election-different-nonce";

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwtFactory.Create("subject-different-nonce"));

        var grantResponse = await client.PostAsync($"/api/authorization/elections/{electionId}/grant", content: null);
        var grant = await grantResponse.Content.ReadFromJsonAsync<AuthorizationGrant>();
        Assert.NotNull(grant);

        var firstResponse = await client.PostAsJsonAsync(
            $"/api/authorization/elections/{electionId}/consume",
            new ConsumeAuthorizationRequest(grant!.GrantId, "nonce-first"));
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var secondResponse = await client.PostAsJsonAsync(
            $"/api/authorization/elections/{electionId}/consume",
            new ConsumeAuthorizationRequest(grant.GrantId, "nonce-second"));

        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
        var payload = await secondResponse.Content.ReadFromJsonAsync<ConsumeAuthorizationResponse>();
        Assert.NotNull(payload);
        Assert.Equal(AuthorizationConsumeOutcome.InvalidGrant, payload!.Outcome);
    }
}
