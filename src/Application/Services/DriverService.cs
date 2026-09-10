using Application.Dtos.Drivers;
using Application.Exceptions;
using Application.Integrations;
using Application.Persistence;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using FluentValidation;

namespace Application.Services;

public class DriverService : IDriverService
{
    private readonly IUserRepository _users;
    private readonly IRouteRepository _routes;
    private readonly IRouteStopRepository _routeStops;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailSender _emailSender;
    private readonly IValidator<UpdateDriverLocationRequest> _updateLocationValidator;
    private readonly IValidator<CancelRouteRequest> _cancelRouteValidator;

    public DriverService(
        IUserRepository users,
        IRouteRepository routes,
        IRouteStopRepository routeStops,
        IUnitOfWork unitOfWork,
        IEmailSender emailSender,
        IValidator<UpdateDriverLocationRequest> updateLocationValidator,
        IValidator<CancelRouteRequest> cancelRouteValidator)
    {
        _users = users;
        _routes = routes;
        _routeStops = routeStops;
        _unitOfWork = unitOfWork;
        _emailSender = emailSender;
        _updateLocationValidator = updateLocationValidator;
        _cancelRouteValidator = cancelRouteValidator;
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

        if (requeuedShipments.Count > 0)
        {
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