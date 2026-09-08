using Domain.Entities;
using Domain.ValueObjects;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Persistence;

public static class DemoDataSeeder
{
    public const string DefaultPassword = "LogiMatch#2026";

    private const string TargetShipmentItemName = "Caja de repuestos";

    private static readonly Coordinate DepotOrigin = new(-34.6082m, -58.3784m);

    private static readonly string[] CustomerEmails = ["cliente@logimatch.com"];
    private static readonly string[] DriverEmails =
    [
        "chofer1@logimatch.com",
        "chofer2@logimatch.com",
        "chofer3@logimatch.com",
        "chofer4@logimatch.com",
        "chofer5@logimatch.com"
    ];

    public static async Task SeedAsync(
        IServiceProvider services,
        string? password,
        CancellationToken cancellationToken = default)
    {
        var db = services.GetRequiredService<LogiMatchDbContext>();
        var userManager = services.GetRequiredService<UserManager<User>>();

        var credential = password ?? DefaultPassword;

        var admin = await GetOrCreateAdminAsync(userManager, credential);
        var customer = await EnsureUserAsync(userManager, new Customer
        {
            UserName = CustomerEmails[0],
            Email = CustomerEmails[0],
            Name = "Lucas",
            Surname = "Martínez"
        }, credential, "Customer");

        var driver1 = await EnsureUserAsync(userManager, new Driver
        {
            UserName = DriverEmails[0],
            Email = DriverEmails[0],
            Name = "Carlos",
            Surname = "Pérez"
        }, credential, "Driver");

        var driver2 = await EnsureUserAsync(userManager, new Driver
        {
            UserName = DriverEmails[1],
            Email = DriverEmails[1],
            Name = "Ana",
            Surname = "Gómez"
        }, credential, "Driver");

        var driver3 = await EnsureUserAsync(userManager, new Driver
        {
            UserName = DriverEmails[2],
            Email = DriverEmails[2],
            Name = "Marcos",
            Surname = "Suárez"
        }, credential, "Driver");

        var driver4 = await EnsureUserAsync(userManager, new Driver
        {
            UserName = DriverEmails[3],
            Email = DriverEmails[3],
            Name = "Lucía",
            Surname = "Fernández"
        }, credential, "Driver");

        var driver5 = await EnsureUserAsync(userManager, new Driver
        {
            UserName = DriverEmails[4],
            Email = DriverEmails[4],
            Name = "Diego",
            Surname = "Ramírez"
        }, credential, "Driver");

        var driver1Vehicles = new[]
        {
            Vehicle.Create("AA123BB", 1500m, "Toyota", "Hilux"),
            Vehicle.Create("AB456CC", 1000m, "Ford", "Ranger")
        };

        var driver2Vehicles = new[]
        {
            Vehicle.Create("AC789DD", 1200m, "Renault", "Kangoo"),
            Vehicle.Create("AD321EE", 800m, "Fiat", "Fiorino")
        };

        var driver3Vehicles = new[]
        {
            Vehicle.Create("AE111FF", 1000m, "Chevrolet", "S10"),
            Vehicle.Create("AF222GG", 900m, "Nissan", "Frontier")
        };

        var driver4Vehicles = new[]
        {
            Vehicle.Create("AH333HH", 1100m, "Toyota", "Hilux")
        };

        var driver5Vehicles = new[]
        {
            Vehicle.Create("AI444JJ", 2000m, "Ford", "Ranger")
        };

        var existingPlates = new HashSet<string>(
            await db.Vehicles.Select(v => v.LicensePlate).ToListAsync(cancellationToken),
            StringComparer.OrdinalIgnoreCase);

        var newVehicles = new List<Vehicle>();

        foreach (var (driver, vehicles) in new[]
                 {
                     (driver1, driver1Vehicles),
                     (driver2, driver2Vehicles),
                     (driver3, driver3Vehicles),
                     (driver4, driver4Vehicles),
                     (driver5, driver5Vehicles)
                 })
        {
            foreach (var vehicle in vehicles)
            {
                if (!existingPlates.Contains(vehicle.LicensePlate))
                {
                    driver.Vehicles.Add(vehicle);
                    newVehicles.Add(vehicle);
                }
            }
        }

        if (driver1.CurrentLocation is null)
        {
            driver1.SetCurrentLocation(new Coordinate(-34.5885m, -58.4299m));
        }

        if (driver2.CurrentLocation is null)
        {
            driver2.SetCurrentLocation(new Coordinate(-34.6500m, -58.4700m));
        }

        if (driver3.CurrentLocation is null)
        {
            driver3.SetCurrentLocation(new Coordinate(-34.6200m, -58.4200m));
        }

        if (driver4.CurrentLocation is null)
        {
            driver4.SetCurrentLocation(new Coordinate(-34.6800m, -58.5200m));
        }

        if (driver5.CurrentLocation is null)
        {
            driver5.SetCurrentLocation(new Coordinate(-34.5700m, -58.4000m));
        }

        var demoSeeded = await db.Shipments.AnyAsync(
            s => s.Order.Items.Any(i => i.Name == TargetShipmentItemName),
            cancellationToken);

        if (!demoSeeded)
        {
            var shipments = new List<Shipment>();
            var routes = new List<Route>();

            var (targetShipment, _, targetRoute) = BuildShipment(
                customer, admin, null,
                TargetShipmentItemName, 250m, 1, 300m,
                "Av. Rivadavia 123", new Coordinate(-34.6083m, -58.3816m));
            shipments.Add(targetShipment);
            routes.Add(targetRoute);

            var (driver1Pending1, _, route1) = BuildShipment(
                customer, admin, driver1,
                "Bulto textil", 120m, 2, 150m,
                "Av. Corrientes 345", new Coordinate(-34.5711m, -58.4353m));
            shipments.Add(driver1Pending1);
            routes.Add(route1);

            var (driver1Pending2, _, route2) = BuildShipment(
                customer, admin, driver1,
                "Equipo de cómputo", 400m, 1, 80m,
                "Cerrito 512", new Coordinate(-34.6012m, -58.3847m));
            shipments.Add(driver1Pending2);
            routes.Add(route2);

            var (driver1Pending3, _, route3) = BuildShipment(
                customer, admin, driver1,
                "Mercadería general", 90m, 3, 200m,
                "Av. La Plata 120", new Coordinate(-34.6280m, -58.4450m));
            shipments.Add(driver1Pending3);
            routes.Add(route3);

            var (delivered, deliveredStop, route4) = BuildShipment(
                customer, admin, driver1,
                "Documentación", 80m, 2, 60m,
                "Diagonal Norte 730", new Coordinate(-34.6130m, -58.3770m));
            delivered.Start();
            delivered.MarkArrivedAtDestination();
            delivered.RegisterDeliveryAttempt(
                deliveredStop,
                succeeded: true,
                note: "Entregado al cliente",
                attemptedAt: DateTime.UtcNow);
            shipments.Add(delivered);
            routes.Add(route4);

            var (driver2InProgress, _, route5) = BuildShipment(
                customer, admin, driver2,
                "Pallet de bebidas", 400m, 1, 500m,
                "Av. Hipólito Yrigoyen 9000", new Coordinate(-34.6400m, -58.5050m));
            driver2InProgress.Start();
            shipments.Add(driver2InProgress);
            routes.Add(route5);

            var (driver4Pending1, _, route6) = BuildShipment(
                customer, admin, driver4,
                "Cajas de vidrio", 180m, 2, 120m,
                "Av. San Martín 2100", new Coordinate(-34.6980m, -58.5480m));
            shipments.Add(driver4Pending1);
            routes.Add(route6);

            var (driver4Pending2, _, route7) = BuildShipment(
                customer, admin, driver4,
                "Mercadería suelta", 60m, 4, 80m,
                "Av. Mitre 4300", new Coordinate(-34.7100m, -58.5600m));
            shipments.Add(driver4Pending2);
            routes.Add(route7);

            var (driver5InProgress, _, route8) = BuildShipment(
                customer, admin, driver5,
                "Carga pesada", 600m, 2, 700m,
                "Av. General Paz 1500", new Coordinate(-34.5900m, -58.4400m));
            driver5InProgress.Start();
            shipments.Add(driver5InProgress);
            routes.Add(route8);

            db.Routes.AddRange(routes);
            db.Shipments.AddRange(shipments);
        }

        db.Vehicles.AddRange(newVehicles);

        await db.SaveChangesAsync(cancellationToken);
    }

    private static (Shipment Shipment, RouteStop RouteStop, Route Route) BuildShipment(
        Customer customer,
        Admin admin,
        Driver? assignedDriver,
        string itemName,
        decimal price,
        int quantity,
        decimal weightKg,
        string stopName,
        Coordinate stopCoordinate)
    {
        var order = Order.Create(customer, admin, [OrderItem.Create(itemName, price, quantity, weightKg)]);

        if (assignedDriver is not null)
        {
            order.SetAssignedDriver(assignedDriver.Id);
        }

        var shipment = Shipment.Create(order);
        var route = Route.Create(new Coordinate(DepotOrigin.Latitude, DepotOrigin.Longitude));
        var routeStop = RouteStop.Create(shipment, stopCoordinate, 1, stopName);
        routeStop.AssignToRoute(route);
        route.AddStop(routeStop);

        return (shipment, routeStop, route);
    }

    private static async Task<Admin> GetOrCreateAdminAsync(UserManager<User> userManager, string credential)
    {
        const string adminEmail = "admin@logimatch.com";

        var existing = await userManager.FindByEmailAsync(adminEmail);
        if (existing is Admin admin)
        {
            return admin;
        }

        if (existing is not null)
        {
            throw new InvalidOperationException(
                $"The email '{adminEmail}' is already used by a {existing.GetType().Name} user.");
        }

        admin = new Admin
        {
            Id = Guid.NewGuid(),
            UserName = adminEmail,
            Email = adminEmail,
            Name = "Admin",
            Surname = "LogiMatch"
        };

        var created = await userManager.CreateAsync(admin, credential);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to seed admin: {FormatErrors(created.Errors)}");
        }

        await userManager.AddToRoleAsync(admin, "Admin");

        return admin;
    }

    private static async Task<TUser> EnsureUserAsync<TUser>(
        UserManager<User> userManager,
        TUser user,
        string credential,
        string role)
        where TUser : User
    {
        var existing = await userManager.FindByEmailAsync(user.Email!);
        if (existing is not null)
        {
            return (TUser)existing;
        }

        user.Id = Guid.NewGuid();

        var created = await userManager.CreateAsync(user, credential);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to seed {user.Email}: {FormatErrors(created.Errors)}");
        }

        await userManager.AddToRoleAsync(user, role);

        return user;
    }

    private static string FormatErrors(IEnumerable<IdentityError> errors)
    {
        return string.Join("; ", errors.Select(e => e.Description));
    }
}