using Application.Dtos.Customers;
using Application.Dtos.Deposits;
using Application.Dtos.Vehicles;
using IntegrationTests.Infrastructure;
using System.Net;
using System.Net.Http.Json;

namespace IntegrationTests.Catalog;

[Collection(DatabaseCollection.Name)]
public class CatalogQueryTests
{
    private readonly TestDatabaseFixture _fixture;

    public CatalogQueryTests(TestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetVehicles_ReturnsSeededVehicles()
    {
        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.GetAsync("/api/vehicles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadJsonAsync<List<VehicleSummaryDto>>();
        Assert.NotNull(body);
        Assert.Contains(body!, v => v.LicensePlate == "AA123BB");
    }

    [Fact]
    public async Task GetDeposits_ReturnsSeededDeposits()
    {
        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.GetAsync("/api/deposits");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadJsonAsync<List<DepositSummaryDto>>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!);
    }

    [Fact]
    public async Task GetCustomers_ReturnsSeededCustomers()
    {
        using var client = await _fixture.Factory.CreateAuthorizedClientAsync(
            ApiWebApplicationFactory.AdminEmail,
            ApiWebApplicationFactory.AdminPassword);

        var response = await client.GetAsync("/api/customers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadJsonAsync<List<CustomerSummaryDto>>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!);
    }

    [Fact]
    public async Task GetVehicles_WithoutToken_ReturnsUnauthorized()
    {
        using var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/vehicles");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
