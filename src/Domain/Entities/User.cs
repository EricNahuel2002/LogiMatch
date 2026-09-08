using Domain.Common;
using Microsoft.AspNetCore.Identity;

namespace Domain.Entities;

public abstract class User : IdentityUser<Guid>, IAuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Surname { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}