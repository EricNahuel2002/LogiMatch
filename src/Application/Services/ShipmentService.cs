using Application.Dtos.Shipments;
using Application.Exceptions;
using Application.Persistence;
using Domain.Entities;
using FluentValidation;

namespace Application.Services;

public class ShipmentService : IShipmentService
{
    private readonly IShipmentRepository _shipments;
    private readonly IOrderRepository _orders;
    private readonly IRouteStopRepository _routeStops;
    private readonly IUnitOfWork _unitOfWork;
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
        _stopValidator = stopValidator;
        _resumeValidator = resumeValidator;
        _markArrivedValidator = markArrivedValidator;
        _cancelValidator = cancelValidator;
        _deliveryAttemptValidator = deliveryAttemptValidator;
    }

    public async Task<Guid> CreateAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await _orders.GetByIdAsync(orderId, cancellationToken)
            ?? throw new NotFoundException(nameof(Order), orderId);

        var shipment = Shipment.Create(order);

        await _shipments.AddAsync(shipment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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

        shipment.RegisterDeliveryAttempt(routeStop, request.Succeeded, request.Note, request.AttemptedAt);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
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
}