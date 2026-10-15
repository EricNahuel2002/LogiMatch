using Application.Dtos.Shipments;
using Application.Exceptions;
using Application.Persistence;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;

namespace Application.Services;

public class ShipmentService : IShipmentService
{
    private readonly IShipmentRepository _shipments;
    private readonly IOrderRepository _orders;
    private readonly IRouteStopRepository _routeStops;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IShipmentPriorityAssignmentService _priorityAssignment;
    private readonly IValidator<StopShipmentRequest> _stopValidator;
    private readonly IValidator<ResumeShipmentRequest> _resumeValidator;
    private readonly IValidator<MarkArrivedRequest> _markArrivedValidator;
    private readonly IValidator<CancelShipmentRequest> _cancelValidator;
    private readonly IValidator<RegisterDeliveryAttemptRequest> _deliveryAttemptValidator;

    public ShipmentService(
        IShipmentRepository shipments,
        IOrderRepository orders,
        IRouteStopRepository routeStops,
        IUnitOfWork unitOfWork,
        IShipmentPriorityAssignmentService priorityAssignment,
        IValidator<StopShipmentRequest> stopValidator,
        IValidator<ResumeShipmentRequest> resumeValidator,
        IValidator<MarkArrivedRequest> markArrivedValidator,
        IValidator<CancelShipmentRequest> cancelValidator,
        IValidator<RegisterDeliveryAttemptRequest> deliveryAttemptValidator)
    {
        _shipments = shipments;
        _orders = orders;
        _routeStops = routeStops;
        _unitOfWork = unitOfWork;
        _priorityAssignment = priorityAssignment;
        _stopValidator = stopValidator;
        _resumeValidator = resumeValidator;
        _markArrivedValidator = markArrivedValidator;
        _cancelValidator = cancelValidator;
        _deliveryAttemptValidator = deliveryAttemptValidator;
    }

    public async Task<IReadOnlyList<ShipmentDelayRiskResponse>> GetPendingWithDelayRiskAsync(
        CancellationToken cancellationToken = default)
    {
        var shipments = await _shipments.GetPendingWithDriverAndRiskAsync(cancellationToken);

        return shipments
            .Select(ToDelayRiskResponse)
            .ToList();
    }

    public async Task<Guid> CreateAsync(Guid orderId, bool urgent = false, CancellationToken cancellationToken = default)
    {
        var order = await _orders.GetByIdAsync(orderId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), orderId);

        var shipment = Shipment.Create(order);

        if (urgent)
        {
            shipment.SetUrgent();
        }

        await _shipments.AddAsync(shipment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _priorityAssignment.RecalculatePrioritiesAsync(cancellationToken);

        return shipment.Id;
    }

    public async Task StartAsync(Guid shipmentId, CancellationToken cancellationToken = default)
    {
        var shipment = await GetShipmentAsync(shipmentId, cancellationToken);

        shipment.Start();

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task StopAsync(Guid shipmentId, StopShipmentRequest request, CancellationToken cancellationToken = default)
    {
        await _stopValidator.ValidateAndThrowAsync(request, cancellationToken);

        var shipment = await GetShipmentAsync(shipmentId, cancellationToken);
        var routeStop = await GetRouteStopOrDefaultAsync(request.RouteStopId, cancellationToken);

        shipment.Stop(routeStop, request.Note);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ResumeAsync(Guid shipmentId, ResumeShipmentRequest request, CancellationToken cancellationToken = default)
    {
        await _resumeValidator.ValidateAndThrowAsync(request, cancellationToken);

        var shipment = await GetShipmentAsync(shipmentId, cancellationToken);
        var routeStop = await GetRouteStopOrDefaultAsync(request.RouteStopId, cancellationToken);

        shipment.Resume(routeStop, request.Note);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkArrivedAsync(Guid shipmentId, MarkArrivedRequest request, CancellationToken cancellationToken = default)
    {
        await _markArrivedValidator.ValidateAndThrowAsync(request, cancellationToken);

        var shipment = await GetShipmentAsync(shipmentId, cancellationToken);

        shipment.MarkArrivedAtDestination(request.Note);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelAsync(Guid shipmentId, CancelShipmentRequest request, CancellationToken cancellationToken = default)
    {
        await _cancelValidator.ValidateAndThrowAsync(request, cancellationToken);

        var shipment = await GetShipmentAsync(shipmentId, cancellationToken);

        shipment.Cancel(request.Note);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RegisterDeliveryAttemptAsync(
        Guid shipmentId,
        RegisterDeliveryAttemptRequest request,
        CancellationToken cancellationToken = default)
    {
        await _deliveryAttemptValidator.ValidateAndThrowAsync(request, cancellationToken);

        var shipment = await _shipments.GetByIdWithDetailsAsync(shipmentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Shipment), shipmentId);

        var routeStop = await GetRouteStopOrDefaultAsync(request.RouteStopId, cancellationToken);

        shipment.RegisterDeliveryAttempt(routeStop, request.Succeeded, request.FailureReason, request.Note, request.AttemptedAt);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (shipment.Status is ShipmentStatus.DeliveryFailed or ShipmentStatus.Finalized)
        {
            await _priorityAssignment.RecalculatePrioritiesAsync(cancellationToken);
        }
    }

    private async Task<Shipment> GetShipmentAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        return await _shipments.GetByIdAsync(shipmentId, cancellationToken)
            ?? throw new NotFoundException(nameof(Shipment), shipmentId);
    }

    private async Task<RouteStop?> GetRouteStopOrDefaultAsync(Guid? routeStopId, CancellationToken cancellationToken)
    {
        if (routeStopId is not { } id)
        {
            return null;
        }

        return await _routeStops.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(RouteStop), id);
    }

    public async Task<IReadOnlyList<ShipmentSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var shipments = await _shipments.GetAllAsync(cancellationToken);

        return shipments.Select(s => new ShipmentSummaryDto(
            s.Id,
            s.Status,
            s.Priority,
            s.DelayRiskPercentage,
            s.OrderId,
            s.Order?.AssignedDriverId,
            s.RouteStop?.Id,
            s.RouteStop?.RouteId,
            s.CreatedAt,
            s.UpdatedAt)).ToList();
    }

    public async Task<ShipmentDetailsDto?> GetByIdAsync(Guid shipmentId, CancellationToken cancellationToken = default)
    {
        var s = await _shipments.GetByIdWithFullDetailsAsync(shipmentId, cancellationToken);

        if (s is null)
        {
            return null;
        }

        return new ShipmentDetailsDto(
            s.Id,
            s.Status,
            s.Priority,
            s.DelayRiskPercentage,
            s.ArrivedAtDestination,
            s.OrderId,
            s.Order.Items.Select(i => new ShipmentOrderItemDto(i.Id, i.Name, i.Quantity, i.WeightKg)).ToArray(),
            s.RouteStop is null
                ? null
                : new ShipmentRouteStopDto(
                    s.RouteStop.Id,
                    s.RouteStop.StopOrder,
                    s.RouteStop.RouteId,
                    s.RouteStop.Name,
                    s.RouteStop.Coordinate.Latitude,
                    s.RouteStop.Coordinate.Longitude,
                    s.RouteStop.DistanceMeters,
                    s.RouteStop.TollCost),
            s.Order.AssignedDriver is null
                ? null
                : new ShipmentDriverDto(s.Order.AssignedDriver.Id, s.Order.AssignedDriver.Name, s.Order.AssignedDriver.Surname),
            s.CreatedAt,
            s.UpdatedAt);
    }

    public async Task<IReadOnlyList<ShipmentHistoryDto>> GetHistoryAsync(Guid shipmentId, CancellationToken cancellationToken = default)
    {
        var history = await _shipments.GetHistoryAsync(shipmentId, cancellationToken);

        return history.Select(h => new ShipmentHistoryDto(
            h.Id,
            h.ShipmentId,
            h.Status,
            h.RouteStopId,
            h.Note,
            h.RecordedAt)).ToList();
    }

    public async Task<IReadOnlyList<DeliveryAttemptDto>> GetDeliveryAttemptsAsync(Guid shipmentId, CancellationToken cancellationToken = default)
    {
        var attempts = await _shipments.GetDeliveryAttemptsAsync(shipmentId, cancellationToken);

        return attempts.Select(d => new DeliveryAttemptDto(
            d.Id,
            d.ShipmentId,
            d.OrderId,
            d.RouteStopId,
            d.AttemptNumber,
            d.AttemptedAt,
            d.Succeeded,
            d.FailureReason,
            d.Note)).ToList();
    }

    private static ShipmentDelayRiskResponse ToDelayRiskResponse(Shipment shipment)
    {
        var driver = shipment.RouteStop?.Route?.Driver ?? shipment.Order.AssignedDriver;

        return new ShipmentDelayRiskResponse(
            shipment.Id,
            shipment.Status,
            shipment.Priority,
            shipment.DelayRiskPercentage,
            driver is null
                ? null
                : new ShipmentDriverResponse(driver.Id, driver.Name, driver.Surname),
            shipment.OrderId,
            shipment.Order.Items.Sum(i => i.WeightKg * i.Quantity),
            $"{shipment.Order.Customer.Name} {shipment.Order.Customer.Surname}".Trim());
    }
}