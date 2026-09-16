using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeterinaryHospitalManagement.Web.Models.Entities;

namespace VeterinaryHospitalManagement.Web.Data.Configurations;

public sealed class SpeciesConfiguration : IEntityTypeConfiguration<Species>
{
    public void Configure(EntityTypeBuilder<Species> builder)
    {
        builder.ToTable("Species");
        builder.Property(species => species.Code).HasMaxLength(30).IsRequired();
        builder.Property(species => species.Name).HasMaxLength(100).IsRequired();
        builder.Property(species => species.IsActive).HasDefaultValue(true).IsRequired();
        builder.HasIndex(species => species.Code).IsUnique();
    }
}
