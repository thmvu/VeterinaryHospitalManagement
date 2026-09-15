using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeterinaryHospitalManagement.Web.Models.Entities;

namespace VeterinaryHospitalManagement.Web.Data.Configurations;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");

        builder.Property(permission => permission.Code)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(permission => permission.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.HasIndex(permission => permission.Code)
            .IsUnique();
    }
}
