using Domain.Entities;
using Infrastructure.Persistence.Interceptors;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class LogiMatchDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public LogiMatchDbContext(DbContextOptions<LogiMatchDbContext> options)
        : base(options)
    {
    }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<DeliveryAttempt> DeliveryAttempts => Set<DeliveryAttempt>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<ShipmentHistory> ShipmentHistory => Set<ShipmentHistory>();
    public DbSet<Route> Routes => Set<Route>();
    public DbSet<RouteStop> RouteStops => Set<RouteStop>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public override int SaveChanges()
    {
        NormalizeNewHistoryEntries();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        NormalizeNewHistoryEntries();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        NormalizeNewHistoryEntries();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        NormalizeNewHistoryEntries();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void NormalizeNewHistoryEntries()
    {
        // ShipmentHistory is immutable after creation. EF Core flags a ShipmentHistory
        // added through the Shipment.History navigation as Modified instead of Added,
        // because its snapshot is taken before relationship fixup fills the foreign key.
        // A row loaded from the database never has CLR-default original values, so any
        // Modified entry whose original snapshot still contains defaults was created in
        // this context and must be inserted.
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries<ShipmentHistory>())
        {
            if (entry.State != EntityState.Modified)
            {
                continue;
            }

            var hasDefaultOriginal = entry.Properties.Any(p =>
                Equals(p.OriginalValue, GetDefaultValue(p.Metadata.ClrType))
                && !Equals(p.OriginalValue, p.CurrentValue));

            if (hasDefaultOriginal)
            {
                entry.State = EntityState.Added;
            }
        }
    }

    private static object? GetDefaultValue(Type type)
    {
        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LogiMatchDbContext).Assembly);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(new AuditableEntityInterceptor());
    }
}