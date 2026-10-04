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
using VeterinaryHospitalManagement.Web.Services.Clinical;
using VeterinaryHospitalManagement.Web.Services.Identity;
using VeterinaryHospitalManagement.Web.Services.Visits;

namespace VeterinaryHospitalManagement.Tests.Integration.Clinical;

[Collection(IdentitySqlServerTestCollection.Name)]
public sealed class PrescriptionPrintSqlServerTests
{
    private const string VetEmail = "print.vet@example.test";
    private const string Password = "Integration.Print123!";
    private const string MedicineName = "Thuốc <script>alert('medicine')</script>";
    private const string Instructions = "Sau ăn <script>alert('instructions')</script>";

    [IdentitySqlServerFact]
    public async Task Admin_and_assigned_vet_print_finalized_snapshots_with_encoded_text_without_writing_data()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var setup = await SetupAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var prescriptions = scope.ServiceProvider.GetRequiredService<IPrescriptionService>();
        await prescriptions.SaveDraftAsync(new(setup.VisitId, setup.VetUserId, null, Instructions,
            [new(null, setup.MedicineId, "5 mg", "Uống", "2 lần/ngày", "5 ngày", 0.5m, "Không dùng <b>quá liều</b>")]));
        db.MedicalRecords.Add(new MedicalRecord { VisitId = setup.VisitId, ChiefComplaint = "Bỏ ăn", Diagnosis = "Viêm da" });
        await db.SaveChangesAsync();
        var visits = scope.ServiceProvider.GetRequiredService<IVisitService>();
        await visits.CompleteAsync(new(setup.VisitId, setup.VetUserId, (await visits.GetDetailsAsync(setup.VisitId))!.RowVersion));

        // Changing live records after completion must not rewrite the clinical printout.
        (await db.Medicines.FindAsync(setup.MedicineId))!.Name = "Tên thuốc mới";
        (await db.Medicines.FindAsync(setup.MedicineId))!.Unit = "lọ";
        (await db.Pets.SingleAsync()).Name = "Thú cưng đổi tên";
        (await db.Owners.SingleAsync()).FullName = "Chủ mới";
        (await db.Users.FindAsync(setup.VetUserId))!.FullName = "Bác sĩ đổi tên";
        await db.SaveChangesAsync();

        using var admin = await LoginAsync(factory, IdentitySqlServerTestEnvironment.BootstrapAdminEmail,
            IdentitySqlServerTestEnvironment.BootstrapAdminPassword);
        using var vet = await LoginAsync(factory, VetEmail, Password);
        db.ChangeTracker.Clear();
        var visitVersion = (await db.Visits.SingleAsync()).RowVersion.ToArray();
        var prescriptionVersion = (await db.Prescriptions.SingleAsync()).RowVersion.ToArray();
        var auditCount = await db.AuditLogs.CountAsync();
        foreach (var client in new[] { admin, vet })
        {
            using var response = await client.GetAsync(PrintUrl(setup.VisitId));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            var decoded = WebUtility.HtmlDecode(html);
            foreach (var expected in new[] { "Milu", "Nguyễn An", "+84901234567", "Bác sĩ Lan", MedicineName,
                         "viên", "5 mg", "Uống", "2 lần/ngày", "5 ngày", Instructions, "Không dùng <b>quá liều</b>" })
                Assert.Contains(expected, decoded);
            var quantityCell = Regex.Match(decoded, "<td[^>]*class=\"quantity\"[^>]*>(?<content>.*?)</td>", RegexOptions.Singleline);
            Assert.True(quantityCell.Success, "The printed medicine must show a quantity and its snapshot unit.");
            var quantityText = Regex.Replace(Regex.Replace(quantityCell.Groups["content"].Value, "<[^>]+>", " "), "\\s+", " ").Trim();
            Assert.Equal("0,5 viên", quantityText);
            foreach (var liveValue in new[] { "Tên thuốc mới", "Thú cưng đổi tên", "Chủ mới", "Bác sĩ đổi tên" })
                Assert.DoesNotContain(liveValue, decoded);
            Assert.DoesNotContain("<script>alert(", html);
            Assert.DoesNotContain("<b>quá liều</b>", html);
            Assert.Contains("&lt;script&gt;", html);
            Assert.Contains("A4", html);
            Assert.Contains("@page", html);
            Assert.DoesNotContain("Đơn giá", decoded);
            Assert.DoesNotContain("Thành tiền", decoded);
            var details = await client.GetStringAsync(DetailsUrl(setup.VisitId));
            Assert.Contains(PrintUrl(setup.VisitId), details);
        }
        db.ChangeTracker.Clear();
        Assert.Equal(visitVersion, (await db.Visits.SingleAsync()).RowVersion);
        Assert.Equal(prescriptionVersion, (await db.Prescriptions.SingleAsync()).RowVersion);
        Assert.Equal(auditCount, await db.AuditLogs.CountAsync());
        Assert.Single(await db.PrescriptionItems.ToListAsync());
        Assert.Empty(await db.Invoices.ToListAsync());
    }

    [IdentitySqlServerFact]
    public async Task Print_requires_authenticated_assigned_active_vet_and_both_print_and_view_grants()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var setup = await SetupAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
        var adminId = await db.Users.Where(x => x.Email == IdentitySqlServerTestEnvironment.BootstrapAdminEmail).Select(x => x.Id).SingleAsync();
        await users.CreateAsync(new(adminId, "Bác sĩ khác", "other.vet@example.test", Password, SystemRoleNames.Veterinarian));
        await users.CreateAsync(new(adminId, "Lễ tân", "print.reception@example.test", Password, SystemRoleNames.Receptionist));
        await AddPrescriptionAsync(db, setup, ClinicalDocumentStatus.Finalized, VisitStatus.Completed, includeItem: true);

        using var anonymous = NewClient(factory);
        using var anonymousResponse = await anonymous.GetAsync(PrintUrl(setup.VisitId));
        Assert.Equal(HttpStatusCode.Redirect, anonymousResponse.StatusCode);
        Assert.Contains("/Account/Login", anonymousResponse.Headers.Location!.OriginalString);
        using var otherVet = await LoginAsync(factory, "other.vet@example.test", Password);
        using var receptionist = await LoginAsync(factory, "print.reception@example.test", Password);
        using var vet = await LoginAsync(factory, VetEmail, Password);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherVet.GetAsync(PrintUrl(setup.VisitId))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await receptionist.GetAsync(PrintUrl(setup.VisitId))).StatusCode);

        var profile = await db.VeterinarianProfiles.SingleAsync(x => x.UserId == setup.VetUserId);
        profile.IsActive = false;
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await vet.GetAsync(PrintUrl(setup.VisitId))).StatusCode);
        profile.IsActive = true;
        await db.SaveChangesAsync();

        var roleId = await db.Roles.Where(x => x.Name == SystemRoleNames.Veterinarian).Select(x => x.Id).SingleAsync();
        var printGrant = await db.RolePermissions.SingleAsync(x => x.RoleId == roleId && x.Permission.Code == PermissionCodes.PrescriptionPrint);
        db.RolePermissions.Remove(printGrant);
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await vet.GetAsync(PrintUrl(setup.VisitId))).StatusCode);
        Assert.DoesNotContain(PrintUrl(setup.VisitId), await vet.GetStringAsync(DetailsUrl(setup.VisitId)));

        db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = printGrant.PermissionId });
        var viewGrant = await db.RolePermissions.SingleAsync(x => x.RoleId == roleId && x.Permission.Code == PermissionCodes.PrescriptionView);
        db.RolePermissions.Remove(viewGrant);
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await vet.GetAsync(PrintUrl(setup.VisitId))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await vet.GetAsync(DetailsUrl(setup.VisitId))).StatusCode);
    }

    [IdentitySqlServerFact]
    public async Task Missing_prescription_returns_404_and_unfinished_or_empty_prescription_returns_409()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var setup = await SetupAsync(factory);
        using var vet = await LoginAsync(factory, VetEmail, Password);
        Assert.Equal(HttpStatusCode.NotFound, (await vet.GetAsync(PrintUrl(int.MaxValue))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await vet.GetAsync(PrintUrl(setup.VisitId))).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await AddPrescriptionAsync(db, setup, ClinicalDocumentStatus.Draft, VisitStatus.Completed, includeItem: true);
        Assert.Equal(HttpStatusCode.Conflict, (await vet.GetAsync(PrintUrl(setup.VisitId))).StatusCode);
        Assert.DoesNotContain(PrintUrl(setup.VisitId), await vet.GetStringAsync(DetailsUrl(setup.VisitId)));

        var prescription = await db.Prescriptions.SingleAsync();
        prescription.Status = ClinicalDocumentStatus.Finalized;
        prescription.FinalizedAt = DateTimeOffset.UtcNow;
        var visit = await db.Visits.SingleAsync();
        visit.Status = VisitStatus.InProgress;
        visit.CompletedAt = null;
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await vet.GetAsync(PrintUrl(setup.VisitId))).StatusCode);
        Assert.DoesNotContain(PrintUrl(setup.VisitId), await vet.GetStringAsync(DetailsUrl(setup.VisitId)));

        visit.Status = VisitStatus.Completed;
        visit.CompletedAt = DateTimeOffset.UtcNow;
        db.PrescriptionItems.RemoveRange(await db.PrescriptionItems.ToListAsync());
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await vet.GetAsync(PrintUrl(setup.VisitId))).StatusCode);
        Assert.DoesNotContain(PrintUrl(setup.VisitId), await vet.GetStringAsync(DetailsUrl(setup.VisitId)));
    }

    private static string PrintUrl(int id) => $"/BackOffice/Prescriptions/Print/{id}";
    private static string DetailsUrl(int id) => $"/BackOffice/Prescriptions/Details/{id}";
    private static HttpClient NewClient(IdentitySqlServerWebApplicationFactory factory) => factory.CreateClient(
        new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

    private static async Task<HttpClient> LoginAsync(IdentitySqlServerWebApplicationFactory factory, string email, string? password = null)
    {
        var client = NewClient(factory);
        var page = await client.GetStringAsync("/Account/Login");
        var token = Regex.Match(page, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"").Groups["token"].Value;
        using var form = new FormUrlEncodedContent([
            new("Email", email), new("Password", password ?? Password), new("__RequestVerificationToken", token)]);
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Account/Login", form)).StatusCode);
        return client;
    }

    private static async Task AddPrescriptionAsync(ApplicationDbContext db, Setup setup,
        ClinicalDocumentStatus status, VisitStatus visitStatus, bool includeItem)
    {
        var visit = (await db.Visits.FindAsync(setup.VisitId))!;
        visit.Status = visitStatus;
        visit.CompletedAt = visitStatus == VisitStatus.Completed ? DateTimeOffset.UtcNow : null;
        var prescription = new Prescription { VisitId = setup.VisitId, CreatedAt = DateTimeOffset.UtcNow,
            Status = status, FinalizedAt = status == ClinicalDocumentStatus.Finalized ? DateTimeOffset.UtcNow : null };
        if (includeItem) prescription.Items.Add(new PrescriptionItem { MedicineId = setup.MedicineId,
            MedicineNameSnapshot = MedicineName, UnitSnapshot = "viên", Dosage = "5 mg", Route = "Uống",
            Frequency = "2 lần/ngày", Duration = "5 ngày", Quantity = 0.5m });
        db.Prescriptions.Add(prescription);
        await db.SaveChangesAsync();
    }

    private sealed record Setup(int VisitId, string VetUserId, int MedicineId);

    private static async Task<Setup> SetupAsync(IdentitySqlServerWebApplicationFactory factory)
    {
        using var client = factory.CreateClient();
        await client.GetAsync("/");
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var adminId = await db.Users.Where(x => x.Email == IdentitySqlServerTestEnvironment.BootstrapAdminEmail).Select(x => x.Id).SingleAsync();
        var vetId = await scope.ServiceProvider.GetRequiredService<IUserManagementService>()
            .CreateAsync(new(adminId, "Bác sĩ Lan", VetEmail, Password, SystemRoleNames.Veterinarian));
        var profileId = await db.VeterinarianProfiles.Where(x => x.UserId == vetId).Select(x => x.Id).SingleAsync();
        var owner = new Owner { OwnerCode = "OWN-000001", FullName = "Nguyễn An", PhoneNumber = "+84901234567", CreatedAt = DateTimeOffset.UtcNow };
        var species = new Species { Code = "DOG", Name = "Chó" };
        var medicine = new Medicine { Code = "MED-001", Name = MedicineName, Unit = "viên" };
        db.AddRange(owner, species, medicine);
        await db.SaveChangesAsync();
        var pet = new Pet { PetCode = "PET-000001", OwnerId = owner.Id, SpeciesId = species.Id, Name = "Milu", CreatedAt = DateTimeOffset.UtcNow };
        db.Pets.Add(pet);
        await db.SaveChangesAsync();
        var visit = new Visit { VisitNumber = "V-20261004-0001", PetId = pet.Id, VeterinarianId = profileId,
            PetNameSnapshot = pet.Name, OwnerNameSnapshot = owner.FullName, OwnerPhoneSnapshot = owner.PhoneNumber,
            VeterinarianNameSnapshot = "Bác sĩ Lan", Status = VisitStatus.InProgress,
            CheckedInAt = DateTimeOffset.UtcNow.AddMinutes(-5), StartedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            CheckedInByUserId = adminId };
        db.Visits.Add(visit);
        await db.SaveChangesAsync();
        return new Setup(visit.Id, vetId, medicine.Id);
    }
}
