using Application.Integrations;
using Application.Persistence;
using Infrastructure.Integrations;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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

        services.AddHttpClient<IGeocodingClient, OpenRouteServiceGeocodingClient>((sp, client) =>
        {
            var configuration = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
            client.BaseAddress = new Uri(
                configuration["OpenRouteService:BaseUrl"] ?? "https://api.heigit.org/openrouteservice/");
        });

        services.AddHttpClient<IRouteDistanceClient, OpenRouteServiceMatrixClient>((sp, client) =>
        {
            var configuration = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
            client.BaseAddress = new Uri(
                configuration["OpenRouteService:BaseUrl"] ?? "https://api.heigit.org/openrouteservice/");
        });

        return services;
    }
}