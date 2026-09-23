using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeterinaryHospitalManagement.Web.Models.Entities;

namespace VeterinaryHospitalManagement.Web.Data.Configurations;

public sealed class ServiceCatalogConfiguration : IEntityTypeConfiguration<ServiceCatalog>
{
    public void Configure(EntityTypeBuilder<ServiceCatalog> builder)
    {
        builder.ToTable(
            "ServiceCatalogs",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_ServiceCatalogs_Price",
                    "[Price] >= 0");
            });

        builder.Property(x => x.Code)
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(x => x.Code)
            .IsUnique()
            .HasDatabaseName("UQ_ServiceCatalogs_Code");

        builder.Property(x => x.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.Category)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Price)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.RowVersion)
            .IsRowVersion()
            .HasColumnType("rowversion");
    }
}
