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
        var driverIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(driverIdClaim, out var driverId))
        {
            return Unauthorized();
        }

        await _driverService.UpdateLocationAsync(driverId, request, cancellationToken);

        return NoContent();
    }
}