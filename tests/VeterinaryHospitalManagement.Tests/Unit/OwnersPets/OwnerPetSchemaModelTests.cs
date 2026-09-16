using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using VeterinaryHospitalManagement.Web.Data;

namespace VeterinaryHospitalManagement.Tests.Unit.OwnersPets;

public sealed class OwnerPetSchemaModelTests
{
    private const string EntitiesNamespace =
        "VeterinaryHospitalManagement.Web.Models.Entities";

    [Fact]
    public void Owner_has_required_fields_indexes_defaults_and_rowversion()
    {
        using var context = CreateContext();
        var owner = RequireEntity(context, $"{EntitiesNamespace}.Owner");

        Assert.Equal("Owners", owner.GetTableName());
        AssertProperty(owner, "Id", typeof(int), nullable: false);
        AssertProperty(owner, "OwnerCode", typeof(string), nullable: false, maxLength: 10);
        AssertProperty(owner, "FullName", typeof(string), nullable: false, maxLength: 150);
        AssertProperty(owner, "PhoneNumber", typeof(string), nullable: false, maxLength: 20);
        AssertProperty(owner, "Email", typeof(string), nullable: true, maxLength: 254);
        AssertProperty(owner, "Address", typeof(string), nullable: true, maxLength: 500);
        AssertProperty(owner, "IsActive", typeof(bool), nullable: false, defaultValue: true);
        AssertProperty(owner, "CreatedAt", typeof(DateTimeOffset), nullable: false, columnType: "datetimeoffset(7)");
        AssertRowVersion(owner, "RowVersion");
        AssertIndex(owner, unique: true, "OwnerCode");
        AssertIndex(owner, unique: false, "PhoneNumber");
        var ownerCodeConstraint = Assert.Single(
            owner.GetCheckConstraints(),
            constraint => constraint.Name == "CK_Owners_OwnerCode_Format");
        Assert.Equal(
            "DATALENGTH([OwnerCode]) = 20 AND [OwnerCode] COLLATE Latin1_General_100_BIN2 LIKE N'OWN-[0-9][0-9][0-9][0-9][0-9][0-9]'",
            ownerCodeConstraint.Sql);
        var phoneConstraint = Assert.Single(
            owner.GetCheckConstraints(),
            constraint => constraint.Name == "CK_Owners_PhoneNumber_Canonical");
        Assert.Contains("DATALENGTH([PhoneNumber]) = 24", phoneConstraint.Sql, StringComparison.Ordinal);
        Assert.Contains("COLLATE Latin1_General_100_BIN2", phoneConstraint.Sql, StringComparison.Ordinal);
        Assert.Contains("+84", phoneConstraint.Sql, StringComparison.Ordinal);
        Assert.Contains("[35789]", phoneConstraint.Sql, StringComparison.Ordinal);
        Assert.Contains("[0-9]", phoneConstraint.Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Species_has_unique_code_but_nonunique_name()
    {
        using var context = CreateContext();
        var species = RequireEntity(context, $"{EntitiesNamespace}.Species");

        Assert.Equal("Species", species.GetTableName());
        AssertProperty(species, "Id", typeof(int), nullable: false);
        AssertProperty(species, "Code", typeof(string), nullable: false, maxLength: 30);
        AssertProperty(species, "Name", typeof(string), nullable: false, maxLength: 100);
        AssertProperty(species, "IsActive", typeof(bool), nullable: false, defaultValue: true);
        AssertIndex(species, unique: true, "Code");
        Assert.DoesNotContain(
            species.GetIndexes(),
            index => index.IsUnique && index.Properties.Select(property => property.Name).SequenceEqual(["Name"]));
    }

    [Fact]
    public void Breed_is_unique_within_species_and_exposes_the_composite_alternate_key()
    {
        using var context = CreateContext();
        var breed = RequireEntity(context, $"{EntitiesNamespace}.Breed");

        Assert.Equal("Breeds", breed.GetTableName());
        AssertProperty(breed, "Id", typeof(int), nullable: false);
        AssertProperty(breed, "SpeciesId", typeof(int), nullable: false);
        AssertProperty(breed, "Name", typeof(string), nullable: false, maxLength: 100);
        AssertProperty(breed, "IsActive", typeof(bool), nullable: false, defaultValue: true);
        AssertIndex(breed, unique: true, "SpeciesId", "Name");
        Assert.Contains(
            breed.GetKeys(),
            key => !key.IsPrimaryKey()
                && key.Properties.Select(property => property.Name).SequenceEqual(["Id", "SpeciesId"]));
        AssertForeignKey(
            breed,
            dependentProperties: ["SpeciesId"],
            principalEntityName: $"{EntitiesNamespace}.Species",
            principalProperties: ["Id"],
            required: true);
    }

    [Fact]
    public void Pet_has_required_fields_enum_check_unique_code_and_rowversion()
    {
        using var context = CreateContext();
        var pet = RequireEntity(context, $"{EntitiesNamespace}.Pet");

        Assert.Equal("Pets", pet.GetTableName());
        AssertProperty(pet, "Id", typeof(int), nullable: false);
        AssertProperty(pet, "PetCode", typeof(string), nullable: false, maxLength: 10);
        AssertProperty(pet, "OwnerId", typeof(int), nullable: false);
        AssertProperty(pet, "Name", typeof(string), nullable: false, maxLength: 100);
        AssertProperty(pet, "SpeciesId", typeof(int), nullable: false);
        AssertProperty(pet, "BreedId", typeof(int?), nullable: true);
        var sex = AssertProperty(pet, "Sex", nullable: false, maxLength: 20, columnType: "nvarchar(20)");
        Assert.True(sex.ClrType.IsEnum);
        Assert.Equal(["Unknown", "Male", "Female"], Enum.GetNames(sex.ClrType));
        AssertProperty(pet, "BirthDate", typeof(DateOnly?), nullable: true, columnType: "date");
        AssertProperty(pet, "Color", typeof(string), nullable: true, maxLength: 100);
        AssertProperty(pet, "Notes", typeof(string), nullable: true, maxLength: 1000);
        AssertProperty(pet, "IsActive", typeof(bool), nullable: false, defaultValue: true);
        AssertProperty(pet, "CreatedAt", typeof(DateTimeOffset), nullable: false, columnType: "datetimeoffset(7)");
        AssertRowVersion(pet, "RowVersion");
        AssertIndex(pet, unique: true, "PetCode");
        var petCodeConstraint = Assert.Single(
            pet.GetCheckConstraints(),
            constraint => constraint.Name == "CK_Pets_PetCode_Format");
        Assert.Equal(
            "DATALENGTH([PetCode]) = 20 AND [PetCode] COLLATE Latin1_General_100_BIN2 LIKE N'PET-[0-9][0-9][0-9][0-9][0-9][0-9]'",
            petCodeConstraint.Sql);
        var sexConstraint = Assert.Single(
            pet.GetCheckConstraints(),
            constraint => constraint.Name == "CK_Pets_Sex");
        Assert.Contains("'Unknown'", sexConstraint.Sql, StringComparison.Ordinal);
        Assert.Contains("'Male'", sexConstraint.Sql, StringComparison.Ordinal);
        Assert.Contains("'Female'", sexConstraint.Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Pet_uses_separate_species_fk_and_optional_composite_breed_fk_with_restrict_delete()
    {
        using var context = CreateContext();
        var pet = RequireEntity(context, $"{EntitiesNamespace}.Pet");

        AssertForeignKey(
            pet,
            dependentProperties: ["OwnerId"],
            principalEntityName: $"{EntitiesNamespace}.Owner",
            principalProperties: ["Id"],
            required: true);
        AssertForeignKey(
            pet,
            dependentProperties: ["SpeciesId"],
            principalEntityName: $"{EntitiesNamespace}.Species",
            principalProperties: ["Id"],
            required: true);
        AssertForeignKey(
            pet,
            dependentProperties: ["BreedId", "SpeciesId"],
            principalEntityName: $"{EntitiesNamespace}.Breed",
            principalProperties: ["Id", "SpeciesId"],
            required: false);
    }

    [Fact]
    public void Owner_and_pet_code_sequences_are_bounded_noncyclic_bigint_sequences()
    {
        using var context = CreateContext();

        AssertSequence(context, "OwnerCodeSequence");
        AssertSequence(context, "PetCodeSequence");
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=VeterinaryHospitalManagement_Test;Integrated Security=True;Encrypt=True;TrustServerCertificate=True")
            .Options;
        return new ApplicationDbContext(options);
    }

    private static IEntityType RequireEntity(ApplicationDbContext context, string name)
    {
        var entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(name);
        Assert.True(entity is not null, $"Expected entity '{name}' to be mapped.");
        return entity!;
    }

    private static IProperty AssertProperty(
        IEntityType entity,
        string propertyName,
        bool nullable,
        int? maxLength = null,
        string? columnType = null) =>
        AssertProperty(entity, propertyName, clrType: null, nullable, maxLength, columnType);

    private static IProperty AssertProperty(
        IEntityType entity,
        string propertyName,
        Type? clrType,
        bool nullable,
        int? maxLength = null,
        string? columnType = null,
        object? defaultValue = null)
    {
        var property = entity.FindProperty(propertyName);
        Assert.NotNull(property);
        if (clrType is not null)
        {
            Assert.Equal(clrType, property.ClrType);
        }
        Assert.Equal(nullable, property.IsNullable);
        Assert.Equal(maxLength, property.GetMaxLength());
        if (columnType is not null)
        {
            Assert.Equal(columnType, property.GetColumnType());
        }
        if (defaultValue is not null)
        {
            Assert.Equal(defaultValue, property.GetDefaultValue());
        }
        return property;
    }

    private static void AssertRowVersion(IEntityType entity, string propertyName)
    {
        var property = AssertProperty(entity, propertyName, typeof(byte[]), nullable: false, columnType: "rowversion");
        Assert.True(property.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, property.ValueGenerated);
    }

    private static void AssertIndex(IEntityType entity, bool unique, params string[] propertyNames)
    {
        var index = Assert.Single(
            entity.GetIndexes(),
            candidate => candidate.Properties.Select(property => property.Name).SequenceEqual(propertyNames));
        Assert.Equal(unique, index.IsUnique);
    }

    private static void AssertForeignKey(
        IEntityType entity,
        string[] dependentProperties,
        string principalEntityName,
        string[] principalProperties,
        bool required)
    {
        var foreignKey = Assert.Single(
            entity.GetForeignKeys(),
            candidate => candidate.Properties.Select(property => property.Name).SequenceEqual(dependentProperties));
        Assert.Equal(principalEntityName, foreignKey.PrincipalEntityType.Name);
        Assert.Equal(principalProperties, foreignKey.PrincipalKey.Properties.Select(property => property.Name));
        Assert.Equal(required, foreignKey.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }

    private static void AssertSequence(ApplicationDbContext context, string name)
    {
        var sequence = context.Model.FindSequence(name);
        Assert.NotNull(sequence);
        Assert.Equal(typeof(long), sequence.Type);
        Assert.Equal(1, sequence.StartValue);
        Assert.Equal(1, sequence.IncrementBy);
        Assert.Equal(1, sequence.MinValue);
        Assert.Equal(999999, sequence.MaxValue);
        Assert.False(sequence.IsCyclic);
    }
}
