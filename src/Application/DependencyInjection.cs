using Application.Services;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IShipmentService, ShipmentService>();
        services.AddScoped<IRouteService, RouteService>();
        services.AddScoped<IRouteStopService, RouteStopService>();
        services.AddScoped<IDriverService, DriverService>();

        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}