using System.Security.Claims;
using Api.Authorization;
using Application.Dtos.Drivers;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

[Authorize(Policy = Policies.DriverOnly)]
public sealed class DriverHub : Hub
{
    public const string RouteAssignedEvent = "RouteAssigned";
    public const string RouteCancelledEvent = "RouteCancelled";

    private readonly IDriverService _driverService;

    public DriverHub(IDriverService driverService)
    {
        _driverService = driverService;
    }

    public static string GroupName(Guid driverId) => $"driver:{driverId}";

    public override async Task OnConnectedAsync()
    {
        if (TryGetDriverId(out var driverId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(driverId));
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (TryGetDriverId(out var driverId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(driverId));
        }

        await base.OnDisconnectedAsync(exception);
    }

    public Task UpdateLocation(decimal latitude, decimal longitude)
    {
        if (!TryGetDriverId(out var driverId))
        {
            throw new HubException("The authenticated user is not a driver.");
        }

        return _driverService.UpdateLocationAsync(
            driverId,
            new UpdateDriverLocationRequest { Latitude = latitude, Longitude = longitude },
            Context.ConnectionAborted);
    }

    private bool TryGetDriverId(out Guid driverId)
    {
        var claim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out driverId);
    }
}
