using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeterinaryHospitalManagement.Web.Models.Entities;

namespace VeterinaryHospitalManagement.Web.Data.Configurations;

public sealed class PetConfiguration : IEntityTypeConfiguration<Pet>
{
    public void Configure(EntityTypeBuilder<Pet> builder)
    {
        builder.ToTable(
            "Pets",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_Pets_PetCode_Format",
                    "DATALENGTH([PetCode]) = 20 AND [PetCode] COLLATE Latin1_General_100_BIN2 LIKE N'PET-[0-9][0-9][0-9][0-9][0-9][0-9]'");
                table.HasCheckConstraint(
                    "CK_Pets_Sex",
                    "[Sex] IN ('Unknown', 'Male', 'Female')");
            });

        builder.Property(pet => pet.PetCode).HasMaxLength(10).IsRequired();
        builder.Property(pet => pet.Name).HasMaxLength(100).IsRequired();
        builder.Property(pet => pet.Sex).HasConversion<string>().HasColumnType("nvarchar(20)").HasMaxLength(20).IsRequired();
        builder.Property(pet => pet.BirthDate).HasColumnType("date");
        builder.Property(pet => pet.Color).HasMaxLength(100);
        builder.Property(pet => pet.Notes).HasMaxLength(1000);
        builder.Property(pet => pet.IsActive).HasDefaultValue(true).IsRequired();
        builder.Property(pet => pet.CreatedAt).HasColumnType("datetimeoffset(7)").IsRequired();
        builder.Property(pet => pet.RowVersion).IsRowVersion().HasColumnType("rowversion");

        builder.HasIndex(pet => pet.PetCode).IsUnique();

        builder.HasOne(pet => pet.Owner)
            .WithMany(owner => owner.Pets)
            .HasForeignKey(pet => pet.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pet => pet.Species)
            .WithMany(species => species.Pets)
            .HasForeignKey(pet => pet.SpeciesId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(pet => pet.Breed)
            .WithMany(breed => breed.Pets)
            .HasForeignKey(pet => new { pet.BreedId, pet.SpeciesId })
            .HasPrincipalKey(breed => new { breed.Id, breed.SpeciesId })
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}
