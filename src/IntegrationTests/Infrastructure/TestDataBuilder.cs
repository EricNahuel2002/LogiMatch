using Domain.Entities;
using Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IntegrationTests.Infrastructure;

public static class TestDataBuilder
{
    public static async Task<Order> CreateOrderAsync(
        LogiMatchDbContext db,
        IReadOnlyCollection<OrderItem> items,
        Guid? assignedDriverId = null)
    {
        var customer = await db.Users.OfType<Customer>().FirstAsync();
        var admin = await db.Users.OfType<Admin>().FirstAsync();

        var order = Order.Create(customer, admin, items);
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
        Guid? assignedDriverId = null)
    {
        var order = await CreateOrderAsync(db, items, assignedDriverId);
        var shipment = Shipment.Create(order);

        db.Shipments.Add(shipment);
        await db.SaveChangesAsync();

        return shipment;
    }

    public static async Task<Shipment> CreatePendingShipmentWithStopAsync(
        LogiMatchDbContext db,
        IReadOnlyCollection<OrderItem> items,
        Guid? assignedDriverId = null)
    {
        var shipment = await CreatePendingShipmentAsync(db, items, assignedDriverId);

        var stop = RouteStop.Create(
            shipment,
            new Coordinate(-34.6083m, -58.3816m),
            1,
            "Sucursal central");
        db.RouteStops.Add(stop);
        await db.SaveChangesAsync();

        return shipment;
    }
}