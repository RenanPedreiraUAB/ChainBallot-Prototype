using System.Net;

namespace VoterComponent.Web.Tests;

public sealed class OutboxOperatorEndpointsTests
{
    [Fact]
    public async Task OutboxStateEndpoint_ReturnsPendingItems_ForAuthenticatedUser()
    {
        using var factory = new VoterComponentWebFactory();
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        await LoginAsync(client);

        // Use existing submit flow to create a pending outbox item by simulating queue failure via direct store.
        var package = factory.QueuePublisher.Snapshot().FirstOrDefault();
        if (package is null)
        {
            // Create one by doing a normal submit first and then insert it into outbox as if retry were needed.
            await SubmitBallotAsync(client);
            package = factory.QueuePublisher.Snapshot().Single();
        }

        factory.SubmissionOutbox.Store(package, "manual test pending");

        var response = await client.GetAsync("/api/voter/outbox/state");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("pendingItems", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(package.BallotNullifier, body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CompactEndpoint_ReturnsOk_ForAuthenticatedUser()
    {
        using var factory = new VoterComponentWebFactory();
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        await LoginAsync(client);

        var response = await client.PostAsync("/api/voter/outbox/compact", content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("removed", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stats", body, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task LoginAsync(HttpClient client)
    {
        var loginPage = await client.GetAsync("/Login");
        loginPage.EnsureSuccessStatusCode();
        var token = AntiforgeryTokenReader.ReadToken(await loginPage.Content.ReadAsStringAsync());

        var loginContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = "alice",
            ["Input.Password"] = "Password123!",
            ["ReturnElectionId"] = "election-demo",
            ["__RequestVerificationToken"] = token
        });

        var loginResponse = await client.PostAsync("/Login", loginContent);
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
    }

    private static async Task SubmitBallotAsync(HttpClient client)
    {
        var dashboardPage = await client.GetAsync("/Dashboard?electionId=election-demo");
        dashboardPage.EnsureSuccessStatusCode();
        var dashboardToken = AntiforgeryTokenReader.ReadToken(await dashboardPage.Content.ReadAsStringAsync());

        var submitContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.ElectionId"] = "election-demo",
            ["Input.CandidateId"] = "1",
            ["Input.CandidateName"] = "Ada Lovelace",
            ["__RequestVerificationToken"] = dashboardToken
        });

        var submitResponse = await client.PostAsync("/Dashboard?handler=SubmitBallot", submitContent);
        submitResponse.EnsureSuccessStatusCode();
    }
}
