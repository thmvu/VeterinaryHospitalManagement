using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VeterinaryHospitalManagement.Tests.Infrastructure.Identity;
using VeterinaryHospitalManagement.Web.Models.Entities;
using VeterinaryHospitalManagement.Web.Models.Enums;

namespace VeterinaryHospitalManagement.Tests.Integration.OwnersPets;

[Collection(IdentitySqlServerTestCollection.Name)]
public sealed class SqlServerOwnerPetSchemaTests
{
    [IdentitySqlServerFact]
    public async Task AddOwnersAndPets_migrates_latest_down_and_up_on_the_exact_test_database()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        await using var context = IdentitySqlServerTestEnvironment.CreateContext();
        var migrator = context.GetService<IMigrator>();
        var initialMigration = FindMigration(context, "_InitialIdentityAndPermissions");
        var ownerPetMigration = FindMigration(context, "_AddOwnersAndPets");

        Assert.Contains(ownerPetMigration, await context.Database.GetAppliedMigrationsAsync());
        Assert.True(await TableExistsAsync(context, "Owners"));
        Assert.True(await TableExistsAsync(context, "Pets"));

        await migrator.MigrateAsync(initialMigration);

        Assert.False(await TableExistsAsync(context, "Owners"));
        Assert.False(await TableExistsAsync(context, "Species"));
        Assert.False(await TableExistsAsync(context, "Breeds"));
        Assert.False(await TableExistsAsync(context, "Pets"));

        await migrator.MigrateAsync(ownerPetMigration);
        await migrator.MigrateAsync(ownerPetMigration);

        Assert.Contains(ownerPetMigration, await context.Database.GetAppliedMigrationsAsync());
        Assert.True(await TableExistsAsync(context, "Owners"));
        Assert.True(await TableExistsAsync(context, "Species"));
        Assert.True(await TableExistsAsync(context, "Breeds"));
        Assert.True(await TableExistsAsync(context, "Pets"));
    }

    [IdentitySqlServerFact]
    public async Task Database_enforces_owner_phone_code_and_pet_classification_contracts()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();

        var invalidPhones = new (string Code, string PhoneNumber)[]
        {
            ("OWN-800001", "84912345678"),
            ("OWN-800002", "+84 912345678"),
            ("OWN-800003", "+8491234567A"),
            ("OWN-800004", "+8491234567１"),
            ("OWN-800005", "+84212345678"),
            ("OWN-800006", "+8491234567"),
            ("OWN-800007", "+84912345678 "),
            ("OWN-800008", "0912345678")
        };
        foreach (var invalidPhone in invalidPhones)
        {
            await using var context = IdentitySqlServerTestEnvironment.CreateContext();
            context.Owners.Add(CreateOwner(invalidPhone.Code, invalidPhone.PhoneNumber));
            await AssertSqlErrorAsync(() => context.SaveChangesAsync(), 547);
        }

        var invalidOwnerCodes = new[]
        {
            "PET-800001",
            "OWN-80001",
            "OWN-80000１",
            "OWN-80000A"
        };
        foreach (var invalidOwnerCode in invalidOwnerCodes)
        {
            await using var context = IdentitySqlServerTestEnvironment.CreateContext();
            context.Owners.Add(CreateOwner(invalidOwnerCode, "+84912345678"));
            await AssertSqlErrorAsync(() => context.SaveChangesAsync(), 547);
        }

        await using (var setup = IdentitySqlServerTestEnvironment.CreateContext())
        {
            setup.Owners.AddRange(
                CreateOwner("OWN-000001", "+84912345678"),
                CreateOwner("OWN-000002", "+84912345678"));
            setup.Species.AddRange(
                new Species { Code = "DOG", Name = "Chó" },
                new Species { Code = "CAT", Name = "Mèo" });
            await setup.SaveChangesAsync();
        }

        await using (var duplicateOwnerCode = IdentitySqlServerTestEnvironment.CreateContext())
        {
            duplicateOwnerCode.Owners.Add(CreateOwner("OWN-000001", "+84812345678"));
            await AssertSqlErrorAsync(() => duplicateOwnerCode.SaveChangesAsync(), 2601, 2627);
        }

        await using (var duplicateSpeciesCode = IdentitySqlServerTestEnvironment.CreateContext())
        {
            duplicateSpeciesCode.Species.Add(new Species { Code = "DOG", Name = "Chó khác" });
            await AssertSqlErrorAsync(() => duplicateSpeciesCode.SaveChangesAsync(), 2601, 2627);
        }

        int ownerId;
        int dogId;
        int catId;
        int dogBreedId;
        await using (var catalog = IdentitySqlServerTestEnvironment.CreateContext())
        {
            Assert.Equal(2, await catalog.Owners.CountAsync(owner => owner.PhoneNumber == "+84912345678"));
            ownerId = await catalog.Owners.Where(owner => owner.OwnerCode == "OWN-000001").Select(owner => owner.Id).SingleAsync();
            dogId = await catalog.Species.Where(species => species.Code == "DOG").Select(species => species.Id).SingleAsync();
            catId = await catalog.Species.Where(species => species.Code == "CAT").Select(species => species.Id).SingleAsync();
            var dogBreed = new Breed { SpeciesId = dogId, Name = "Poodle" };
            var catBreedWithSameName = new Breed { SpeciesId = catId, Name = "Poodle" };
            catalog.Breeds.AddRange(dogBreed, catBreedWithSameName);
            await catalog.SaveChangesAsync();
            dogBreedId = dogBreed.Id;
        }

        await using (var duplicateBreedName = IdentitySqlServerTestEnvironment.CreateContext())
        {
            duplicateBreedName.Breeds.Add(new Breed { SpeciesId = dogId, Name = "Poodle" });
            await AssertSqlErrorAsync(() => duplicateBreedName.SaveChangesAsync(), 2601, 2627);
        }

        await using (var validPets = IdentitySqlServerTestEnvironment.CreateContext())
        {
            validPets.Pets.AddRange(
                CreatePet("PET-000001", ownerId, dogId, dogBreedId, PetSex.Male),
                CreatePet("PET-000002", ownerId, catId, null, PetSex.Unknown));
            await validPets.SaveChangesAsync();
        }

        await using (var duplicatePetCode = IdentitySqlServerTestEnvironment.CreateContext())
        {
            duplicatePetCode.Pets.Add(CreatePet("PET-000001", ownerId, catId, null, PetSex.Female));
            await AssertSqlErrorAsync(() => duplicatePetCode.SaveChangesAsync(), 2601, 2627);
        }

        var invalidPetCodes = new[]
        {
            "OWN-800001",
            "PET-80001",
            "PET-80000１",
            "PET-80000A"
        };
        foreach (var invalidPetCode in invalidPetCodes)
        {
            await using var context = IdentitySqlServerTestEnvironment.CreateContext();
            context.Pets.Add(CreatePet(invalidPetCode, ownerId, catId, null, PetSex.Unknown));
            await AssertSqlErrorAsync(() => context.SaveChangesAsync(), 547);
        }

        await using (var mismatchedBreed = IdentitySqlServerTestEnvironment.CreateContext())
        {
            mismatchedBreed.Pets.Add(CreatePet("PET-000003", ownerId, catId, dogBreedId, PetSex.Female));
            await AssertSqlErrorAsync(() => mismatchedBreed.SaveChangesAsync(), 547);
        }

        await using (var invalidSex = IdentitySqlServerTestEnvironment.CreateContext())
        {
            invalidSex.Pets.Add(CreatePet("PET-000004", ownerId, catId, null, (PetSex)99));
            await AssertSqlErrorAsync(() => invalidSex.SaveChangesAsync(), 547);
        }
    }

    [IdentitySqlServerFact]
    public async Task Owner_and_pet_rowversions_reject_stale_updates()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();

        int ownerId;
        int petId;
        await using (var setup = IdentitySqlServerTestEnvironment.CreateContext())
        {
            var owner = CreateOwner("OWN-000001", "+84912345678");
            var species = new Species { Code = "DOG", Name = "Chó" };
            setup.AddRange(owner, species);
            await setup.SaveChangesAsync();
            var pet = CreatePet("PET-000001", owner.Id, species.Id, null, PetSex.Unknown);
            setup.Pets.Add(pet);
            await setup.SaveChangesAsync();
            ownerId = owner.Id;
            petId = pet.Id;
            Assert.Equal(8, owner.RowVersion.Length);
            Assert.Equal(8, pet.RowVersion.Length);
        }

        await using (var first = IdentitySqlServerTestEnvironment.CreateContext())
        await using (var stale = IdentitySqlServerTestEnvironment.CreateContext())
        {
            var firstOwner = await first.Owners.SingleAsync(owner => owner.Id == ownerId);
            var staleOwner = await stale.Owners.SingleAsync(owner => owner.Id == ownerId);
            var oldVersion = firstOwner.RowVersion.ToArray();
            firstOwner.FullName = "Owner updated first";
            await first.SaveChangesAsync();
            Assert.NotEqual(oldVersion, firstOwner.RowVersion);
            staleOwner.FullName = "Owner stale update";
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
        }

        await using (var first = IdentitySqlServerTestEnvironment.CreateContext())
        await using (var stale = IdentitySqlServerTestEnvironment.CreateContext())
        {
            var firstPet = await first.Pets.SingleAsync(pet => pet.Id == petId);
            var stalePet = await stale.Pets.SingleAsync(pet => pet.Id == petId);
            var oldVersion = firstPet.RowVersion.ToArray();
            firstPet.Name = "Pet updated first";
            await first.SaveChangesAsync();
            Assert.NotEqual(oldVersion, firstPet.RowVersion);
            stalePet.Name = "Pet stale update";
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
        }
    }

    private static Owner CreateOwner(string code, string phoneNumber) => new()
    {
        OwnerCode = code,
        FullName = $"Owner {code}",
        PhoneNumber = phoneNumber,
        CreatedAt = new DateTimeOffset(2026, 9, 16, 0, 0, 0, TimeSpan.Zero)
    };

    private static Pet CreatePet(
        string code,
        int ownerId,
        int speciesId,
        int? breedId,
        PetSex sex) => new()
    {
        PetCode = code,
        OwnerId = ownerId,
        Name = $"Pet {code}",
        SpeciesId = speciesId,
        BreedId = breedId,
        Sex = sex,
        CreatedAt = new DateTimeOffset(2026, 9, 16, 0, 0, 0, TimeSpan.Zero)
    };

    private static string FindMigration(DbContext context, string suffix) =>
        Assert.Single(
            context.Database.GetMigrations(),
            migration => migration.EndsWith(suffix, StringComparison.Ordinal));

    private static async Task<bool> TableExistsAsync(DbContext context, string tableName)
    {
        var connection = (SqlConnection)context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync();
        }
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CASE WHEN OBJECT_ID(@tableName, N'U') IS NULL THEN 0 ELSE 1 END";
        command.Parameters.AddWithValue("@tableName", tableName);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    private static async Task AssertSqlErrorAsync(Func<Task> operation, params int[] expectedNumbers)
    {
        var exception = await Assert.ThrowsAsync<DbUpdateException>(operation);
        var sqlException = Assert.IsType<SqlException>(exception.InnerException);
        Assert.Contains(sqlException.Number, expectedNumbers);
    }
}
