using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CynetAuth.Api.Contracts;

namespace CynetAuth.Api.Tests;

public sealed class AuthEndpointsTests(AuthApiFactory factory) : IClassFixture<AuthApiFactory>
{
    [Fact]
    public async Task Register_ThenLogin_ThenMe_ReturnsAuthenticatedIdentity()
    {
        using var client = factory.CreateClient();

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("alice", "Password123!"));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("alice", "Password123!"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth!.AccessToken));
        Assert.Equal("alice", auth.Username);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        var meResponse = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var me = await meResponse.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.NotNull(me);
        Assert.Equal("alice", me!.Username);
        Assert.False(string.IsNullOrWhiteSpace(me.Subject));
    }

    [Fact]
    public async Task Register_DuplicateUsername_ReturnsBadRequest()
    {
        using var client = factory.CreateClient();

        var firstResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("bob", "Password123!"));
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var duplicateResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("bob", "Password123!"));
        Assert.Equal(HttpStatusCode.BadRequest, duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateUsernameWithDifferentCase_ReturnsBadRequest()
    {
        using var client = factory.CreateClient();

        var firstResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("dave", "Password123!"));
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var duplicateResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("DaVe", "Password123!"));
        Assert.Equal(HttpStatusCode.BadRequest, duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsUnauthorized()
    {
        using var client = factory.CreateClient();

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("carol", "Password123!"));
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("carol", "WrongPassword123!"));
        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }
}
