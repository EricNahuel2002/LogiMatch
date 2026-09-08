using Api.Authorization;
using Application.Dtos.Routes;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/routes")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class RoutesController : ControllerBase
{
    private readonly IRouteService _routeService;

    public RoutesController(IRouteService routeService)
    {
        _routeService = routeService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateRouteRequest request, CancellationToken cancellationToken)
    {
        var id = await _routeService.CreateAsync(request, cancellationToken);

        return Created($"/api/routes/{id}", new { id });
    }

    [HttpPatch("{routeId:guid}/driver")]
    public async Task<IActionResult> AssignDriver(Guid routeId, AssignRouteDriverRequest request, CancellationToken cancellationToken)
    {
        await _routeService.AssignDriverAsync(routeId, request.DriverId, cancellationToken);

        return NoContent();
    }

    [HttpPatch("{routeId:guid}/vehicle")]
    public async Task<IActionResult> AssignVehicle(Guid routeId, AssignRouteVehicleRequest request, CancellationToken cancellationToken)
    {
        await _routeService.AssignVehicleAsync(routeId, request.VehicleId, cancellationToken);

        return NoContent();
    }

    [HttpPost("{routeId:guid}/stops/{routeStopId:guid}")]
    public async Task<IActionResult> AddStop(Guid routeId, Guid routeStopId, CancellationToken cancellationToken)
    {
        await _routeService.AddStopAsync(routeId, routeStopId, cancellationToken);

        return NoContent();
    }
}