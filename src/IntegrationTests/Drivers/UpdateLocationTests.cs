using Domain.Entities;
using Domain.ValueObjects;
using IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Drivers;

[Collection(DatabaseCollection.Name)]
public class UpdateLocationTests
{
    private readonly TestDatabaseFixture _fixture;

    public UpdateLocationTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UpdateLocation_PersistsDriverLocation()
    {
        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.DriverEmail,
            ApiWebApplicationFactory.UsersPassword);

        var response = await client.PatchAsJsonAsync(
            "/api/drivers/me/location",
            new { latitude = -34.6m, longitude = -58.4m });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var verify = _fixture.Factory.OpenDatabaseAsync();
        var driver = await verify.Db.Users.OfType<Driver>()
            .SingleAsync(d => d.Email == ApiWebApplicationFactory.DriverEmail);

        Assert.Equal(new Coordinate(-34.6m, -58.4m), driver.CurrentLocation);
    }

    [Fact]
    public async Task UpdateLocation_InvalidCoordinates_ReturnsBadRequest()
    {
        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.DriverEmail,
            ApiWebApplicationFactory.UsersPassword);

        var response = await client.PatchAsJsonAsync(
            "/api/drivers/me/location",
            new { latitude = 100m, longitude = -58.4m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateLocation_WithoutToken_ReturnsUnauthorized()
    {
        using var client = _fixture.Factory.CreateClient();

        var response = await client.PatchAsJsonAsync(
            "/api/drivers/me/location",
            new { latitude = -34.6m, longitude = -58.4m });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}