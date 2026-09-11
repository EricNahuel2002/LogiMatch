using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Auth;
using Application.Dtos.Auth;
using Application.Integrations;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Infrastructure;

public class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@logimatch.com";
    public const string AdminPassword = "Admin#2026";
    public const string DriverEmail = "chofer1@logimatch.com";
    public const string UsersPassword = "LogiMatch#2026";
    private const string TestSecret = "IntegrationTestSecretKey-0123456789abcdef!";

    public RecordingEmailSender EmailRecorder { get; } = new();
    public RecordingGeocodingClient Geocoding { get; } = new();
    public RecordingRouteClient RouteMetrics { get; } = new();

    public string DatabaseName { get; } = $"LogiMatchDB_IntTests_{Guid.NewGuid():N}";

    private string ConnectionString
    {
        get
        {
            const string localFallback =
                "Server=localhost\\SQLEXPRESS;Trusted_Connection=True;TrustServerCertificate=True";

            var configured = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                ?? localFallback;

            var builder = new SqlConnectionStringBuilder(configured)
            {
                InitialCatalog = DatabaseName
            };

            return builder.ConnectionString;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
        builder.UseSetting("Jwt:Secret", TestSecret);
        builder.UseSetting("Seed:AdminEmail", AdminEmail);
        builder.UseSetting("Seed:AdminPassword", AdminPassword);

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IEmailSender>(EmailRecorder);
            services.AddSingleton<IGeocodingClient>(Geocoding);
            services.AddSingleton<IRouteClient>(RouteMetrics);
        });
    }

    public async Task<string> LoginAsync(string email, string password)
    {
        using var client = CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = email, Password = password });

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.Token;
    }

    public async Task<HttpClient> CreateAuthorizedClientAsync(string email, string password)
    {
        var token = await LoginAsync(email, password);
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public TestDbSession OpenDatabaseAsync()
    {
        var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LogiMatchDbContext>();
        return new TestDbSession(scope, db);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            var options = new DbContextOptionsBuilder<LogiMatchDbContext>()
                .UseSqlServer(ConnectionString)
                .Options;

            using var db = new LogiMatchDbContext(options);
            db.Database.EnsureDeleted();
        }
    }

    public sealed class TestDbSession : IDisposable, IAsyncDisposable
    {
        private readonly AsyncServiceScope _scope;

        public TestDbSession(AsyncServiceScope scope, LogiMatchDbContext db)
        {
            _scope = scope;
            Db = db;
        }

        public LogiMatchDbContext Db { get; }

        public void Dispose() => _scope.Dispose();

        public ValueTask DisposeAsync() => _scope.DisposeAsync();
    }
}