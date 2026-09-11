using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure.Persistence;

public class LogiMatchDbContextFactory : IDesignTimeDbContextFactory<LogiMatchDbContext>
{
    public LogiMatchDbContext CreateDbContext(string[] args)
    {
        var configured = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Server=localhost\\SQLEXPRESS;Trusted_Connection=True;TrustServerCertificate=True;Database=LogiMatchDB";

        var options = new DbContextOptionsBuilder<LogiMatchDbContext>()
            .UseSqlServer(configured)
            .Options;

        return new LogiMatchDbContext(options);
    }
}