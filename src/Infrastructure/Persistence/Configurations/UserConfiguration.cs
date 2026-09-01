using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasDiscriminator<string>("UserType")
            .HasValue<Customer>(nameof(Customer))
            .HasValue<Driver>(nameof(Driver))
            .HasValue<Admin>(nameof(Admin));

        builder.Property(u => u.Name).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Surname).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(256).IsRequired();

        builder.HasIndex(u => u.Email).IsUnique();
    }
}