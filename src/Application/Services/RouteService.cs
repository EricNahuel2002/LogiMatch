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
    private readonly IDepositRepository _deposits;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateRouteRequest> _createRouteValidator;
    private readonly IValidator<CommitFromProposalRequest> _commitFromProposalValidator;

    public RouteService(
        IRouteRepository routes,
        IRouteStopRepository routeStops,
        IUserRepository users,
        IVehicleRepository vehicles,
        IDepositRepository deposits,
        IUnitOfWork unitOfWork,
        IValidator<CreateRouteRequest> createRouteValidator,
        IValidator<CommitFromProposalRequest> commitFromProposalValidator)
    {
        _routes = routes;
        _routeStops = routeStops;
        _users = users;
        _vehicles = vehicles;
        _deposits = deposits;
        _unitOfWork = unitOfWork;
        _createRouteValidator = createRouteValidator;
        _commitFromProposalValidator = commitFromProposalValidator;
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

    public async Task<IReadOnlyList<RouteSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var routes = await _routes.GetAllAsync(cancellationToken);

        return routes.Select(r => new RouteSummaryDto(
            r.Id,
            r.IsActive,
            r.CancellationReason,
            r.VehicleId,
            r.DriverId,
            r.CreatedAt,
            r.UpdatedAt)).ToList();
    }

    public async Task<RouteDetailsDto?> GetByIdAsync(Guid routeId, CancellationToken cancellationToken = default)
    {
        var r = await _routes.GetByIdWithStopsAsync(routeId, cancellationToken);

        if (r is null)
        {
            return null;
        }

        return new RouteDetailsDto(
            r.Id,
            r.IsActive,
            r.CancellationReason,
            r.VehicleId,
            r.DriverId,
            r.Origin,
            r.RouteStops.OrderBy(s => s.StopOrder).Select(ToStopSummary).ToArray(),
            r.CreatedAt,
            r.UpdatedAt);
    }

    public async Task<CommitFromProposalResponse> CommitFromProposalAsync(CommitFromProposalRequest request, CancellationToken cancellationToken = default)
    {
        await _commitFromProposalValidator.ValidateAndThrowAsync(request, cancellationToken);

        var committed = new List<CommittedRouteDto>();

        foreach (var assignment in request.Assignments)
        {
            var deposit = await _deposits.GetByIdAsync(assignment.DepositId, cancellationToken)
                ?? throw new NotFoundException(nameof(Deposit), assignment.DepositId);

            var driver = await _users.GetDriverByIdAsync(assignment.DriverId, cancellationToken)
                ?? throw new NotFoundException(nameof(Driver), assignment.DriverId);

            var vehicle = await _vehicles.GetByIdAsync(assignment.VehicleId, cancellationToken)
                ?? throw new NotFoundException(nameof(Vehicle), assignment.VehicleId);

            var route = Route.Create(deposit.Coordinate);
            route.AssignDriver(driver);
            route.AssignVehicle(vehicle);

            var routeStopIds = new List<Guid>();

            foreach (var stop in assignment.Stops.OrderBy(s => s.StopOrder))
            {
                var routeStop = await _routeStops.GetByShipmentIdAsync(stop.ShipmentId, cancellationToken)
                    ?? throw new NotFoundException(nameof(RouteStop), stop.ShipmentId);

                routeStop.AssignToRoute(route);
                route.AddStop(routeStop);
                routeStopIds.Add(routeStop.Id);
            }

            await _routes.AddAsync(route, cancellationToken);
            committed.Add(new CommittedRouteDto(route.Id, driver.Id, routeStopIds.ToArray()));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CommitFromProposalResponse(committed.ToArray());
    }

    private static RouteStopSummaryDto ToStopSummary(RouteStop s)
    {
        return new RouteStopSummaryDto(
            s.Id,
            s.RouteId ?? Guid.Empty,
            s.ShipmentId,
            s.Name,
            s.StopOrder,
            s.Coordinate.Latitude,
            s.Coordinate.Longitude,
            s.DistanceMeters,
            s.TollCost);
    }

    private async Task<Route> GetRouteAsync(Guid routeId, CancellationToken cancellationToken)
    {
        return await _routes.GetByIdAsync(routeId, cancellationToken)
            ?? throw new NotFoundException(nameof(Route), routeId);
    }
}