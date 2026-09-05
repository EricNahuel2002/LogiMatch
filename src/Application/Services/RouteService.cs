using Application.Dtos.Routes;
using Application.Exceptions;
using Application.Persistence;
using Domain.Entities;
using Domain.ValueObjects;
using FluentValidation;

namespace Application.Services;

public class RouteService : IRouteService
{
    private readonly IRouteRepository _routes;
    private readonly IRouteStopRepository _routeStops;
    private readonly IUserRepository _users;
    private readonly IVehicleRepository _vehicles;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateRouteRequest> _createRouteValidator;

    public RouteService(
        IRouteRepository routes,
        IRouteStopRepository routeStops,
        IUserRepository users,
        IVehicleRepository vehicles,
        IUnitOfWork unitOfWork,
        IValidator<CreateRouteRequest> createRouteValidator)
    {
        _routes = routes;
        _routeStops = routeStops;
        _users = users;
        _vehicles = vehicles;
        _unitOfWork = unitOfWork;
        _createRouteValidator = createRouteValidator;
    }

    public async Task<Guid> CreateAsync(CreateRouteRequest request, CancellationToken cancellationToken = default)
    {
        await _createRouteValidator.ValidateAndThrowAsync(request, cancellationToken);

        var route = Route.Create(new Coordinate(request.Latitude, request.Longitude));

        await _routes.AddAsync(route, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return route.Id;
    }

    public async Task AssignDriverAsync(Guid routeId, Guid driverId, CancellationToken cancellationToken = default)
    {
        var route = await GetRouteAsync(routeId, cancellationToken);

        var driver = await _users.GetDriverByIdAsync(driverId, cancellationToken)
            ?? throw new NotFoundException(nameof(Driver), driverId);

        route.AssignDriver(driver);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task AssignVehicleAsync(Guid routeId, Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var route = await GetRouteAsync(routeId, cancellationToken);

        var vehicle = await _vehicles.GetByIdAsync(vehicleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), vehicleId);

        route.AssignVehicle(vehicle);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task AddStopAsync(Guid routeId, Guid routeStopId, CancellationToken cancellationToken = default)
    {
        var route = await GetRouteAsync(routeId, cancellationToken);

        var routeStop = await _routeStops.GetByIdAsync(routeStopId, cancellationToken)
            ?? throw new NotFoundException(nameof(RouteStop), routeStopId);

        routeStop.AssignToRoute(route);
        route.AddStop(routeStop);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Route> GetRouteAsync(Guid routeId, CancellationToken cancellationToken)
    {
        return await _routes.GetByIdAsync(routeId, cancellationToken)
            ?? throw new NotFoundException(nameof(Route), routeId);
    }
}