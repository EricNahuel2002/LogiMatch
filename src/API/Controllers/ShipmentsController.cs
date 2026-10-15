using Application.Dtos.Shipments;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Api.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Api.Controllers;

[ApiController]
[Route("api/shipments")]
public sealed class ShipmentsController : ControllerBase
{
    private readonly IShipmentService _shipmentService;
    private readonly IShipmentAssignmentService _shipmentAssignmentService;

    public ShipmentsController(
        IShipmentService shipmentService,
        IShipmentAssignmentService shipmentAssignmentService)
    {
        _shipmentService = shipmentService;
        _shipmentAssignmentService = shipmentAssignmentService;
    }

    [HttpGet("pending")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> GetPendingWithDelayRisk(CancellationToken cancellationToken)
    {
        var shipments = await _shipmentService.GetPendingWithDelayRiskAsync(cancellationToken);

        return Ok(shipments);
    }

    [HttpGet]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var shipments = await _shipmentService.GetAllAsync(cancellationToken);

        return Ok(shipments);
    }

    [HttpGet("{shipmentId:guid}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> GetById(Guid shipmentId, CancellationToken cancellationToken)
    {
        var shipment = await _shipmentService.GetByIdAsync(shipmentId, cancellationToken);

        return shipment is null ? NotFound() : Ok(shipment);
    }

    [HttpGet("{shipmentId:guid}/history")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> GetHistory(Guid shipmentId, CancellationToken cancellationToken)
    {
        var history = await _shipmentService.GetHistoryAsync(shipmentId, cancellationToken);

        return Ok(history);
    }

    [HttpGet("{shipmentId:guid}/delivery-attempts")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> GetDeliveryAttempts(Guid shipmentId, CancellationToken cancellationToken)
    {
        var attempts = await _shipmentService.GetDeliveryAttemptsAsync(shipmentId, cancellationToken);

        return Ok(attempts);
    }

    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> Create([FromQuery] Guid orderId, [FromQuery] bool urgent = false, CancellationToken cancellationToken = default)
    {
        var id = await _shipmentService.CreateAsync(orderId, urgent, cancellationToken);

        return Created($"/api/shipments/{id}", new { id });
    }

    [HttpPost("{shipmentId:guid}/start")]
    [Authorize(Policy = Policies.DriverOnly)]
    public async Task<IActionResult> Start(Guid shipmentId, CancellationToken cancellationToken)
    {
        var driverIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(driverIdClaim, out var driverId))
        {
            return Unauthorized();
        }

        await _shipmentService.StartAsync(driverId, shipmentId, cancellationToken);

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

    [HttpPost("{shipmentId:guid}/driver-suggestion")]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> SuggestDriver(Guid shipmentId, CancellationToken cancellationToken)
    {
        var suggestion = await _shipmentAssignmentService.SuggestDriverAsync(shipmentId, cancellationToken);

        return Ok(suggestion);
    }
}
