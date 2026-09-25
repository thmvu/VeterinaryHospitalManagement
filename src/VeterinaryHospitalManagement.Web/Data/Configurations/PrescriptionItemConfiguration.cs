using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeterinaryHospitalManagement.Web.Models.Entities;

namespace VeterinaryHospitalManagement.Web.Data.Configurations;

public sealed class PrescriptionItemConfiguration : IEntityTypeConfiguration<PrescriptionItem>
{
    public void Configure(EntityTypeBuilder<PrescriptionItem> builder)
    {
        builder.ToTable("PrescriptionItems", table =>
            table.HasCheckConstraint("CK_PrescriptionItems_Quantity", "[Quantity] > 0"));
        builder.Property(x => x.MedicineNameSnapshot).HasMaxLength(150).IsRequired();
        builder.Property(x => x.UnitSnapshot).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Dosage).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Route).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Frequency).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Duration).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Quantity).HasColumnType("decimal(10,2)").IsRequired();
        builder.Property(x => x.Instructions).HasMaxLength(1000);
        builder.HasOne(x => x.Prescription).WithMany(x => x.Items).HasForeignKey(x => x.PrescriptionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Medicine).WithMany().HasForeignKey(x => x.MedicineId).OnDelete(DeleteBehavior.Restrict);
    }
}
