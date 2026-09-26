using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text.RegularExpressions;
using VeterinaryHospitalManagement.Tests.Infrastructure.Identity;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Entities;
using VeterinaryHospitalManagement.Web.Models.Enums;
using VeterinaryHospitalManagement.Web.Services.Clinical;
using VeterinaryHospitalManagement.Web.Services.Identity;

namespace VeterinaryHospitalManagement.Tests.Integration.Clinical;

[Collection(IdentitySqlServerTestCollection.Name)]
public sealed class PrescriptionDraftSqlServerTests
{
    [IdentitySqlServerFact]
    public async Task Assigned_veterinarian_saves_draft_with_medicine_snapshot_and_can_edit_it()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var setup = await SetupAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IPrescriptionService>();
        await service.SaveDraftAsync(new(setup.VisitId, setup.VetUserId, null, "Uống sau ăn",
            [new(null, setup.MedicineId, "5 mg", "Uống", "2 lần/ngày", "5 ngày", 10, null)]));
        var first = (await service.FindByVisitAsync(setup.VisitId))!;
        Assert.Equal("Draft", first.Status);
        Assert.Single(first.Items);
        Assert.Equal("Thuốc mẫu", first.Items[0].MedicineName);
        Assert.Equal("viên", first.Items[0].Unit);

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Medicines.FindAsync(setup.MedicineId))!.Name = "Tên thuốc đổi";
        await db.SaveChangesAsync();
        await service.SaveDraftAsync(new(setup.VisitId, setup.VetUserId, first.RowVersion, "Theo dõi tại nhà",
            [new(first.Items[0].Id, setup.MedicineId, "10 mg", "Uống", "1 lần/ngày", "3 ngày", 3, null)]));
        var updated = (await service.FindByVisitAsync(setup.VisitId))!;
        Assert.Equal("Theo dõi tại nhà", updated.Instructions);
        Assert.Equal("Thuốc mẫu", updated.Items[0].MedicineName);
        Assert.Equal("10 mg", updated.Items[0].Dosage);
    }

    [IdentitySqlServerFact]
    public async Task Rejects_wrong_veterinarian_stale_version_and_finalized_prescription()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var setup = await SetupAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IPrescriptionService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var adminId = await db.Users.Where(x => x.NormalizedEmail == IdentitySqlServerTestEnvironment.BootstrapAdminEmail.ToUpperInvariant())
            .Select(x => x.Id).SingleAsync();
        var item = new PrescriptionItemDraft(null, setup.MedicineId, "5 mg", "Uống", "2 lần/ngày", "5 ngày", 10, null);
        await Assert.ThrowsAsync<PrescriptionAccessException>(() => service.SaveDraftAsync(new(setup.VisitId, adminId, null, null, [item])));
        Assert.False(await db.Prescriptions.AnyAsync());

        await service.SaveDraftAsync(new(setup.VisitId, setup.VetUserId, null, null, [item]));
        var saved = (await service.FindByVisitAsync(setup.VisitId))!;
        await Assert.ThrowsAsync<PrescriptionManagementException>(() => service.SaveDraftAsync(new(setup.VisitId, setup.VetUserId,
            new byte[8], null, [new(saved.Items[0].Id, setup.MedicineId, "5 mg", "Uống", "2 lần/ngày", "5 ngày", 10, null)])));

        var tracked = await db.Prescriptions.SingleAsync();
        tracked.Status = ClinicalDocumentStatus.Finalized;
        tracked.FinalizedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<PrescriptionManagementException>(() => service.SaveDraftAsync(new(setup.VisitId, setup.VetUserId,
            tracked.RowVersion, null, [])));
    }

    [IdentitySqlServerFact]
    public async Task Rejects_inactive_medicine_and_preserves_existing_draft()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var setup = await SetupAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IPrescriptionService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var medicine = (await db.Medicines.FindAsync(setup.MedicineId))!;
        medicine.IsActive = false;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<PrescriptionManagementException>(() => service.SaveDraftAsync(new(setup.VisitId, setup.VetUserId,
            null, "Lưu tạm", [new(null, setup.MedicineId, "5 mg", "Uống", "1 lần/ngày", "2 ngày", 2, null)])));
        Assert.False(await db.Prescriptions.AnyAsync());
    }

    [IdentitySqlServerFact]
    public async Task Existing_line_keeps_its_snapshot_when_catalog_medicine_becomes_inactive()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var setup = await SetupAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IPrescriptionService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await service.SaveDraftAsync(new(setup.VisitId, setup.VetUserId, null, null,
            [new(null, setup.MedicineId, "5 mg", "Uống", "2 lần/ngày", "5 ngày", 10, null)]));
        var original = (await service.FindByVisitAsync(setup.VisitId))!;
        (await db.Medicines.FindAsync(setup.MedicineId))!.IsActive = false;
        await db.SaveChangesAsync();
        await service.SaveDraftAsync(new(setup.VisitId, setup.VetUserId, original.RowVersion, "Theo dõi",
            [new(original.Items[0].Id, setup.MedicineId, "5 mg", "Uống", "2 lần/ngày", "5 ngày", 10, null)]));
        var updated = (await service.FindByVisitAsync(setup.VisitId))!;
        Assert.Equal("Thuốc mẫu", updated.Items[0].MedicineName);
        Assert.Equal("Theo dõi", updated.Instructions);
    }

    [IdentitySqlServerFact]
    public async Task Removing_all_lines_keeps_an_editable_draft_without_medicine_items()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var setup = await SetupAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IPrescriptionService>();
        await service.SaveDraftAsync(new(setup.VisitId, setup.VetUserId, null, null,
            [new(null, setup.MedicineId, "5 mg", "Uống", "2 lần/ngày", "5 ngày", 10, null)]));
        var first = (await service.FindByVisitAsync(setup.VisitId))!;
        await service.SaveDraftAsync(new(setup.VisitId, setup.VetUserId, first.RowVersion, "Chưa kê thuốc", []));
        var latest = (await service.FindByVisitAsync(setup.VisitId))!;
        Assert.Empty(latest.Items);
        Assert.Equal("Chưa kê thuốc", latest.Instructions);
        Assert.False(first.RowVersion.AsSpan().SequenceEqual(latest.RowVersion));
    }

    [IdentitySqlServerFact]
    public async Task Assigned_veterinarian_can_open_and_save_draft_from_prescription_page()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var setup = await SetupAsync(factory);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var loginPage = await client.GetStringAsync("/Account/Login");
        using var login = new FormUrlEncodedContent([
            new("Email", "lan@vet.test"), new("Password", "Integration.Vet123!"),
            new("__RequestVerificationToken", Token(loginPage))]);
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Account/Login", login)).StatusCode);

        var editPage = await client.GetStringAsync($"/BackOffice/Prescriptions/Edit/{setup.VisitId}");
        Assert.Contains("Đơn thuốc", System.Net.WebUtility.HtmlDecode(editPage));
        using var form = new FormUrlEncodedContent([
            new("VisitId", setup.VisitId.ToString()), new("Instructions", "Uống sau ăn"),
            new("Items[0].MedicineId", setup.MedicineId.ToString()), new("Items[0].Dosage", "5 mg"),
            new("Items[0].Route", "Uống"), new("Items[0].Frequency", "2 lần/ngày"),
            new("Items[0].Duration", "5 ngày"), new("Items[0].Quantity", "0.5"),
            new("__RequestVerificationToken", Token(editPage))]);
        using var response = await client.PostAsync("/BackOffice/Prescriptions/Edit", form);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal("Uống sau ăn", await db.Prescriptions.Where(x => x.VisitId == setup.VisitId).Select(x => x.Instructions).SingleAsync());
        Assert.Equal(0.5m, await db.PrescriptionItems.Select(x => x.Quantity).SingleAsync());
    }

    private static string Token(string html) => Regex.Match(html,
        "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"").Groups["token"].Value;

    private static async Task<(int VisitId, string VetUserId, int MedicineId)> SetupAsync(IdentitySqlServerWebApplicationFactory factory)
    {
        using var client = factory.CreateClient();
        await client.GetAsync("/");
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var adminId = await db.Users.Where(x => x.NormalizedEmail == IdentitySqlServerTestEnvironment.BootstrapAdminEmail.ToUpperInvariant())
            .Select(x => x.Id).SingleAsync();
        var vetUserId = await scope.ServiceProvider.GetRequiredService<IUserManagementService>()
            .CreateAsync(new(adminId, "Bác sĩ Lan", "lan@vet.test", "Integration.Vet123!", SystemRoleNames.Veterinarian));
        var vetId = await db.VeterinarianProfiles.Where(x => x.UserId == vetUserId).Select(x => x.Id).SingleAsync();
        var owner = new Owner { OwnerCode = "OWN-000001", FullName = "Nguyễn An", PhoneNumber = "+84901234567", CreatedAt = DateTimeOffset.UtcNow };
        var species = new Species { Code = "DOG", Name = "Chó" };
        var medicine = new Medicine { Code = "MED-001", Name = "Thuốc mẫu", Unit = "viên" };
        db.AddRange(owner, species, medicine);
        await db.SaveChangesAsync();
        var pet = new Pet { PetCode = "PET-000001", OwnerId = owner.Id, SpeciesId = species.Id, Name = "Milu", CreatedAt = DateTimeOffset.UtcNow };
        db.Pets.Add(pet);
        await db.SaveChangesAsync();
        var visit = new Visit { VisitNumber = "V-20260927-0001", PetId = pet.Id, VeterinarianId = vetId,
            PetNameSnapshot = pet.Name, OwnerNameSnapshot = owner.FullName, OwnerPhoneSnapshot = owner.PhoneNumber,
            VeterinarianNameSnapshot = "Bác sĩ Lan", Status = VisitStatus.InProgress,
            CheckedInAt = DateTimeOffset.UtcNow.AddMinutes(-5), StartedAt = DateTimeOffset.UtcNow,
            CheckedInByUserId = adminId };
        db.Visits.Add(visit);
        await db.SaveChangesAsync();
        return (visit.Id, vetUserId, medicine.Id);
    }
}
