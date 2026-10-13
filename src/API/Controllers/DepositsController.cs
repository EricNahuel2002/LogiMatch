using Api.Authorization;
using Application.Dtos.Deposits;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/deposits")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class DepositsController : ControllerBase
{
    private readonly IDepositService _depositService;

    public DepositsController(IDepositService depositService)
    {
        _depositService = depositService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var deposits = await _depositService.GetAllAsync(cancellationToken);

        return Ok(deposits);
    }
}
