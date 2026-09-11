using Api.Auth;
using Application.Dtos.Auth;
using IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Auth;

[Collection(DatabaseCollection.Name)]
public class AuthTests
{
    private readonly TestDatabaseFixture _fixture;

    public AuthTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Login_WithValidAdminCredentials_ReturnsToken()
    {
        using var client = _fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest
            {
                Email = ApiWebApplicationFactory.AdminEmail,
                Password = ApiWebApplicationFactory.AdminPassword
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
        Assert.True(body.ExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        using var client = _fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest
            {
                Email = ApiWebApplicationFactory.AdminEmail,
                Password = "WrongPassword#123"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithEmptyFields_ReturnsBadRequest()
    {
        using var client = _fixture.Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = string.Empty, Password = string.Empty });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}