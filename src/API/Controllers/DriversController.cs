using System.Security.Claims;
using Api.Authorization;
using Application.Dtos.Drivers;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/drivers")]
[Authorize(Policy = Policies.DriverOnly)]
public sealed class DriversController : ControllerBase
{
    private readonly IDriverService _driverService;

    public DriversController(IDriverService driverService)
    {
        _driverService = driverService;
    }

    [HttpPatch("me/location")]
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