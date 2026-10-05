using Application.Integrations;
using Application.Persistence;
using Application.RoutePlanning;
using Infrastructure.Email;
using Infrastructure.Integrations;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Infrastructure.RoutePlanning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<LogiMatchDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IShipmentRepository, ShipmentRepository>();
        services.AddScoped<IRouteRepository, RouteRepository>();
        services.AddScoped<IRouteStopRepository, RouteStopRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IDepositRepository, DepositRepository>();

        services.AddOptions<EmailOptions>()
            .Configure<IConfiguration>((options, configuration) =>
                configuration.GetSection("Email").Bind(options));
        services.AddScoped<IEmailSender, MailKitEmailSender>();

        services.AddHttpClient<IGeocodingClient, OpenRouteServiceGeocodingClient>((sp, client) =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            client.BaseAddress = new Uri(
                configuration["OpenRouteService:BaseUrl"] ?? "https://api.heigit.org/openrouteservice/");
        });

        services.AddHttpClient<IRouteClient, OpenRouteServiceMatrixClient>((sp, client) =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            client.BaseAddress = new Uri(
                configuration["OpenRouteService:BaseUrl"] ?? "https://api.heigit.org/openrouteservice/");
        });

        services.AddHttpClient<IRouteMatrixClient, OpenRouteServiceFullMatrixClient>((sp, client) =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            client.BaseAddress = new Uri(
                configuration["OpenRouteService:BaseUrl"] ?? "https://api.heigit.org/openrouteservice/");
        });

        services.AddOptions<DriverScoringOptions>()
            .Configure<IConfiguration>((options, configuration) =>
                configuration.GetSection("DriverScoring").Bind(options))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Consumed as the domain policy, not as options: the Application layer reads the
        // thresholds through DriverEligibilityPolicy and stays free of the options pattern.
        services.AddSingleton(sp => sp
            .GetRequiredService<IOptions<DriverScoringOptions>>()
            .Value
            .ToPolicy());
        services.AddScoped<IDriverEligibilityFilter, DriverEligibilityFilter>();

        services.AddSingleton<IVehicleRoutingSolver, OrToolsVehicleRoutingSolver>();

        return services;
    }
}