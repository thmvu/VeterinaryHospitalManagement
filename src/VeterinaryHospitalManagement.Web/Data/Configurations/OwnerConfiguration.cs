using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeterinaryHospitalManagement.Web.Models.Entities;

namespace VeterinaryHospitalManagement.Web.Data.Configurations;

public sealed class OwnerConfiguration : IEntityTypeConfiguration<Owner>
{
    public void Configure(EntityTypeBuilder<Owner> builder)
    {
        builder.ToTable(
            "Owners",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_Owners_OwnerCode_Format",
                    "DATALENGTH([OwnerCode]) = 20 AND [OwnerCode] COLLATE Latin1_General_100_BIN2 LIKE N'OWN-[0-9][0-9][0-9][0-9][0-9][0-9]'");
                table.HasCheckConstraint(
                    "CK_Owners_PhoneNumber_Canonical",
                    "DATALENGTH([PhoneNumber]) = 24 AND [PhoneNumber] COLLATE Latin1_General_100_BIN2 LIKE N'+84[35789][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'");
            });

        builder.Property(owner => owner.OwnerCode).HasMaxLength(10).IsRequired();
        builder.Property(owner => owner.FullName).HasMaxLength(150).IsRequired();
        builder.Property(owner => owner.PhoneNumber).HasMaxLength(20).IsRequired();
        builder.Property(owner => owner.Email).HasMaxLength(254);
        builder.Property(owner => owner.Address).HasMaxLength(500);
        builder.Property(owner => owner.IsActive).HasDefaultValue(true).IsRequired();
        builder.Property(owner => owner.CreatedAt).HasColumnType("datetimeoffset(7)").IsRequired();
        builder.Property(owner => owner.RowVersion).IsRowVersion().HasColumnType("rowversion");

        builder.HasIndex(owner => owner.OwnerCode).IsUnique();
        builder.HasIndex(owner => owner.PhoneNumber);
    }
}
