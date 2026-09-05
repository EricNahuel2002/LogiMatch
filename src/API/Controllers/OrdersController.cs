using Api.Authorization;
using Application.Dtos.Orders;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var id = await _orderService.CreateAsync(request, cancellationToken);

        return Created($"/api/orders/{id}", new { id });
    }

    [HttpPost("{orderId:guid}/items")]
    public async Task<IActionResult> AddItem(Guid orderId, CreateOrderItemRequest request, CancellationToken cancellationToken)
    {
        await _orderService.AddItemAsync(orderId, request, cancellationToken);

        return NoContent();
    }

    [HttpPatch("{orderId:guid}/driver")]
    public async Task<IActionResult> AssignDriver(Guid orderId, AssignOrderDriverRequest request, CancellationToken cancellationToken)
    {
        await _orderService.AssignDriverAsync(orderId, request, cancellationToken);

        return NoContent();
    }
}