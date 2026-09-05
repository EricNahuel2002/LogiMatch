using Application.Dtos.RouteStops;
using Application.Exceptions;
using Application.Persistence;
using Domain.Entities;
using Domain.ValueObjects;
using FluentValidation;

namespace Application.Services;

public class RouteStopService : IRouteStopService
{
    private readonly IRouteStopRepository _routeStops;
    private readonly IShipmentRepository _shipments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateRouteStopRequest> _createRouteStopValidator;

    public RouteStopService(
        IRouteStopRepository routeStops,
        IShipmentRepository shipments,
        IUnitOfWork unitOfWork,
        IValidator<CreateRouteStopRequest> createRouteStopValidator)
    {
        _routeStops = routeStops;
        _shipments = shipments;
        _unitOfWork = unitOfWork;
        _createRouteStopValidator = createRouteStopValidator;
    }

    public async Task<Guid> CreateAsync(CreateRouteStopRequest request, CancellationToken cancellationToken = default)
    {
        await _createRouteStopValidator.ValidateAndThrowAsync(request, cancellationToken);

        var shipment = await _shipments.GetByIdAsync(request.ShipmentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Shipment), request.ShipmentId);

        var routeStop = RouteStop.Create(
            shipment,
            new Coordinate(request.Latitude, request.Longitude),
            request.StopOrder,
            request.Name);

        await _routeStops.AddAsync(routeStop, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return routeStop.Id;
    }
}