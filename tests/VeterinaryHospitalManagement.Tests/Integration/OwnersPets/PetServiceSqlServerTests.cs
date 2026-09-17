using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Tests.Infrastructure.Identity;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Entities;
using VeterinaryHospitalManagement.Web.Models.Enums;
using VeterinaryHospitalManagement.Web.Services.Pets;
using VeterinaryHospitalManagement.Web.Services.Time;

namespace VeterinaryHospitalManagement.Tests.Integration.OwnersPets;

[Collection(IdentitySqlServerTestCollection.Name)]
public sealed class PetServiceSqlServerTests
{
    [IdentitySqlServerFact]
    public async Task Create_assigns_sequential_codes_and_enforces_active_species_and_breed()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        var actorId = await CreateActorUserAsync();

        int ownerId;
        int speciesId;
        int breedId;

        await using (var context = IdentitySqlServerTestEnvironment.CreateContext())
        {
            var owner = new Owner
            {
                OwnerCode = "OWN-000001",
                FullName = "Chủ Thú Cưng",
                PhoneNumber = "+84912345678",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            var species = new Species { Code = "DOG", Name = "Chó", IsActive = true };
            context.Owners.Add(owner);
            context.Species.Add(species);
            await context.SaveChangesAsync();

            ownerId = owner.Id;
            speciesId = species.Id;

            var breed = new Breed { SpeciesId = speciesId, Name = "Golden Retriever", IsActive = true };
            context.Breeds.Add(breed);
            await context.SaveChangesAsync();
            breedId = breed.Id;
        }

        string firstPetCode;
        string secondPetCode;
        await using (var context = IdentitySqlServerTestEnvironment.CreateContext())
        {
            var service = CreateService(context);
            firstPetCode = await service.CreateAsync(new CreatePetRequest(
                actorId, ownerId, "Lucky", speciesId, breedId, PetSex.Male, new DateOnly(2023, 1, 1), "Vàng", "Khỏe"));
            secondPetCode = await service.CreateAsync(new CreatePetRequest(
                actorId, ownerId, "Milo", speciesId, null, PetSex.Female, null, "Trắng", null));
        }

        Assert.Equal("PET-000001", firstPetCode);
        Assert.Equal("PET-000002", secondPetCode);

        await using (var verify = IdentitySqlServerTestEnvironment.CreateContext())
        {
            var pets = await verify.Pets.OrderBy(p => p.PetCode).ToListAsync();
            Assert.Equal(2, pets.Count);
            Assert.Equal("Lucky", pets[0].Name);
            Assert.Equal("Milo", pets[1].Name);

            var audits = await verify.AuditLogs.Where(a => a.EntityName == "Pet").ToListAsync();
            Assert.Equal(2, audits.Count);
            Assert.All(audits, a => Assert.Equal("Pet.Created", a.Action));
        }
    }

    [IdentitySqlServerFact]
    public async Task Create_rejects_inactive_species_and_inactive_breed()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        var actorId = await CreateActorUserAsync();

        int ownerId;
        int activeSpeciesId;
        int inactiveSpeciesId;
        int inactiveBreedId;

        await using (var context = IdentitySqlServerTestEnvironment.CreateContext())
        {
            var owner = new Owner
            {
                OwnerCode = "OWN-000001",
                FullName = "Chủ Thú Cưng",
                PhoneNumber = "+84912345678",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            var activeSpecies = new Species { Code = "CAT", Name = "Mèo", IsActive = true };
            var inactiveSpecies = new Species { Code = "BIRD", Name = "Chim", IsActive = false };
            context.Owners.Add(owner);
            context.Species.AddRange(activeSpecies, inactiveSpecies);
            await context.SaveChangesAsync();

            ownerId = owner.Id;
            activeSpeciesId = activeSpecies.Id;
            inactiveSpeciesId = inactiveSpecies.Id;

            var inactiveBreed = new Breed { SpeciesId = activeSpeciesId, Name = "Mèo Rừng", IsActive = false };
            context.Breeds.Add(inactiveBreed);
            await context.SaveChangesAsync();
            inactiveBreedId = inactiveBreed.Id;
        }

        await using (var context = IdentitySqlServerTestEnvironment.CreateContext())
        {
            var service = CreateService(context);

            // Inactive species rejected
            var ex1 = await Assert.ThrowsAsync<PetManagementException>(() => service.CreateAsync(
                new CreatePetRequest(actorId, ownerId, "Vẹt", inactiveSpeciesId, null, PetSex.Unknown, null, null, null)));
            Assert.Contains("ngừng hoạt động", ex1.Message);

            // Inactive breed rejected
            var ex2 = await Assert.ThrowsAsync<PetManagementException>(() => service.CreateAsync(
                new CreatePetRequest(actorId, ownerId, "Miu", activeSpeciesId, inactiveBreedId, PetSex.Female, null, null, null)));
            Assert.Contains("ngừng hoạt động", ex2.Message);
        }
    }

    [IdentitySqlServerFact]
    public async Task Update_with_stale_rowversion_reports_conflict_and_keeps_latest_data()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        var actorId = await CreateActorUserAsync();

        int petId;
        PetDetails created;

        await using (var context = IdentitySqlServerTestEnvironment.CreateContext())
        {
            var owner = new Owner
            {
                OwnerCode = "OWN-000001",
                FullName = "Chủ Thú Cưng",
                PhoneNumber = "+84912345678",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            var species = new Species { Code = "DOG", Name = "Chó", IsActive = true };
            context.Owners.Add(owner);
            context.Species.Add(species);
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var petCode = await service.CreateAsync(new CreatePetRequest(
                actorId, owner.Id, "Bé Lu", species.Id, null, PetSex.Male, null, "Đen", null));
            petId = await context.Pets.Where(p => p.PetCode == petCode).Select(p => p.Id).SingleAsync();
            created = (await service.FindAsync(petId))!;
        }

        // Concurrent update in another context
        await using (var concurrentContext = IdentitySqlServerTestEnvironment.CreateContext())
        {
            var concurrentService = CreateService(concurrentContext);
            await concurrentService.UpdateAsync(new UpdatePetRequest(
                actorId, petId, created.RowVersion, "Bé Lu Mới", created.SpeciesId, null, PetSex.Male, null, "Đen nâu", null));
        }

        // Attempting to update with original stale rowversion must fail
        await using (var staleContext = IdentitySqlServerTestEnvironment.CreateContext())
        {
            var staleService = CreateService(staleContext);
            await Assert.ThrowsAsync<PetConcurrencyException>(() => staleService.UpdateAsync(new UpdatePetRequest(
                actorId, petId, created.RowVersion, "Bé Lu Cũ", created.SpeciesId, null, PetSex.Male, null, "Đen", null)));
        }
    }

    [IdentitySqlServerFact]
    public async Task SetActive_toggles_pet_and_writes_audit_log()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        var actorId = await CreateActorUserAsync();

        int petId;
        PetDetails created;

        await using (var context = IdentitySqlServerTestEnvironment.CreateContext())
        {
            var owner = new Owner
            {
                OwnerCode = "OWN-000001",
                FullName = "Chủ Thú Cưng",
                PhoneNumber = "+84912345678",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            var species = new Species { Code = "CAT", Name = "Mèo", IsActive = true };
            context.Owners.Add(owner);
            context.Species.Add(species);
            await context.SaveChangesAsync();

            var service = CreateService(context);
            var petCode = await service.CreateAsync(new CreatePetRequest(
                actorId, owner.Id, "Tom", species.Id, null, PetSex.Male, null, null, null));
            petId = await context.Pets.Where(p => p.PetCode == petCode).Select(p => p.Id).SingleAsync();
            created = (await service.FindAsync(petId))!;

            await service.SetActiveAsync(new PetActivationRequest(actorId, petId, created.RowVersion, false));
        }

        await using (var verify = IdentitySqlServerTestEnvironment.CreateContext())
        {
            var pet = await verify.Pets.SingleAsync(p => p.Id == petId);
            Assert.False(pet.IsActive);

            var audit = await verify.AuditLogs.SingleAsync(a => a.EntityName == "Pet" && a.Action == "Pet.Deactivated");
            Assert.Equal(petId.ToString(), audit.EntityId);
        }
    }

    private static PetService CreateService(ApplicationDbContext context) =>
        new(context, TimeProvider.System, new VietnamTimeProvider(TimeProvider.System));

    private static async Task<string> CreateActorUserAsync()
    {
        await using var context = IdentitySqlServerTestEnvironment.CreateContext();
        var user = new ApplicationUser
        {
            UserName = "pet-service-actor@example.test",
            NormalizedUserName = "PET-SERVICE-ACTOR@EXAMPLE.TEST",
            Email = "pet-service-actor@example.test",
            NormalizedEmail = "PET-SERVICE-ACTOR@EXAMPLE.TEST",
            EmailConfirmed = true,
            FullName = "Pet Service Actor",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            SecurityStamp = "pet-service-actor-stamp",
            ConcurrencyStamp = "pet-service-actor-concurrency"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user.Id;
    }
}
