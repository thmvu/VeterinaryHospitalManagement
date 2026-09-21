using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeterinaryHospitalManagement.Web.Models.Entities;

namespace VeterinaryHospitalManagement.Web.Data.Configurations;

public sealed class VeterinarianShiftConfiguration : IEntityTypeConfiguration<VeterinarianShift>
{
    public void Configure(EntityTypeBuilder<VeterinarianShift> builder)
    {
        builder.ToTable(
            "VeterinarianShifts",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_VeterinarianShifts_TimeRange",
                    "[StartAt] < [EndAt]");
            });

        builder.Property(x => x.StartAt)
            .HasColumnType("datetimeoffset(7)")
            .IsRequired();

        builder.Property(x => x.EndAt)
            .HasColumnType("datetimeoffset(7)")
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(x => x.RowVersion)
            .IsRowVersion()
            .HasColumnType("rowversion");

        // Composite index for availability queries: filter by vet + active, order by StartAt, INCLUDE EndAt
        builder.HasIndex(x => new { x.VeterinarianId, x.IsActive, x.StartAt })
            .HasDatabaseName("IX_VeterinarianShifts_VeterinarianId_IsActive_StartAt")
            .IncludeProperties(x => x.EndAt);

        builder.HasOne(x => x.Veterinarian)
            .WithMany(v => v.Shifts)
            .HasForeignKey(x => x.VeterinarianId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
