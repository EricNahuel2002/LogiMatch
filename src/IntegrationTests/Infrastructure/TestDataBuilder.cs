using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IntegrationTests.Infrastructure;

public static class TestDataBuilder
{
    public static async Task<Order> CreateOrderAsync(
        LogiMatchDbContext db,
        IReadOnlyCollection<OrderItem> items,
        Guid? assignedDriverId = null,
        DateTime? deliveryWindowStartAt = null,
        DateTime? deliveryWindowEndAt = null)
    {
        var customer = await db.Users.OfType<Customer>().FirstAsync();
        var admin = await db.Users.OfType<Admin>().FirstAsync();

        var order = Order.Create(
            customer,
            admin,
            items,
            deliveryWindowStartAt,
            deliveryWindowEndAt);
        if (assignedDriverId is { } driverId)
        {
            order.SetAssignedDriver(driverId);
        }

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return order;
    }

    public static async Task<Shipment> CreatePendingShipmentAsync(
        LogiMatchDbContext db,
        IReadOnlyCollection<OrderItem> items,
        Guid? assignedDriverId = null,
        DateTime? deliveryWindowStartAt = null,
        DateTime? deliveryWindowEndAt = null)
    {
        var order = await CreateOrderAsync(
            db,
            items,
            assignedDriverId,
            deliveryWindowStartAt,
            deliveryWindowEndAt);
        var shipment = Shipment.Create(order);

        db.Shipments.Add(shipment);
        await db.SaveChangesAsync();

        return shipment;
    }

    public static async Task<Shipment> CreatePendingShipmentWithStopAsync(
        LogiMatchDbContext db,
        IReadOnlyCollection<OrderItem> items,
        Guid? assignedDriverId = null,
        DateTime? deliveryWindowStartAt = null,
        DateTime? deliveryWindowEndAt = null)
    {
        var shipment = await CreatePendingShipmentAsync(
            db,
            items,
            assignedDriverId,
            deliveryWindowStartAt,
            deliveryWindowEndAt);

        var stop = RouteStop.Create(
            shipment,
            new Coordinate(-34.6083m, -58.3816m),
            1,
            "Sucursal central");
        db.RouteStops.Add(stop);
        await db.SaveChangesAsync();

        return shipment;
    }

    /// <summary>
    /// Creates an active route assigned to the driver with one stop per shipment, in the given
    /// stop order. Used by tests that need the driver to have a route to work on.
    /// </summary>
    public static async Task<Route> CreateActiveRouteAsync(
        LogiMatchDbContext db,
        Guid driverId,
        params (Guid ShipmentId, int StopOrder)[] stops)
    {
        await DeactivateActiveRoutesAsync(db, driverId);

        var driver = await db.Users.OfType<Driver>().SingleAsync(d => d.Id == driverId);
        var route = Route.Create(new Coordinate(-34.6083m, -58.3816m));
        route.AssignDriver(driver);

        foreach (var (shipmentId, stopOrder) in stops)
        {
            var shipment = await db.Shipments.SingleAsync(s => s.Id == shipmentId);
            var stop = RouteStop.Create(shipment, new Coordinate(-34.6083m, -58.3816m), stopOrder);
            stop.AssignToRoute(route);
            route.AddStop(stop);
        }

        db.Routes.Add(route);
        await db.SaveChangesAsync();

        return route;
    }

    /// <summary>
    /// Deactivates every active route of the driver in bulk. The whole suite shares one database
    /// and driver endpoints resolve "the" active route with a single lookup, so a route left active
    /// by an earlier test would otherwise be picked instead of the one a later test created.
    /// </summary>
    public static Task<int> DeactivateActiveRoutesAsync(LogiMatchDbContext db, Guid driverId)
    {
        return db.Routes
            .Where(r => r.DriverId == driverId && r.IsActive)
            .ExecuteUpdateAsync(r => r.SetProperty(x => x.IsActive, false));
    }

    /// <summary>
    /// Rewrites the status of the given shipments in bulk. The domain transition is bypassed on
    /// purpose: this exists for tests that need a deterministic query over the database the whole
    /// suite shares, and any domain transition would leave shipment history behind that no later
    /// restore could undo. Callers must put the shipments back the way they found them.
    /// </summary>
    public static Task<int> SetShipmentStatusAsync(
        LogiMatchDbContext db,
        IReadOnlyCollection<Guid> shipmentIds,
        ShipmentStatus status)
    {
        if (shipmentIds.Count == 0)
        {
            return Task.FromResult(0);
        }

        return db.Shipments
            .Where(s => shipmentIds.Contains(s.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, status));
    }
}