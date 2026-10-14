using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;

namespace VoterComponent.Web.Tests;

public sealed class VoterComponentWebTests
{
    [Fact]
    public async Task Login_ThenDashboard_RendersAuthenticatedWorkspace()
    {
        using var factory = new VoterComponentWebFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var loginPage = await client.GetAsync("/Login");
        loginPage.EnsureSuccessStatusCode();
        var loginToken = AntiforgeryTokenReader.ReadToken(await loginPage.Content.ReadAsStringAsync());

        var loginContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = "alice",
            ["Input.Password"] = "Password123!",
            ["ReturnElectionId"] = "election-demo",
            ["__RequestVerificationToken"] = loginToken
        });

        var loginResponse = await client.PostAsync("/Login", loginContent);
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        var dashboard = await client.GetAsync("/Dashboard?electionId=election-demo");
        dashboard.EnsureSuccessStatusCode();

        var dashboardHtml = await dashboard.Content.ReadAsStringAsync();
        Assert.Contains("Authenticate, inspect eligibility", dashboardHtml);
        Assert.Contains("Ada Lovelace", dashboardHtml);
        Assert.Contains("Eligibility", dashboardHtml);
    }

    [Fact]
    public async Task SubmitBallot_PublishesQueueItem_AndConsumesGrant()
    {
        using var factory = new VoterComponentWebFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var loginPage = await client.GetAsync("/Login");
        var loginToken = AntiforgeryTokenReader.ReadToken(await loginPage.Content.ReadAsStringAsync());

        var loginContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Username"] = "alice",
            ["Input.Password"] = "Password123!",
            ["ReturnElectionId"] = "election-demo",
            ["__RequestVerificationToken"] = loginToken
        });

        var loginResponse = await client.PostAsync("/Login", loginContent);
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);

        var dashboardPage = await client.GetAsync("/Dashboard?electionId=election-demo");
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

        var submitHtml = await submitResponse.Content.ReadAsStringAsync();
        Assert.Contains("Ballot has been encrypted, queued, and authorized.", submitHtml);
        Assert.Single(factory.QueuePublisher.Snapshot());

        var package = factory.QueuePublisher.Snapshot().Single();
        Assert.Equal("election-demo", package.ElectionId);
        Assert.False(string.IsNullOrWhiteSpace(package.BallotCommitment));
        Assert.Equal("A256GCM", package.EncryptedVotePayload.Algorithm);
    }
}