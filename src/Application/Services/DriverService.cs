using Application.Dtos.Drivers;
using Application.Exceptions;
using Application.Integrations;
using Application.Persistence;
using Domain.Entities;
using Domain.Enums;
using Domain.Services;
using Domain.ValueObjects;
using FluentValidation;

namespace Application.Services;

public class DriverService : IDriverService
{
    private readonly IUserRepository _users;
    private readonly IRouteRepository _routes;
    private readonly IRouteStopRepository _routeStops;
    private readonly IShipmentRepository _shipments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailSender _emailSender;
    private readonly IShipmentPriorityAssignmentService _priorityAssignment;
    private readonly IDriverRealtimeNotifier _realtimeNotifier;
    private readonly IValidator<UpdateDriverLocationRequest> _updateLocationValidator;
    private readonly IValidator<CancelRouteRequest> _cancelRouteValidator;

    public DriverService(
        IUserRepository users,
        IRouteRepository routes,
        IRouteStopRepository routeStops,
        IShipmentRepository shipments,
        IUnitOfWork unitOfWork,
        IEmailSender emailSender,
        IShipmentPriorityAssignmentService priorityAssignment,
        IDriverRealtimeNotifier realtimeNotifier,
        IValidator<UpdateDriverLocationRequest> updateLocationValidator,
        IValidator<CancelRouteRequest> cancelRouteValidator)
    {
        _users = users;
        _routes = routes;
        _routeStops = routeStops;
        _shipments = shipments;
        _unitOfWork = unitOfWork;
        _emailSender = emailSender;
        _priorityAssignment = priorityAssignment;
        _realtimeNotifier = realtimeNotifier;
        _updateLocationValidator = updateLocationValidator;
        _cancelRouteValidator = cancelRouteValidator;
    }

    public async Task<IReadOnlyList<DriverSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var drivers = await _users.GetAllDriversAsync(cancellationToken);
        return drivers.Select(d => new DriverSummaryDto(d.Id, d.Name, d.Surname, d.Email, d.PhoneNumber)).ToList();
    }

    public async Task<DriverRouteCurrentDto?> GetCurrentRouteAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        var route = await _routes.GetActiveByDriverIdAsync(driverId, cancellationToken);
        if (route is null)
        {
            return null;
        }

        var orderedStops = route.RouteStops
            .OrderBy(stop => stop.StopOrder)
            .ToList();

        var progress = orderedStops
            .Select(stop => new RouteStopProgress(stop.ShipmentId, stop.StopOrder, stop.Shipment.Status))
            .ToList();

        var currentShipmentId = RouteSequencePolicy.GetCurrentStopShipmentId(progress);

        var stops = orderedStops
            .Select(stop => new DriverRouteStopDto(
                stop.Id,
                stop.ShipmentId,
                stop.StopOrder,
                stop.Name,
                stop.Coordinate.Latitude,
                stop.Coordinate.Longitude,
                stop.Shipment.Status,
                FormatCustomerName(stop.Shipment.Order.Customer),
                stop.ShipmentId == currentShipmentId,
                stop.Shipment.Order.Items
                    .Select(item => new DriverOrderItemDto(item.Name, item.Quantity, item.Price))
                    .ToList()))
            .ToList();

        return new DriverRouteCurrentDto(
            route.Id,
            route.IsActive,
            route.VehicleId,
            route.Origin.Latitude,
            route.Origin.Longitude,
            stops,
            route.CreatedAt);
    }

    private static string? FormatCustomerName(Customer? customer)
    {
        if (customer is null)
        {
            return null;
        }

        var name = $"{customer.Name} {customer.Surname}".Trim();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    public async Task<IReadOnlyList<DriverShipmentDto>> GetShipmentsAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        var shipments = await _shipments.GetAssignedWithHistoryAsync(cancellationToken);
        return shipments.Where(s => s.Order.AssignedDriverId == driverId)
            .Select(s => new DriverShipmentDto(s.Id, s.Status, s.Priority, s.OrderId, s.CreatedAt))
            .ToList();
    }

    public async Task UpdateLocationAsync(
        Guid driverId,
        UpdateDriverLocationRequest request,
        CancellationToken cancellationToken = default)
    {
        await _updateLocationValidator.ValidateAndThrowAsync(request, cancellationToken);

        var driver = await _users.GetDriverByIdAsync(driverId, cancellationToken)
            ?? throw new NotFoundException(nameof(Driver), driverId);

        driver.SetCurrentLocation(new Coordinate(request.Latitude, request.Longitude));

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelCurrentRouteAsync(
        Guid driverId,
        CancelRouteRequest request,
        CancellationToken cancellationToken = default)
    {
        await _cancelRouteValidator.ValidateAndThrowAsync(request, cancellationToken);

        var driver = await _users.GetDriverByIdAsync(driverId, cancellationToken)
            ?? throw new NotFoundException(nameof(Driver), driverId);

        var route = await _routes.GetActiveByDriverIdAsync(driverId, cancellationToken)
            ?? throw new NotFoundException(nameof(Route), driverId);

        var requeuedShipments = new List<Shipment>();
        foreach (var stop in route.RouteStops.ToList())
        {
            var shipment = stop.Shipment;
            if (!IsTerminal(shipment.Status))
            {
                shipment.Requeue(request.Reason, request.Note);
                requeuedShipments.Add(shipment);
            }

            _routeStops.Remove(stop);
        }

        route.Deactivate(request.Reason);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _realtimeNotifier.RouteCancelledAsync(driverId, route.Id, cancellationToken);

        if (requeuedShipments.Count > 0)
        {
            await _priorityAssignment.RecalculatePrioritiesAsync(cancellationToken);
            await NotifyAdminsAsync(driver, route, requeuedShipments, request, cancellationToken);
        }
    }

    private async Task NotifyAdminsAsync(
        Driver driver,
        Route route,
        IReadOnlyCollection<Shipment> shipments,
        CancelRouteRequest request,
        CancellationToken cancellationToken)
    {
        var admins = await _users.GetAdminsAsync(cancellationToken);
        var recipients = admins
            .Select(a => a.Email)
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Cast<string>()
            .ToList();

        if (recipients.Count == 0)
        {
            return;
        }

        var lines = string.Join(
            Environment.NewLine,
            shipments.Select(s => $"- Envío {s.Id} (Orden {s.OrderId})"));

        var body =
            $"El conductor {driver.Name} {driver.Surname} canceló su ruta que debía reiniciarse." +
            $"{Environment.NewLine}{Environment.NewLine}" +
            $"Motivo: {request.Reason}." +
            $"{(string.IsNullOrWhiteSpace(request.Note) ? string.Empty : $"{Environment.NewLine}Detalle: {request.Note}")}" +
            $"{Environment.NewLine}{Environment.NewLine}" +
            $"Envíos pendientes de reasignación:{Environment.NewLine}{lines}";

        await _emailSender.SendAsync(
            recipients,
            "LogiMatch - Envíos pendientes de reasignación",
            body,
            cancellationToken);
    }

    private static bool IsTerminal(ShipmentStatus status)
    {
        return status is ShipmentStatus.Finalized
            or ShipmentStatus.DeliveryFailed
            or ShipmentStatus.Cancelled;
    }
}
