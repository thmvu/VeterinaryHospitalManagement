using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeterinaryHospitalManagement.Web.Models.Entities;

namespace VeterinaryHospitalManagement.Web.Data.Configurations;

public sealed class VisitServiceConfiguration : IEntityTypeConfiguration<VisitService>
{
    public void Configure(EntityTypeBuilder<VisitService> builder)
    {
        builder.ToTable("VisitServices", table =>
        {
            table.HasCheckConstraint("CK_VisitServices_Quantity", "[Quantity] > 0");
            table.HasCheckConstraint("CK_VisitServices_UnitPrice", "[UnitPrice] >= 0 AND [UnitPrice] = FLOOR([UnitPrice])");
            table.HasCheckConstraint("CK_VisitServices_Status",
                "([Status] = 'Pending' AND [PerformedAt] IS NULL AND [PerformedByVeterinarianId] IS NULL AND [CancellationReason] IS NULL) OR " +
                "([Status] = 'Performed' AND [PerformedAt] IS NOT NULL AND [PerformedByVeterinarianId] IS NOT NULL AND [CancellationReason] IS NULL) OR " +
                "([Status] = 'Cancelled' AND [PerformedAt] IS NULL AND [PerformedByVeterinarianId] IS NULL AND " +
                "[CancellationReason] IS NOT NULL AND LEN(LTRIM(RTRIM([CancellationReason]))) > 0)");
        });
        builder.Property(x => x.ServiceNameSnapshot).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Quantity).HasColumnType("decimal(10,2)").IsRequired();
        builder.Property(x => x.UnitPrice).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.PerformedAt).HasColumnType("datetimeoffset(7)");
        builder.Property(x => x.CancellationReason).HasMaxLength(500);
        builder.Property(x => x.CreatedAt).HasColumnType("datetimeoffset(7)").IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion().HasColumnType("rowversion");
        builder.HasOne(x => x.Visit).WithMany(x => x.Services).HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ServiceCatalog).WithMany().HasForeignKey(x => x.ServiceCatalogId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PerformedByVeterinarian).WithMany().HasForeignKey(x => x.PerformedByVeterinarianId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.VisitId, x.Status });
    }
}
