using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeterinaryHospitalManagement.Web.Models.Entities;

namespace VeterinaryHospitalManagement.Web.Data.Configurations;

public sealed class MedicalRecordConfiguration : IEntityTypeConfiguration<MedicalRecord>
{
    public void Configure(EntityTypeBuilder<MedicalRecord> builder)
    {
        builder.ToTable("MedicalRecords", table =>
        {
            table.HasCheckConstraint("CK_MedicalRecords_WeightKg", "[WeightKg] IS NULL OR [WeightKg] > 0");
            table.HasCheckConstraint("CK_MedicalRecords_Status",
                "([Status] = 'Draft' AND [FinalizedAt] IS NULL AND [FinalizedByVeterinarianId] IS NULL) OR " +
                "([Status] = 'Finalized' AND [FinalizedAt] IS NOT NULL AND [FinalizedByVeterinarianId] IS NOT NULL " +
                "AND [Diagnosis] IS NOT NULL AND LEN(LTRIM(RTRIM([Diagnosis]))) > 0)");
        });
        builder.Property(x => x.ChiefComplaint).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.Symptoms).HasColumnType("nvarchar(max)");
        builder.Property(x => x.WeightKg).HasColumnType("decimal(6,2)");
        builder.Property(x => x.TemperatureC).HasColumnType("decimal(4,1)");
        builder.Property(x => x.Diagnosis).HasColumnType("nvarchar(max)");
        builder.Property(x => x.TreatmentNotes).HasColumnType("nvarchar(max)");
        builder.Property(x => x.FollowUpDate).HasColumnType("date");
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.FinalizedAt).HasColumnType("datetimeoffset(7)");
        builder.Property(x => x.RowVersion).IsRowVersion().HasColumnType("rowversion");
        builder.HasIndex(x => x.VisitId).IsUnique();
        builder.HasOne(x => x.Visit).WithOne(x => x.MedicalRecord).HasForeignKey<MedicalRecord>(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FinalizedByVeterinarian).WithMany().HasForeignKey(x => x.FinalizedByVeterinarianId).OnDelete(DeleteBehavior.Restrict);
    }
}
