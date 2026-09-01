using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure.Persistence;

public class LogiMatchDbContextFactory : IDesignTimeDbContextFactory<LogiMatchDbContext>
{
    public LogiMatchDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Server=localhost;Database=LogiMatchDB;Trusted_Connection=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<LogiMatchDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new LogiMatchDbContext(options);
    }
}