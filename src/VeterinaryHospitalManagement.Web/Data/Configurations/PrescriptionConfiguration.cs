using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeterinaryHospitalManagement.Web.Models.Entities;

namespace VeterinaryHospitalManagement.Web.Data.Configurations;

public sealed class PrescriptionConfiguration : IEntityTypeConfiguration<Prescription>
{
    public void Configure(EntityTypeBuilder<Prescription> builder)
    {
        builder.ToTable("Prescriptions", table => table.HasCheckConstraint("CK_Prescriptions_Status",
            "([Status] = 'Draft' AND [FinalizedAt] IS NULL) OR " +
            "([Status] = 'Finalized' AND [FinalizedAt] IS NOT NULL AND [FinalizedAt] >= [CreatedAt])"));
        builder.Property(x => x.Instructions).HasMaxLength(2000);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnType("datetimeoffset(7)").IsRequired();
        builder.Property(x => x.FinalizedAt).HasColumnType("datetimeoffset(7)");
        builder.Property(x => x.RowVersion).IsRowVersion().HasColumnType("rowversion");
        builder.HasIndex(x => x.VisitId).IsUnique();
        builder.HasOne(x => x.Visit).WithOne(x => x.Prescription).HasForeignKey<Prescription>(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
    }
}
