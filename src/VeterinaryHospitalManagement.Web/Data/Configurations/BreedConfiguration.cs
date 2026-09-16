using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeterinaryHospitalManagement.Web.Models.Entities;

namespace VeterinaryHospitalManagement.Web.Data.Configurations;

public sealed class BreedConfiguration : IEntityTypeConfiguration<Breed>
{
    public void Configure(EntityTypeBuilder<Breed> builder)
    {
        builder.ToTable("Breeds");
        builder.Property(breed => breed.Name).HasMaxLength(100).IsRequired();
        builder.Property(breed => breed.IsActive).HasDefaultValue(true).IsRequired();

        builder.HasAlternateKey(breed => new { breed.Id, breed.SpeciesId });
        builder.HasIndex(breed => new { breed.SpeciesId, breed.Name }).IsUnique();

        builder.HasOne(breed => breed.Species)
            .WithMany(species => species.Breeds)
            .HasForeignKey(breed => breed.SpeciesId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
