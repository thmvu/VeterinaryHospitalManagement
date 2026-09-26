using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VeterinaryHospitalManagement.Tests.Infrastructure.Identity;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Entities;
using VeterinaryHospitalManagement.Web.Models.Enums;
using VeterinaryHospitalManagement.Web.Services.Identity;
using VeterinaryHospitalManagement.Web.Services.Clinical;
using VeterinaryHospitalManagement.Web.Services.Scheduling;
using VeterinaryHospitalManagement.Web.Services.Veterinarians;
using VeterinaryHospitalManagement.Web.Services.Visits;

namespace VeterinaryHospitalManagement.Tests.Integration.Visits;

[Collection(IdentitySqlServerTestCollection.Name)]
public sealed class VisitServiceSqlServerTests
{
    [IdentitySqlServerFact]
    public async Task Assigned_veterinarian_can_save_draft_from_form_while_other_veterinarian_cannot_read_it()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var data = await Setup(factory);
        var visitId = await WithVisitService(factory, s => s.WalkInAsync(new(data.PetId, data.VetProfileId, data.ActorUserId)));
        var visit = (await WithVisitService(factory, s => s.GetDetailsAsync(visitId)))!;
        await WithVisitService(factory, async s => { await s.StartAsync(new(visitId, data.VetUserId, visit.RowVersion)); return true; });

        using (var otherDoctor = Client(factory))
        {
            await Login(otherDoctor, "vet2@vet.test", "Integration.Vet123!");
            Assert.Equal(HttpStatusCode.Forbidden, (await otherDoctor.GetAsync($"/BackOffice/MedicalRecords/Details/{visitId}")).StatusCode);
        }

        using var doctor = Client(factory);
        await Login(doctor, "vet1@vet.test", "Integration.Vet123!");
        var editPage = await doctor.GetStringAsync($"/BackOffice/MedicalRecords/Edit/{visitId}");
        Assert.Contains("Ghi bệnh án cho Milo", editPage);
        using var response = await doctor.PostAsync("/BackOffice/MedicalRecords/Edit", new FormUrlEncodedContent([
            new("VisitId", visitId.ToString()), new("ChiefComplaint", "Bỏ ăn"),
            new("Diagnosis", "Theo dõi tiêu hóa"), new("WeightKg", "8.25"),
            new("__RequestVerificationToken", Token(editPage))]));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/BackOffice/MedicalRecords/Details/{visitId}", response.Headers.Location?.ToString());
        var details = await doctor.GetStringAsync($"/BackOffice/MedicalRecords/Details/{visitId}");
        Assert.Contains("Theo dõi tiêu hóa", WebUtility.HtmlDecode(details));

        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().MedicalRecords.CountAsync());
    }

    [IdentitySqlServerFact]
    public async Task Medical_record_draft_is_saved_and_stale_or_other_veterinarian_edits_are_rejected()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var data = await Setup(factory);
        var visitId = await WithVisitService(factory, s => s.WalkInAsync(new(data.PetId, data.VetProfileId, data.ActorUserId)));
        var visit = (await WithVisitService(factory, s => s.GetDetailsAsync(visitId)))!;
        await WithVisitService(factory, async s => { await s.StartAsync(new(visitId, data.VetUserId, visit.RowVersion)); return true; });

        var id = await WithMedicalRecordService(factory, s => s.SaveDraftAsync(new(
            visitId, data.VetUserId, null, "  Bỏ ăn  ", "Mệt mỏi", 8.25m, 38.5m,
            null, "Theo dõi", new DateOnly(2026, 11, 5))));
        var original = (await WithMedicalRecordService(factory, s => s.FindByVisitAsync(visitId)))!;
        Assert.Equal(id, original.Id);
        Assert.Equal("Bỏ ăn", original.ChiefComplaint);
        Assert.Equal(8.25m, original.WeightKg);
        Assert.Equal("Draft", original.Status);

        await Assert.ThrowsAsync<MedicalRecordAccessException>(() => WithMedicalRecordService(factory, s => s.SaveDraftAsync(new(
            visitId, data.SecondVetUserId, original.RowVersion, "Sai bác sĩ", null, null, null, null, null, null))));
        await WithMedicalRecordService(factory, s => s.SaveDraftAsync(new(
            visitId, data.VetUserId, original.RowVersion, "Đã ăn lại", null, 8.30m, null, "Theo dõi tiếp", null, null)));
        await Assert.ThrowsAsync<MedicalRecordManagementException>(() => WithMedicalRecordService(factory, s => s.SaveDraftAsync(new(
            visitId, data.VetUserId, original.RowVersion, "Dữ liệu cũ", null, null, null, null, null, null))));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal("Đã ăn lại", (await db.MedicalRecords.SingleAsync()).ChiefComplaint);
        Assert.Equal(2, await db.AuditLogs.CountAsync(x => x.Action == "MedicalRecord.DraftSaved"));
    }

    [IdentitySqlServerFact]
    public async Task Medical_record_cannot_be_saved_before_visit_starts_or_after_finalization()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var data = await Setup(factory);
        var visitId = await WithVisitService(factory, s => s.WalkInAsync(new(data.PetId, data.VetProfileId, data.ActorUserId)));
        await Assert.ThrowsAsync<MedicalRecordManagementException>(() => WithMedicalRecordService(factory, s => s.SaveDraftAsync(new(
            visitId, data.VetUserId, null, "Khám", null, null, null, null, null, null))));
        var visit = (await WithVisitService(factory, s => s.GetDetailsAsync(visitId)))!;
        await WithVisitService(factory, async s => { await s.StartAsync(new(visitId, data.VetUserId, visit.RowVersion)); return true; });
        await WithMedicalRecordService(factory, s => s.SaveDraftAsync(new(
            visitId, data.VetUserId, null, "Khám", null, null, null, "Chẩn đoán", null, null)));
        var original = (await WithMedicalRecordService(factory, s => s.FindByVisitAsync(visitId)))!;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var record = await db.MedicalRecords.SingleAsync();
            record.Status = ClinicalDocumentStatus.Finalized;
            record.FinalizedAt = DateTimeOffset.UtcNow;
            record.FinalizedByVeterinarianId = data.VetProfileId;
            await db.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<MedicalRecordManagementException>(() => WithMedicalRecordService(factory, s => s.SaveDraftAsync(new(
            visitId, data.VetUserId, original.RowVersion, "Sửa sau chốt", null, null, null, null, null, null))));
    }

    [IdentitySqlServerFact]
    public async Task CheckIn_rejects_participants_deactivated_after_booking()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var data = await Setup(factory);
        var start = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.FromHours(7));
        var appointmentId = await WithAppointmentService(factory, s =>
            s.CreateAsync(new(data.ActorUserId, data.PetId, data.VetProfileId, start, start.AddMinutes(30), "Khám")));

        foreach (var participant in new[] { "Pet", "Owner", "Veterinarian", "User" })
        {
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                switch (participant)
                {
                    case "Pet": (await db.Pets.FindAsync(data.PetId))!.IsActive = false; break;
                    case "Owner": (await db.Owners.SingleAsync())!.IsActive = false; break;
                    case "Veterinarian": (await db.VeterinarianProfiles.FindAsync(data.VetProfileId))!.IsActive = false; break;
                    case "User": (await db.Users.FindAsync(data.VetUserId))!.IsActive = false; break;
                }
                await db.SaveChangesAsync();
            }

            await Assert.ThrowsAsync<VisitManagementException>(() => WithVisitService(factory, s =>
                s.CheckInFromAppointmentAsync(new(appointmentId, data.ActorUserId))));

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                switch (participant)
                {
                    case "Pet": (await db.Pets.FindAsync(data.PetId))!.IsActive = true; break;
                    case "Owner": (await db.Owners.SingleAsync())!.IsActive = true; break;
                    case "Veterinarian": (await db.VeterinarianProfiles.FindAsync(data.VetProfileId))!.IsActive = true; break;
                    case "User": (await db.Users.FindAsync(data.VetUserId))!.IsActive = true; break;
                }
                await db.SaveChangesAsync();
            }
        }

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(AppointmentStatus.Scheduled, (await verify.Appointments.FindAsync(appointmentId))!.Status);
        Assert.Equal(0, await verify.Visits.CountAsync());
        Assert.Equal(0, await verify.AuditLogs.CountAsync(x => x.Action == "Visit.CheckedIn"));
    }

    [IdentitySqlServerFact]
    public async Task WalkIn_keeps_full_150_character_owner_and_veterinarian_names()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var data = await Setup(factory);
        var ownerName = new string('O', 150);
        var vetName = new string('V', 150);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Owners.SingleAsync()).FullName = ownerName;
            (await db.Users.FindAsync(data.VetUserId))!.FullName = vetName;
            await db.SaveChangesAsync();
        }
        var visitId = await WithVisitService(factory, s =>
            s.WalkInAsync(new(data.PetId, data.VetProfileId, data.ActorUserId)));
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var visit = (await verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Visits.FindAsync(visitId))!;
        Assert.Equal(ownerName, visit.OwnerNameSnapshot);
        Assert.Equal(vetName, visit.VeterinarianNameSnapshot);
    }

    [IdentitySqlServerFact]
    public async Task CheckIn_from_appointment_is_idempotent_and_creates_snapshots()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var data = await Setup(factory);

        var start = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.FromHours(7));
        var appointmentId = await WithAppointmentService(factory, s =>
            s.CreateAsync(new(data.ActorUserId, data.PetId, data.VetProfileId, start, start.AddMinutes(30), "Khám định kỳ")));

        // First CheckIn
        var visitId1 = await WithVisitService(factory, s =>
            s.CheckInFromAppointmentAsync(new(appointmentId, data.ActorUserId)));
        Assert.True(visitId1 > 0);

        // Idempotent CheckIn: must return the exact same visit id
        var visitId2 = await WithVisitService(factory, s =>
            s.CheckInFromAppointmentAsync(new(appointmentId, data.ActorUserId)));
        Assert.Equal(visitId1, visitId2);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var visit = await db.Visits.SingleAsync(v => v.Id == visitId1);

        Assert.Equal(appointmentId, visit.AppointmentId);
        Assert.Equal(data.PetId, visit.PetId);
        Assert.Equal(data.VetProfileId, visit.VeterinarianId);
        Assert.Equal("Milo", visit.PetNameSnapshot);
        Assert.Equal("Nguyễn Văn A", visit.OwnerNameSnapshot);
        Assert.Equal("+84901234567", visit.OwnerPhoneSnapshot);
        Assert.Equal(VisitStatus.Waiting, visit.Status);
        Assert.StartsWith("V-", visit.VisitNumber);

        var apt = await db.Appointments.SingleAsync(a => a.Id == appointmentId);
        Assert.Equal(AppointmentStatus.CheckedIn, apt.Status);

        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "Visit.CheckedIn" && a.EntityId == visitId1.ToString()));
    }

    [IdentitySqlServerFact]
    public async Task WalkIn_creates_waiting_visit_and_rejects_second_active_visit_for_same_pet()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var data = await Setup(factory);

        var visitId = await WithVisitService(factory, s =>
            s.WalkInAsync(new(data.PetId, data.VetProfileId, data.ActorUserId)));
        Assert.True(visitId > 0);

        // Attempting another walk-in for the same pet while first visit is Waiting must fail
        await Assert.ThrowsAsync<VisitManagementException>(() =>
            WithVisitService(factory, s => s.WalkInAsync(new(data.PetId, data.VetProfileId, data.ActorUserId))));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var visit = await db.Visits.SingleAsync(v => v.Id == visitId);
        Assert.Null(visit.AppointmentId);
        Assert.Equal(VisitStatus.Waiting, visit.Status);
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "Visit.WalkIn" && a.EntityId == visitId.ToString()));
    }

    [IdentitySqlServerFact]
    public async Task AssignVeterinarian_reassigns_and_updates_snapshot_with_concurrency_guard()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var data = await Setup(factory);

        var visitId = await WithVisitService(factory, s =>
            s.WalkInAsync(new(data.PetId, data.VetProfileId, data.ActorUserId)));
        var details = (await WithVisitService(factory, s => s.GetDetailsAsync(visitId)))!;

        // Reassign to second vet
        await WithVisitService(factory, async s =>
        {
            await s.AssignVeterinarianAsync(new(visitId, data.SecondVetProfileId, data.ActorUserId, details.RowVersion));
            return true;
        });

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var visit = await db.Visits.SingleAsync(v => v.Id == visitId);
        Assert.Equal(data.SecondVetProfileId, visit.VeterinarianId);
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "Visit.VeterinarianAssigned" && a.EntityId == visitId.ToString()));

        // Stale row version fails
        await Assert.ThrowsAsync<VisitManagementException>(() =>
            WithVisitService(factory, async s =>
            {
                await s.AssignVeterinarianAsync(new(visitId, data.VetProfileId, data.ActorUserId, details.RowVersion));
                return true;
            }));
    }

    [IdentitySqlServerFact]
    public async Task Cancel_requires_reason_and_sets_cancelled_status()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var data = await Setup(factory);

        var visitId = await WithVisitService(factory, s =>
            s.WalkInAsync(new(data.PetId, data.VetProfileId, data.ActorUserId)));
        var details = (await WithVisitService(factory, s => s.GetDetailsAsync(visitId)))!;

        // Empty reason fails
        await Assert.ThrowsAsync<VisitManagementException>(() =>
            WithVisitService(factory, async s =>
            {
                await s.CancelAsync(new(visitId, "", data.ActorUserId, details.RowVersion));
                return true;
            }));

        // Valid cancel
        await WithVisitService(factory, async s =>
        {
            await s.CancelAsync(new(visitId, "Chủ nuôi đổi ý về nhà", data.ActorUserId, details.RowVersion));
            return true;
        });

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var visit = await db.Visits.SingleAsync(v => v.Id == visitId);
        Assert.Equal(VisitStatus.Cancelled, visit.Status);
        Assert.Equal("Chủ nuôi đổi ý về nhà", visit.CancellationReason);
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "Visit.Cancelled" && a.EntityId == visitId.ToString()));
    }

    [IdentitySqlServerFact]
    public async Task Start_allows_assigned_veterinarian_only_and_blocks_concurrent_in_progress()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var data = await Setup(factory);

        var visitId = await WithVisitService(factory, s =>
            s.WalkInAsync(new(data.PetId, data.VetProfileId, data.ActorUserId)));
        var details = (await WithVisitService(factory, s => s.GetDetailsAsync(visitId)))!;

        // Different user cannot start
        await Assert.ThrowsAsync<VisitManagementException>(() =>
            WithVisitService(factory, async s =>
            {
                await s.StartAsync(new(visitId, data.SecondVetUserId, details.RowVersion));
                return true;
            }));

        // Assigned vet starts
        await WithVisitService(factory, async s =>
        {
            await s.StartAsync(new(visitId, data.VetUserId, details.RowVersion));
            return true;
        });

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var visit = await db.Visits.SingleAsync(v => v.Id == visitId);
        Assert.Equal(VisitStatus.InProgress, visit.Status);
        Assert.NotNull(visit.StartedAt);
        Assert.True(await db.AuditLogs.AnyAsync(a => a.Action == "Visit.Started" && a.EntityId == visitId.ToString()));

        // Create second pet for testing vet's concurrent in-progress block
        int secondPetId;
        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var owner = await seedDb.Owners.FirstAsync();
            var pet = new Pet
            {
                PetCode = "PET-000002",
                Name = "LuLu",
                SpeciesId = 1,
                OwnerId = owner.Id,
                Sex = PetSex.Female,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            seedDb.Pets.Add(pet);
            await seedDb.SaveChangesAsync();
            secondPetId = pet.Id;
        }

        var visitId2 = await WithVisitService(factory, s =>
            s.WalkInAsync(new(secondPetId, data.VetProfileId, data.ActorUserId)));
        var details2 = (await WithVisitService(factory, s => s.GetDetailsAsync(visitId2)))!;

        // Vet already has InProgress visit, starting another must fail
        await Assert.ThrowsAsync<VisitManagementException>(() =>
            WithVisitService(factory, async s =>
            {
                await s.StartAsync(new(visitId2, data.VetUserId, details2.RowVersion));
                return true;
            }));
    }

    [IdentitySqlServerFact]
    public async Task Availability_regression_checked_in_appointment_does_not_block_slot_if_visit_completed()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var data = await Setup(factory);

        var start = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.FromHours(7));
        var end = start.AddMinutes(30);

        var aptId = await WithAppointmentService(factory, s =>
            s.CreateAsync(new(data.ActorUserId, data.PetId, data.VetProfileId, start, end, "Khám ban đầu")));

        var visitId = await WithVisitService(factory, s =>
            s.CheckInFromAppointmentAsync(new(aptId, data.ActorUserId)));

        // While visit is Waiting, appointment check availability for same slot fails
        var avail1 = await WithAppointmentService(factory, s =>
            s.CheckAvailabilityAsync(data.PetId, data.VetProfileId, start, end));
        Assert.False(avail1.IsAvailable);

        // Mark visit as Completed directly in db to simulate complete workflow
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var v = await db.Visits.SingleAsync(x => x.Id == visitId);
            v.Status = VisitStatus.Completed;
            v.StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10);
            v.CompletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        // Now availability check for that pet/vet slot should be available!
        var avail2 = await WithAppointmentService(factory, s =>
            s.CheckAvailabilityAsync(data.PetId, data.VetProfileId, start, end));
        Assert.True(avail2.IsAvailable);
    }

    // ── Test Setup Helpers ──────────────────────────────────────────────────────

    private static async Task<T> WithVisitService<T>(
        IdentitySqlServerWebApplicationFactory factory,
        Func<IVisitService, Task<T>> execute)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IVisitService>();
        return await execute(service);
    }

    private static async Task<T> WithMedicalRecordService<T>(
        IdentitySqlServerWebApplicationFactory factory,
        Func<IMedicalRecordService, Task<T>> execute)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await execute(scope.ServiceProvider.GetRequiredService<IMedicalRecordService>());
    }

    private static HttpClient Client(IdentitySqlServerWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

    private static async Task Login(HttpClient client, string email, string password)
    {
        var token = Token(await client.GetStringAsync("/Account/Login"));
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent([
            new("Email", email), new("Password", password), new("RememberMe", "false"),
            new("__RequestVerificationToken", token)]));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static string Token(string html) =>
        Regex.Match(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"").Groups["token"].Value;

    private static async Task<T> WithAppointmentService<T>(
        IdentitySqlServerWebApplicationFactory factory,
        Func<IAppointmentService, Task<T>> execute)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IAppointmentService>();
        return await execute(service);
    }

    private static async Task<TestSetupData> Setup(IdentitySqlServerWebApplicationFactory factory)
    {
        using (var c = factory.CreateClient()) await c.GetAsync("/");
        await using var scope = factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<ApplicationDbContext>();

        var actor = await db.Users.Where(x => x.NormalizedEmail == IdentitySqlServerTestEnvironment.BootstrapAdminEmail.ToUpperInvariant()).Select(x => x.Id).SingleAsync();

        // Create Vet User 1
        await sp.GetRequiredService<IUserManagementService>().CreateAsync(new(actor, "Bác Sĩ Một", "vet1@vet.test", "Integration.Vet123!", SystemRoleNames.Veterinarian));
        var vetUser1 = await db.Users.Where(x => x.Email == "vet1@vet.test").Select(x => x.Id).SingleAsync();
        var vetProfile1 = await sp.GetRequiredService<IVeterinarianProfileService>().CreateAsync(new(actor, vetUser1, "Tổng quát"));

        // Create Vet User 2
        await sp.GetRequiredService<IUserManagementService>().CreateAsync(new(actor, "Bác Sĩ Hai", "vet2@vet.test", "Integration.Vet123!", SystemRoleNames.Veterinarian));
        var vetUser2 = await db.Users.Where(x => x.Email == "vet2@vet.test").Select(x => x.Id).SingleAsync();
        var vetProfile2 = await sp.GetRequiredService<IVeterinarianProfileService>().CreateAsync(new(actor, vetUser2, "Ngoại khoa"));

        // Create Owner & Pet
        var owner = new Owner
        {
            OwnerCode = "OWN-000001",
            FullName = "Nguyễn Văn A",
            PhoneNumber = "+84901234567",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var species = new Species { Code = "DOG", Name = "Chó", IsActive = true };
        db.AddRange(owner, species);
        await db.SaveChangesAsync();

        var pet = new Pet
        {
            PetCode = "PET-000001",
            OwnerId = owner.Id,
            Name = "Milo",
            SpeciesId = species.Id,
            Sex = PetSex.Male,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Pets.Add(pet);
        await db.SaveChangesAsync();

        // Shifts for both vets covering the test day
        var shiftStart = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.FromHours(7));
        db.VeterinarianShifts.Add(new VeterinarianShift
        {
            VeterinarianId = vetProfile1,
            StartAt = shiftStart.ToUniversalTime(),
            EndAt = shiftStart.AddHours(10).ToUniversalTime(),
            IsActive = true
        });
        db.VeterinarianShifts.Add(new VeterinarianShift
        {
            VeterinarianId = vetProfile2,
            StartAt = shiftStart.ToUniversalTime(),
            EndAt = shiftStart.AddHours(10).ToUniversalTime(),
            IsActive = true
        });
        await db.SaveChangesAsync();

        return new TestSetupData(actor, pet.Id, vetProfile1, vetUser1, vetProfile2, vetUser2);
    }

    private sealed record TestSetupData(
        string ActorUserId,
        int PetId,
        int VetProfileId,
        string VetUserId,
        int SecondVetProfileId,
        string SecondVetUserId);
}
