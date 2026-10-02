using Api.Authorization;
using Application.Dtos.RoutePlanning;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Read-only route planning preview. Nothing here is persisted: the response is a proposal
/// that an admin can review before committing anything. The shipments are not chosen by the
/// caller: the preview always works over the pending ones and reports back the ones it had to
/// leave out.
/// </summary>
[ApiController]
[Route("api/route-planning")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class RoutePlanningController : ControllerBase
{
    private readonly IRoutePlanningService _routePlanningService;

    public RoutePlanningController(IRoutePlanningService routePlanningService)
    {
        _routePlanningService = routePlanningService;
    }

    [HttpPost("preview")]
    public async Task<IActionResult> Preview(
        PlanRoutesRequest request,
        CancellationToken cancellationToken)
    {
        var proposal = await _routePlanningService.PreviewAsync(request, cancellationToken);
        return Ok(proposal);
    }
}
