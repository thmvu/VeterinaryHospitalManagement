using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeterinaryHospitalManagement.Web.Models.Entities;

namespace VeterinaryHospitalManagement.Web.Data.Configurations;

public sealed class VeterinarianProfileConfiguration : IEntityTypeConfiguration<VeterinarianProfile>
{
    public void Configure(EntityTypeBuilder<VeterinarianProfile> builder)
    {
        builder.ToTable("VeterinarianProfiles");
        builder.Property(x => x.UserId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.DoctorCode).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Specialty).HasMaxLength(150);
        builder.Property(x => x.IsActive).HasDefaultValue(true).IsRequired();
        builder.Property(x => x.RowVersion).IsRowVersion().HasColumnType("rowversion");
        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasIndex(x => x.DoctorCode).IsUnique();
        builder.HasOne(x => x.User).WithOne().HasForeignKey<VeterinarianProfile>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
