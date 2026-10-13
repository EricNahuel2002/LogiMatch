using System.Security.Claims;
using Api.Authorization;
using Application.Dtos.Drivers;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/drivers")]
public sealed class DriversController : ControllerBase
{
    private readonly IDriverService _driverService;

    public DriversController(IDriverService driverService)
    {
        _driverService = driverService;
    }

    [HttpGet]
    [Authorize(Policy = Policies.AdminOnly)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var drivers = await _driverService.GetAllAsync(cancellationToken);

        return Ok(drivers);
    }

    [HttpGet("me/routes/current")]
    [Authorize(Policy = Policies.DriverOnly)]
    public async Task<IActionResult> GetMyCurrentRoute(CancellationToken cancellationToken)
    {
        var driverId = GetDriverId();
        if (driverId is not { } id)
        {
            return Unauthorized();
        }

        var route = await _driverService.GetCurrentRouteAsync(id, cancellationToken);

        return route is null ? NotFound() : Ok(route);
    }

    [HttpGet("me/shipments")]
    [Authorize(Policy = Policies.DriverOnly)]
    public async Task<IActionResult> GetMyShipments(CancellationToken cancellationToken)
    {
        var driverId = GetDriverId();
        if (driverId is not { } id)
        {
            return Unauthorized();
        }

        var shipments = await _driverService.GetShipmentsAsync(id, cancellationToken);

        return Ok(shipments);
    }

    [HttpPatch("me/location")]
    [Authorize(Policy = Policies.DriverOnly)]
    public async Task<IActionResult> UpdateMyLocation(
        UpdateDriverLocationRequest request,
        CancellationToken cancellationToken)
    {
        var driverId = GetDriverId();
        if (driverId is not { } id)
        {
            return Unauthorized();
        }

        await _driverService.UpdateLocationAsync(id, request, cancellationToken);

        return NoContent();
    }

    [HttpPost("me/routes/cancel")]
    [Authorize(Policy = Policies.DriverOnly)]
    public async Task<IActionResult> CancelMyRoute(CancelRouteRequest request, CancellationToken cancellationToken)
    {
        var driverId = GetDriverId();
        if (driverId is not { } id)
        {
            return Unauthorized();
        }

        await _driverService.CancelCurrentRouteAsync(id, request, cancellationToken);

        return NoContent();
    }

    private Guid? GetDriverId()
    {
        var driverIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(driverIdClaim, out var driverId) ? driverId : null;
    }
}
