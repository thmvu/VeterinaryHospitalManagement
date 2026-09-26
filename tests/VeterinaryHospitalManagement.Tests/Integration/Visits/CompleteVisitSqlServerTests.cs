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
using VeterinaryHospitalManagement.Web.Services.Visits;
using VisitServiceLine = VeterinaryHospitalManagement.Web.Models.Entities.VisitService;

namespace VeterinaryHospitalManagement.Tests.Integration.Visits;

[Collection(IdentitySqlServerTestCollection.Name)]
public sealed class CompleteVisitSqlServerTests
{
    [IdentitySqlServerFact]
    public async Task Completion_finalizes_record_and_prescription_with_visit_in_one_operation()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var setup = await SetupAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IVisitService>();
        var record = new MedicalRecord { VisitId = setup.VisitId, ChiefComplaint = "Bỏ ăn", Diagnosis = "Viêm da" };
        var prescription = new Prescription { VisitId = setup.VisitId, CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1) };
        var medicine = new Medicine { Code = "MED-001", Name = "Thuốc mẫu", Unit = "viên" };
        db.AddRange(record, prescription, medicine);
        await db.SaveChangesAsync();
        db.PrescriptionItems.Add(new PrescriptionItem { PrescriptionId = prescription.Id, MedicineId = medicine.Id,
            MedicineNameSnapshot = medicine.Name, UnitSnapshot = medicine.Unit, Dosage = "5 mg", Route = "Uống",
            Frequency = "2 lần/ngày", Duration = "5 ngày", Quantity = 10 });
        db.VisitServices.Add(new VisitServiceLine { VisitId = setup.VisitId, ServiceCatalogId = setup.CatalogId,
            ServiceNameSnapshot = "Khám tổng quát", Quantity = 1, UnitPrice = 100000,
            Status = VisitServiceStatus.Performed, CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            PerformedAt = DateTimeOffset.UtcNow, PerformedByVeterinarianId = setup.VetId });
        await db.SaveChangesAsync();
        var version = (await service.GetDetailsAsync(setup.VisitId))!.RowVersion;

        await service.CompleteAsync(new(setup.VisitId, setup.VetUserId, version));

        db.ChangeTracker.Clear();
        var visit = await db.Visits.SingleAsync(x => x.Id == setup.VisitId);
        var savedRecord = await db.MedicalRecords.SingleAsync(x => x.VisitId == setup.VisitId);
        var savedPrescription = await db.Prescriptions.SingleAsync(x => x.VisitId == setup.VisitId);
        Assert.Equal(VisitStatus.Completed, visit.Status);
        Assert.True(visit.CompletedAt > visit.StartedAt);
        Assert.Equal(ClinicalDocumentStatus.Finalized, savedRecord.Status);
        Assert.Equal(setup.VetId, savedRecord.FinalizedByVeterinarianId);
        Assert.Equal(visit.CompletedAt, savedRecord.FinalizedAt);
        Assert.Equal(ClinicalDocumentStatus.Finalized, savedPrescription.Status);
        Assert.Equal(visit.CompletedAt, savedPrescription.FinalizedAt);
        Assert.Equal(1, await db.AuditLogs.CountAsync(x => x.Action == "Visit.Completed" && x.EntityId == setup.VisitId.ToString()));
        await Assert.ThrowsAsync<VisitManagementException>(() => service.CompleteAsync(new(setup.VisitId, setup.VetUserId, version)));
    }

    [IdentitySqlServerFact]
    public async Task Completion_rejects_missing_diagnosis_pending_service_and_empty_prescription_without_partial_finalization()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var setup = await SetupAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IVisitService>();
        var request = new CompleteVisitRequest(setup.VisitId, setup.VetUserId,
            (await service.GetDetailsAsync(setup.VisitId))!.RowVersion);
        await Assert.ThrowsAsync<VisitManagementException>(() => service.CompleteAsync(request));
        var record = new MedicalRecord { VisitId = setup.VisitId, ChiefComplaint = "Bỏ ăn" };
        db.MedicalRecords.Add(record);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<VisitManagementException>(() => service.CompleteAsync(request));
        record.Diagnosis = "Viêm da";
        db.VisitServices.Add(new VisitServiceLine { VisitId = setup.VisitId, ServiceCatalogId = setup.CatalogId,
            ServiceNameSnapshot = "Khám tổng quát", Quantity = 1, UnitPrice = 100000,
            Status = VisitServiceStatus.Pending, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<VisitManagementException>(() => service.CompleteAsync(request));
        var line = await db.VisitServices.SingleAsync();
        line.Status = VisitServiceStatus.Cancelled;
        line.CancellationReason = "Không cần";
        db.Prescriptions.Add(new Prescription { VisitId = setup.VisitId, CreatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<VisitManagementException>(() => service.CompleteAsync(request));
        db.ChangeTracker.Clear();
        Assert.Equal(VisitStatus.InProgress, (await db.Visits.FindAsync(setup.VisitId))!.Status);
        Assert.Equal(ClinicalDocumentStatus.Draft, (await db.MedicalRecords.SingleAsync())!.Status);
    }

    [IdentitySqlServerFact]
    public async Task Completion_allows_no_prescription_but_rejects_wrong_actor_and_stale_version()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var setup = await SetupAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IVisitService>();
        db.MedicalRecords.Add(new MedicalRecord { VisitId = setup.VisitId, ChiefComplaint = "Bỏ ăn", Diagnosis = "Viêm da" });
        await db.SaveChangesAsync();
        var adminId = await db.Users.Where(x => x.NormalizedEmail == IdentitySqlServerTestEnvironment.BootstrapAdminEmail.ToUpperInvariant())
            .Select(x => x.Id).SingleAsync();
        var version = (await service.GetDetailsAsync(setup.VisitId))!.RowVersion;
        await Assert.ThrowsAsync<VisitManagementException>(() => service.CompleteAsync(new(setup.VisitId, adminId, version)));
        await Assert.ThrowsAsync<VisitManagementException>(() => service.CompleteAsync(new(setup.VisitId, setup.VetUserId, new byte[8])));
        await service.CompleteAsync(new(setup.VisitId, setup.VetUserId, version));
        db.ChangeTracker.Clear();
        Assert.Equal(ClinicalDocumentStatus.Finalized, (await db.MedicalRecords.SingleAsync())!.Status);
        Assert.False(await db.Prescriptions.AnyAsync());
    }

    [IdentitySqlServerFact]
    public async Task Completion_rejects_prescription_line_without_required_instructions()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var setup = await SetupAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IVisitService>();
        var record = new MedicalRecord { VisitId = setup.VisitId, ChiefComplaint = "Bỏ ăn", Diagnosis = "Viêm da" };
        var prescription = new Prescription { VisitId = setup.VisitId, CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1) };
        var medicine = new Medicine { Code = "MED-001", Name = "Thuốc mẫu", Unit = "viên" };
        db.AddRange(record, prescription, medicine);
        await db.SaveChangesAsync();
        db.PrescriptionItems.Add(new PrescriptionItem { PrescriptionId = prescription.Id, MedicineId = medicine.Id,
            MedicineNameSnapshot = medicine.Name, UnitSnapshot = medicine.Unit, Dosage = "   ", Route = "Uống",
            Frequency = "2 lần/ngày", Duration = "5 ngày", Quantity = 10 });
        await db.SaveChangesAsync();
        var version = (await service.GetDetailsAsync(setup.VisitId))!.RowVersion;

        await Assert.ThrowsAsync<VisitManagementException>(() => service.CompleteAsync(new(setup.VisitId, setup.VetUserId, version)));
        db.ChangeTracker.Clear();
        Assert.Equal(VisitStatus.InProgress, (await db.Visits.FindAsync(setup.VisitId))!.Status);
        Assert.Equal(ClinicalDocumentStatus.Draft, (await db.MedicalRecords.SingleAsync())!.Status);
    }

    [IdentitySqlServerFact]
    public async Task Vet_can_complete_from_visit_page()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var setup = await SetupAsync(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.MedicalRecords.Add(new MedicalRecord { VisitId = setup.VisitId, ChiefComplaint = "Bỏ ăn", Diagnosis = "Viêm da" });
            await db.SaveChangesAsync();
        }
        using var adminClient = factory.CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var adminLoginPage = await adminClient.GetStringAsync("/Account/Login");
        using var adminLogin = new FormUrlEncodedContent([
            new("Email", IdentitySqlServerTestEnvironment.BootstrapAdminEmail),
            new("Password", IdentitySqlServerTestEnvironment.BootstrapAdminPassword),
            new("__RequestVerificationToken", Token(adminLoginPage))]);
        Assert.Equal(HttpStatusCode.Redirect, (await adminClient.PostAsync("/Account/Login", adminLogin)).StatusCode);
        var adminPage = await adminClient.GetStringAsync($"/BackOffice/Visits/Details/{setup.VisitId}");
        Assert.DoesNotContain("✓ Hoàn tất khám", adminPage);

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var loginPage = await client.GetStringAsync("/Account/Login");
        using var login = new FormUrlEncodedContent([
            new("Email", "lan@vet.test"), new("Password", "Integration.Vet123!"),
            new("__RequestVerificationToken", Token(loginPage))]);
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Account/Login", login)).StatusCode);
        var page = await client.GetStringAsync($"/BackOffice/Visits/Details/{setup.VisitId}");
        Assert.Contains("Hoàn tất khám", page);
        var version = (await GetVersionAsync(factory, setup.VisitId));
        using var form = new FormUrlEncodedContent([
            new("VisitId", setup.VisitId.ToString()), new("RowVersionBase64", Convert.ToBase64String(version)),
            new("__RequestVerificationToken", Token(page))]);
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/BackOffice/Visits/Complete", form)).StatusCode);
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(VisitStatus.Completed, (await verifyDb.Visits.FindAsync(setup.VisitId))!.Status);
    }

    private static string Token(string html) => Regex.Match(html,
        "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"").Groups["token"].Value;

    private static async Task<byte[]> GetVersionAsync(IdentitySqlServerWebApplicationFactory factory, int visitId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<IVisitService>().GetDetailsAsync(visitId))!.RowVersion;
    }

    private static async Task<(int VisitId, string VetUserId, int VetId, int CatalogId)> SetupAsync(IdentitySqlServerWebApplicationFactory factory)
    {
        using var client = factory.CreateClient();
        await client.GetAsync("/");
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var adminId = await db.Users.Where(x => x.NormalizedEmail == IdentitySqlServerTestEnvironment.BootstrapAdminEmail.ToUpperInvariant()).Select(x => x.Id).SingleAsync();
        var vetUserId = await scope.ServiceProvider.GetRequiredService<IUserManagementService>()
            .CreateAsync(new(adminId, "Bác sĩ Lan", "lan@vet.test", "Integration.Vet123!", SystemRoleNames.Veterinarian));
        var vetId = await db.VeterinarianProfiles.Where(x => x.UserId == vetUserId).Select(x => x.Id).SingleAsync();
        var owner = new Owner { OwnerCode = "OWN-000001", FullName = "Nguyễn An", PhoneNumber = "+84901234567", CreatedAt = DateTimeOffset.UtcNow };
        var species = new Species { Code = "DOG", Name = "Chó" };
        var catalog = new ServiceCatalog { Code = "EXAM", Name = "Khám tổng quát", Category = "Khám", Price = 100000 };
        db.AddRange(owner, species, catalog);
        await db.SaveChangesAsync();
        var pet = new Pet { PetCode = "PET-000001", OwnerId = owner.Id, SpeciesId = species.Id, Name = "Milu", CreatedAt = DateTimeOffset.UtcNow };
        db.Pets.Add(pet);
        await db.SaveChangesAsync();
        var visit = new Visit { VisitNumber = "V-20260927-0001", PetId = pet.Id, VeterinarianId = vetId,
            PetNameSnapshot = pet.Name, OwnerNameSnapshot = owner.FullName, OwnerPhoneSnapshot = owner.PhoneNumber,
            VeterinarianNameSnapshot = "Bác sĩ Lan", Status = VisitStatus.InProgress,
            CheckedInAt = DateTimeOffset.UtcNow.AddMinutes(-5), StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            CheckedInByUserId = adminId };
        db.Visits.Add(visit);
        await db.SaveChangesAsync();
        return (visit.Id, vetUserId, vetId, catalog.Id);
    }
}
