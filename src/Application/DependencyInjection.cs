using Application.RoutePlanning;
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
        services.AddScoped<IShipmentAssignmentService, ShipmentAssignmentService>();
        services.AddScoped<IShipmentPriorityAssignmentService, ShipmentPriorityAssignmentService>();
        services.AddScoped<IRouteService, RouteService>();
        services.AddScoped<IRouteStopService, RouteStopService>();
        services.AddScoped<IDriverService, DriverService>();
        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<IDepositService, DepositService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IDelayRiskService, DelayRiskService>();
        services.AddScoped<IRoutePlanningService, RoutePlanningService>();

        services.AddSingleton<IDepositAssignmentPolicy, NearestDepositAssignmentPolicy>();
        services.AddSingleton(TimeProvider.System);

        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}