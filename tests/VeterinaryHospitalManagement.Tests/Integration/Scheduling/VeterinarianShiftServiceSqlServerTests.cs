using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VeterinaryHospitalManagement.Tests.Infrastructure.Identity;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Services.Identity;
using VeterinaryHospitalManagement.Web.Services.Scheduling;
using VeterinaryHospitalManagement.Web.Services.Veterinarians;

namespace VeterinaryHospitalManagement.Tests.Integration.Scheduling;

[Collection(IdentitySqlServerTestCollection.Name)]
public sealed class VeterinarianShiftServiceSqlServerTests
{
    [IdentitySqlServerFact]
    public async Task Create_saves_shift_in_utc_and_writes_audit()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var f = new IdentitySqlServerWebApplicationFactory();
        var actor = await Start(f);
        var vetId = await CreateVet(f, actor, "vet1@vet.test", "VET-001");

        var start = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.FromHours(7));
        var end = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(7));

        var shiftId = await WithShift(f, s => s.CreateAsync(new(actor, vetId, start, end)));

        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Web.Data.ApplicationDbContext>();
        var shift = await db.VeterinarianShifts.SingleAsync(x => x.Id == shiftId);

        Assert.Equal(start.ToUniversalTime(), shift.StartAt);
        Assert.Equal(end.ToUniversalTime(), shift.EndAt);
        Assert.True(shift.IsActive);
        Assert.Single(await db.AuditLogs.Where(x => x.Action == "VeterinarianShift.Created" && x.EntityId == shiftId.ToString()).ToListAsync());
    }

    [IdentitySqlServerFact]
    public async Task Create_rejects_inactive_veterinarian_or_user()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var f = new IdentitySqlServerWebApplicationFactory();
        var actor = await Start(f);
        var vetId = await CreateVet(f, actor, "vet2@vet.test", "VET-002");

        // Deactivate vet profile
        await using (var scope = f.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Web.Data.ApplicationDbContext>();
            var p = await db.VeterinarianProfiles.FindAsync(vetId);
            p!.IsActive = false;
            await db.SaveChangesAsync();
        }

        var start = DateTimeOffset.UtcNow;
        var end = start.AddHours(4);

        await Assert.ThrowsAsync<ShiftManagementException>(
            () => WithShift(f, s => s.CreateAsync(new(actor, vetId, start, end))));
    }

    [IdentitySqlServerFact]
    public async Task Create_rejects_overlapping_shifts_for_same_veterinarian_but_allows_other_vet()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var f = new IdentitySqlServerWebApplicationFactory();
        var actor = await Start(f);
        var vet1 = await CreateVet(f, actor, "v1@vet.test", "VET-V1");
        var vet2 = await CreateVet(f, actor, "v2@vet.test", "VET-V2");

        var start1 = new DateTimeOffset(2026, 10, 2, 8, 0, 0, TimeSpan.Zero);
        var end1 = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        await WithShift(f, s => s.CreateAsync(new(actor, vet1, start1, end1)));

        // Overlap for vet1: 10:00 - 14:00
        var startOverlap = new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.Zero);
        var endOverlap = new DateTimeOffset(2026, 10, 2, 14, 0, 0, TimeSpan.Zero);

        await Assert.ThrowsAsync<ShiftManagementException>(
            () => WithShift(f, s => s.CreateAsync(new(actor, vet1, startOverlap, endOverlap))));

        // Same time for vet2 should succeed!
        var shift2Id = await WithShift(f, s => s.CreateAsync(new(actor, vet2, startOverlap, endOverlap)));
        Assert.True(shift2Id > 0);
    }

    [IdentitySqlServerFact]
    public async Task Create_allows_adjacent_shifts()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var f = new IdentitySqlServerWebApplicationFactory();
        var actor = await Start(f);
        var vet = await CreateVet(f, actor, "adj@vet.test", "VET-ADJ");

        var morningStart = new DateTimeOffset(2026, 10, 3, 8, 0, 0, TimeSpan.Zero);
        var morningEnd = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
        await WithShift(f, s => s.CreateAsync(new(actor, vet, morningStart, morningEnd)));

        // Afternoon starts exactly when morning ends: 12:00 - 17:00
        var afternoonEnd = new DateTimeOffset(2026, 10, 3, 17, 0, 0, TimeSpan.Zero);
        var afternoonId = await WithShift(f, s => s.CreateAsync(new(actor, vet, morningEnd, afternoonEnd)));
        Assert.True(afternoonId > 0);
    }

    [IdentitySqlServerFact]
    public async Task Update_stale_version_reports_concurrency_conflict()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var f = new IdentitySqlServerWebApplicationFactory();
        var actor = await Start(f);
        var vet = await CreateVet(f, actor, "stale-shift@vet.test", "VET-STL");

        var start = new DateTimeOffset(2026, 10, 4, 8, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);
        var id = await WithShift(f, s => s.CreateAsync(new(actor, vet, start, end)));

        var details = (await WithShift(f, s => s.FindAsync(id)))!;
        var staleRowVersion = details.RowVersion;

        // Perform first update
        var newEnd = new DateTimeOffset(2026, 10, 4, 11, 0, 0, TimeSpan.Zero);
        await WithShift(f, s => s.UpdateAsync(new(actor, id, staleRowVersion, start, newEnd)));

        // Attempt second update with stale row version
        await Assert.ThrowsAsync<ShiftConcurrencyException>(
            () => WithShift(f, s => s.UpdateAsync(new(actor, id, staleRowVersion, start, end))));
    }

    [IdentitySqlServerFact]
    public async Task SetActive_toggles_active_state_and_writes_audit()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var f = new IdentitySqlServerWebApplicationFactory();
        var actor = await Start(f);
        var vet = await CreateVet(f, actor, "act@vet.test", "VET-ACT");

        var start = new DateTimeOffset(2026, 10, 5, 8, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        var id = await WithShift(f, s => s.CreateAsync(new(actor, vet, start, end)));

        var details = (await WithShift(f, s => s.FindAsync(id)))!;
        await WithShift(f, s => s.SetActiveAsync(new(actor, id, details.RowVersion, false)));

        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Web.Data.ApplicationDbContext>();
        var updated = await db.VeterinarianShifts.SingleAsync(x => x.Id == id);
        Assert.False(updated.IsActive);
        Assert.Single(await db.AuditLogs.Where(x => x.Action == "VeterinarianShift.Deactivated" && x.EntityId == id.ToString()).ToListAsync());
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<T> WithShift<T>(IdentitySqlServerWebApplicationFactory f, Func<IVeterinarianShiftService, Task<T>> call)
    {
        await using var s = f.Services.CreateAsyncScope();
        return await call(s.ServiceProvider.GetRequiredService<IVeterinarianShiftService>());
    }

    private static async Task WithShift(IdentitySqlServerWebApplicationFactory f, Func<IVeterinarianShiftService, Task> call)
    {
        await using var s = f.Services.CreateAsyncScope();
        await call(s.ServiceProvider.GetRequiredService<IVeterinarianShiftService>());
    }

    private static async Task<string> Start(IdentitySqlServerWebApplicationFactory f)
    {
        using var c = f.CreateClient();
        await c.GetAsync("/");
        await using var s = f.Services.CreateAsyncScope();
        return await s.ServiceProvider.GetRequiredService<Web.Data.ApplicationDbContext>().Users
            .Where(x => x.NormalizedEmail == IdentitySqlServerTestEnvironment.BootstrapAdminEmail.ToUpperInvariant())
            .Select(x => x.Id)
            .SingleAsync();
    }

    private static async Task<int> CreateVet(IdentitySqlServerWebApplicationFactory f, string actor, string email, string doctorCode)
    {
        await using var s = f.Services.CreateAsyncScope();
        await s.ServiceProvider.GetRequiredService<IUserManagementService>()
            .CreateAsync(new(actor, "Vet Doctor", email, "Integration.Vet123!", SystemRoleNames.Veterinarian));

        var userId = await s.ServiceProvider.GetRequiredService<Web.Data.ApplicationDbContext>().Users
            .Where(x => x.Email == email)
            .Select(x => x.Id)
            .SingleAsync();

        return await s.ServiceProvider.GetRequiredService<IVeterinarianProfileService>()
            .CreateAsync(new(actor, userId, doctorCode, "General"));
    }
}
