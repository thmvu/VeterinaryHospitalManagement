using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VeterinaryHospitalManagement.Tests.Infrastructure.Identity;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Services.Identity;
using VeterinaryHospitalManagement.Web.Services.Veterinarians;

namespace VeterinaryHospitalManagement.Tests.Integration.Veterinarians;

[Collection(IdentitySqlServerTestCollection.Name)]
public sealed class VeterinarianAccountSynchronizationTests
{
    [IdentitySqlServerFact]
    public async Task Creating_veterinarian_account_also_creates_visible_doctor_profile()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var actor = await StartAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
        var userId = await users.CreateAsync(new(actor, "Bác sĩ Lan", "lan@vet.test", "Integration.Vet123!", SystemRoleNames.Veterinarian));
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var profile = await db.VeterinarianProfiles.SingleAsync(x => x.UserId == userId);
        Assert.Equal("VET-000001", profile.DoctorCode);
        Assert.True(profile.IsActive);
        Assert.Equal(1, await db.AuditLogs.CountAsync(x => x.Action == "VeterinarianProfile.Created"));
    }

    [IdentitySqlServerFact]
    public async Task Changing_role_to_veterinarian_creates_profile_only_once()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var actor = await StartAsync(factory);
        string userId;
        await using (var createScope = factory.Services.CreateAsyncScope())
        {
            userId = await createScope.ServiceProvider.GetRequiredService<IUserManagementService>()
                .CreateAsync(new(actor, "Bác sĩ Lan", "lan@vet.test", "Integration.Vet123!", SystemRoleNames.Receptionist));
        }
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
        var user = (await users.FindAsync(userId))!;
        await users.ChangeRoleAsync(new(actor, userId, user.ConcurrencyStamp, SystemRoleNames.Veterinarian));
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal("VET-000001", await db.VeterinarianProfiles.Where(x => x.UserId == userId).Select(x => x.DoctorCode).SingleAsync());
        Assert.Equal(0, await users.SynchronizeVeterinarianProfilesAsync());
        Assert.Equal(1, await db.VeterinarianProfiles.CountAsync(x => x.UserId == userId));
        await using var nextScope = factory.Services.CreateAsyncScope();
        var nextUsers = nextScope.ServiceProvider.GetRequiredService<IUserManagementService>();
        var updated = (await nextUsers.FindAsync(userId))!;
        await nextUsers.ChangeRoleAsync(new(actor, userId, updated.ConcurrencyStamp, SystemRoleNames.Receptionist));
        var listed = await nextScope.ServiceProvider.GetRequiredService<IVeterinarianProfileService>().ListAsync();
        Assert.Single(listed);
        Assert.False(listed[0].IsActive);
    }

    [IdentitySqlServerFact]
    public async Task Doctor_profile_failure_rolls_back_account_creation()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        await StartAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
        await Assert.ThrowsAnyAsync<Exception>(() => users.CreateAsync(
            new("missing-actor", "Bác sĩ Lan", "lan@vet.test", "Integration.Vet123!", SystemRoleNames.Veterinarian)));
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Users.AnyAsync(x => x.Email == "lan@vet.test"));
        Assert.False(await db.VeterinarianProfiles.AnyAsync());
    }

    [IdentitySqlServerFact]
    public async Task Backfill_repairs_existing_veterinarian_without_duplicate_on_second_run()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var actor = await StartAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
        var userId = await users.CreateAsync(new(actor, "Bác sĩ cũ", "old@vet.test", "Integration.Vet123!", SystemRoleNames.Veterinarian));
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var profile = await db.VeterinarianProfiles.SingleAsync(x => x.UserId == userId);
        db.VeterinarianProfiles.Remove(profile);
        await db.SaveChangesAsync();
        Assert.Equal(1, await users.SynchronizeVeterinarianProfilesAsync());
        Assert.Equal(0, await users.SynchronizeVeterinarianProfilesAsync());
        Assert.Single(await db.VeterinarianProfiles.Where(x => x.UserId == userId).ToListAsync());
    }

    private static async Task<string> StartAsync(IdentitySqlServerWebApplicationFactory factory)
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        await client.GetAsync("/");
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users
            .Where(x => x.NormalizedEmail == IdentitySqlServerTestEnvironment.BootstrapAdminEmail.ToUpperInvariant())
            .Select(x => x.Id).SingleAsync();
    }
}
