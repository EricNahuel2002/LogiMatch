using Api.Authorization;
using Application.Dtos.RouteStops;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/route-stops")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class RouteStopsController : ControllerBase
{
    private readonly IRouteStopService _routeStopService;

    public RouteStopsController(IRouteStopService routeStopService)
    {
        _routeStopService = routeStopService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateRouteStopRequest request, CancellationToken cancellationToken)
    {
        var id = await _routeStopService.CreateAsync(request, cancellationToken);

        return Created($"/api/route-stops/{id}", new { id });
    }
}