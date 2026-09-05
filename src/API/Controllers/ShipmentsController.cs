using Api.Authorization;
using Application.Dtos.Shipments;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/shipments")]
public sealed class ShipmentsController : ControllerBase
{
    private readonly IShipmentService _shipmentService;

    public ShipmentsController(IShipmentService shipmentService)
    {
        _shipmentService = shipmentService;
    }

    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Create([FromQuery] Guid orderId, CancellationToken cancellationToken)
    {
        var id = await _shipmentService.CreateAsync(orderId, cancellationToken);

        return Created($"/api/shipments/{id}", new { id });
    }

    [HttpPost("{shipmentId:guid}/start")]
    [Authorize(Policy = Policies.DriverOnly)]
    public async Task<IActionResult> Start(Guid shipmentId, CancellationToken cancellationToken)
    {
        await _shipmentService.StartAsync(shipmentId, cancellationToken);

        return NoContent();
    }

    [HttpPost("{shipmentId:guid}/stop")]
    [Authorize(Policy = Policies.DriverOnly)]
    public async Task<IActionResult> Stop(Guid shipmentId, StopShipmentRequest request, CancellationToken cancellationToken)
    {
        await _shipmentService.StopAsync(shipmentId, request, cancellationToken);

        return NoContent();
    }

    [HttpPost("{shipmentId:guid}/resume")]
    [Authorize(Policy = Policies.DriverOnly)]
    public async Task<IActionResult> Resume(Guid shipmentId, ResumeShipmentRequest request, CancellationToken cancellationToken)
    {
        await _shipmentService.ResumeAsync(shipmentId, request, cancellationToken);

        return NoContent();
    }

    [HttpPost("{shipmentId:guid}/arrived")]
    [Authorize(Policy = Policies.DriverOnly)]
    public async Task<IActionResult> MarkArrived(Guid shipmentId, MarkArrivedRequest request, CancellationToken cancellationToken)
    {
        await _shipmentService.MarkArrivedAsync(shipmentId, request, cancellationToken);

        return NoContent();
    }

    [HttpPost("{shipmentId:guid}/cancel")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Cancel(Guid shipmentId, CancelShipmentRequest request, CancellationToken cancellationToken)
    {
        await _shipmentService.CancelAsync(shipmentId, request, cancellationToken);

        return NoContent();
    }

    [HttpPost("{shipmentId:guid}/delivery-attempts")]
    [Authorize(Policy = Policies.DriverOnly)]
    public async Task<IActionResult> RegisterDeliveryAttempt(
        Guid shipmentId,
        RegisterDeliveryAttemptRequest request,
        CancellationToken cancellationToken)
    {
        await _shipmentService.RegisterDeliveryAttemptAsync(shipmentId, request, cancellationToken);

        return NoContent();
    }
}