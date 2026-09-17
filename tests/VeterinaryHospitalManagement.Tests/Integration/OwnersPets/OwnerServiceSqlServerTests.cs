using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Tests.Infrastructure.Identity;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Entities;
using VeterinaryHospitalManagement.Web.Services.Owners;

namespace VeterinaryHospitalManagement.Tests.Integration.OwnersPets;

[Collection(IdentitySqlServerTestCollection.Name)]
public sealed class OwnerServiceSqlServerTests
{
    [IdentitySqlServerFact]
    public async Task Create_assigns_sequential_codes_and_canonicalizes_shared_phone_numbers()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        var actorId = await CreateActorUserAsync();

        string firstCode;
        string secondCode;
        await using (var context = IdentitySqlServerTestEnvironment.CreateContext())
        {
            var service = CreateService(context);
            firstCode = await service.CreateAsync(new CreateOwnerRequest(
                actorId, "Nguyễn Văn A", "0912 345 678", null, "Hà Nội"));
            secondCode = await service.CreateAsync(new CreateOwnerRequest(
                actorId, "Trần Thị B", "+84.912.345.678", "b@example.test", null));
        }

        Assert.Equal("OWN-000001", firstCode);
        Assert.Equal("OWN-000002", secondCode);

        await using (var verify = IdentitySqlServerTestEnvironment.CreateContext())
        {
            var phones = await verify.Owners
                .OrderBy(owner => owner.OwnerCode)
                .Select(owner => owner.PhoneNumber)
                .ToListAsync();
            Assert.Equal(["+84912345678", "+84912345678"], phones);
        }

        await using (var searchContext = IdentitySqlServerTestEnvironment.CreateContext())
        {
            var service = CreateService(searchContext);
            var results = await service.SearchAsync(new OwnerSearchCriteria("84-912-345-678", null));
            Assert.Equal(2, results.Count);
            Assert.Equal(["OWN-000001", "OWN-000002"], results.Select(owner => owner.OwnerCode).ToList());
            Assert.All(results, owner => Assert.Equal(0, owner.TotalPets));
        }
    }

    [IdentitySqlServerFact]
    public async Task Create_rejects_invalid_phone_and_writes_nothing()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        var actorId = await CreateActorUserAsync();

        await using var context = IdentitySqlServerTestEnvironment.CreateContext();
        var service = CreateService(context);

        var exception = await Assert.ThrowsAnyAsync<OwnerManagementException>(() => service.CreateAsync(
            new CreateOwnerRequest(actorId, "Nguyễn Văn C", "0912345678A", null, null)));
        Assert.Contains("Số điện thoại", exception.Message);

        var unsupported = await Assert.ThrowsAnyAsync<OwnerManagementException>(() => service.CreateAsync(
            new CreateOwnerRequest(actorId, "Nguyễn Văn C", "12345", null, null)));
        Assert.Contains("số di động Việt Nam", unsupported.Message);

        Assert.Equal(0, await context.Owners.CountAsync());
    }

    [IdentitySqlServerFact]
    public async Task Search_by_owner_code_returns_only_that_owner()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        var actorId = await CreateActorUserAsync();

        await using (var context = IdentitySqlServerTestEnvironment.CreateContext())
        {
            var service = CreateService(context);
            await service.CreateAsync(new CreateOwnerRequest(actorId, "Nguyễn Văn A", "0912345678", null, null));
            await service.CreateAsync(new CreateOwnerRequest(actorId, "Trần Thị B", "0912345678", null, null));
        }

        await using (var context = IdentitySqlServerTestEnvironment.CreateContext())
        {
            var service = CreateService(context);
            var results = await service.SearchAsync(new OwnerSearchCriteria(null, "own-000002"));
            var result = Assert.Single(results);
            Assert.Equal("Trần Thị B", result.FullName);
            Assert.Equal("+84912345678", result.PhoneNumber);
        }
    }

    [IdentitySqlServerFact]
    public async Task Update_with_stale_rowversion_reports_conflict_and_keeps_latest_data()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        var actorId = await CreateActorUserAsync();

        OwnerDetails created;
        await using (var context = IdentitySqlServerTestEnvironment.CreateContext())
        {
            var service = CreateService(context);
            var ownerCode = await service.CreateAsync(new CreateOwnerRequest(
                actorId, "Nguyễn Văn A", "0912345678", null, null));
            created = (await service.FindAsync(await SingleOwnerIdAsync(ownerCode)))!;
        }

        await using (var context = IdentitySqlServerTestEnvironment.CreateContext())
        {
            var service = CreateService(context);
            await service.UpdateAsync(new UpdateOwnerRequest(
                actorId, created.Id, created.RowVersion, "Nguyễn Văn An", "0912345678", null, null));
        }

        await using (var context = IdentitySqlServerTestEnvironment.CreateContext())
        {
            var service = CreateService(context);
            await Assert.ThrowsAnyAsync<OwnerConcurrencyException>(() => service.UpdateAsync(new UpdateOwnerRequest(
                actorId, created.Id, created.RowVersion, "Nguyễn Văn Cũ", "0912345678", null, null)));
        }

        await using (var verify = IdentitySqlServerTestEnvironment.CreateContext())
        {
            var owner = await verify.Owners.SingleAsync(candidate => candidate.Id == created.Id);
            Assert.Equal("Nguyễn Văn An", owner.FullName);
        }
    }

    [IdentitySqlServerFact]
    public async Task SetActive_disables_owner_and_writes_audit_in_the_same_transaction()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        var actorId = await CreateActorUserAsync();

        int ownerId;
        byte[] rowVersion;
        await using (var context = IdentitySqlServerTestEnvironment.CreateContext())
        {
            var service = CreateService(context);
            await service.CreateAsync(new CreateOwnerRequest(
                actorId, "Nguyễn Văn A", "0912345678", null, null));
            var details = await service.FindAsync(await SingleOwnerIdAsync("OWN-000001"));
            Assert.NotNull(details);
            ownerId = details.Id;
            rowVersion = details.RowVersion;

            await service.SetActiveAsync(new OwnerActivationRequest(actorId, ownerId, rowVersion, false));
        }

        await using (var verify = IdentitySqlServerTestEnvironment.CreateContext())
        {
            var owner = await verify.Owners.SingleAsync(candidate => candidate.Id == ownerId);
            Assert.False(owner.IsActive);
            Assert.Equal(
                1,
                await verify.AuditLogs.CountAsync(audit =>
                    audit.Action == "Owner.Deactivated" &&
                    audit.EntityName == "Owner" &&
                    audit.EntityId == ownerId.ToString() &&
                    audit.UserId == actorId));
        }
    }

    private static OwnerService CreateService(ApplicationDbContext context) =>
        new(context, new OwnerPhoneNormalizer(), TimeProvider.System);

    private static async Task<int> SingleOwnerIdAsync(string ownerCode)
    {
        await using var context = IdentitySqlServerTestEnvironment.CreateContext();
        return await context.Owners
            .Where(owner => owner.OwnerCode == ownerCode)
            .Select(owner => owner.Id)
            .SingleAsync();
    }

    private static async Task<string> CreateActorUserAsync()
    {
        await using var context = IdentitySqlServerTestEnvironment.CreateContext();
        var user = new ApplicationUser
        {
            UserName = "owner-service-actor@example.test",
            NormalizedUserName = "OWNER-SERVICE-ACTOR@EXAMPLE.TEST",
            Email = "owner-service-actor@example.test",
            NormalizedEmail = "OWNER-SERVICE-ACTOR@EXAMPLE.TEST",
            EmailConfirmed = true,
            FullName = "Owner Service Actor",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            SecurityStamp = "owner-service-actor-stamp",
            ConcurrencyStamp = "owner-service-actor-concurrency"
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user.Id;
    }
}
