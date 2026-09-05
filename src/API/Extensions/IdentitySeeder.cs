using Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Extensions;

public static class IdentitySeeder
{
    private static readonly string[] RoleNames = ["Admin", "Driver", "Customer"];

    public static async Task SeedAsync(IServiceProvider services, string? adminEmail, string? adminPassword)
    {
        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            return;
        }

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        foreach (var roleName in RoleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            }
        }

        var userManager = services.GetRequiredService<UserManager<User>>();

        if (await userManager.FindByEmailAsync(adminEmail) is not null)
        {
            return;
        }

        var admin = new Admin
        {
            UserName = adminEmail,
            Email = adminEmail,
            Name = "Admin",
            Surname = "LogiMatch"
        };

        var result = await userManager.CreateAsync(admin, adminPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, "Admin");
        }
    }
}