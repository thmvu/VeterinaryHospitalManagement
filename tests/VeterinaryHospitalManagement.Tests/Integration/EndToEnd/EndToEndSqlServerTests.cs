using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VeterinaryHospitalManagement.Tests.Infrastructure.Identity;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Entities;
using VeterinaryHospitalManagement.Web.Models.Enums;
using VeterinaryHospitalManagement.Web.Services.Billing;
using VeterinaryHospitalManagement.Web.Services.Identity;
using VeterinaryHospitalManagement.Web.Services.Owners;
using VeterinaryHospitalManagement.Web.Services.Pets;
using VeterinaryHospitalManagement.Web.Services.Reports;
using VeterinaryHospitalManagement.Web.Services.Scheduling;
using VeterinaryHospitalManagement.Web.Services.Visits;
using VisitServiceLine = VeterinaryHospitalManagement.Web.Models.Entities.VisitService;

namespace VeterinaryHospitalManagement.Tests.Integration.EndToEnd;

[Collection(IdentitySqlServerTestCollection.Name)]
public sealed class EndToEndSqlServerTests
{
    [IdentitySqlServerFact]
    public async Task Full_E2E_Happy_Path_From_Owner_Booking_To_Clinical_Checkout_And_Reporting()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<ApplicationDbContext>();
        var userManager = sp.GetRequiredService<IUserManagementService>();
        var ownerService = sp.GetRequiredService<IOwnerService>();
        var petService = sp.GetRequiredService<IPetService>();
        var shiftService = sp.GetRequiredService<IVeterinarianShiftService>();
        var apptService = sp.GetRequiredService<IAppointmentService>();
        var visitService = sp.GetRequiredService<IVisitService>();
        var checkoutService = sp.GetRequiredService<ICheckoutService>();
        var revenueReport = sp.GetRequiredService<IRevenueReportService>();

        // 1. Identify Admin actor
        var admin = await db.Users.SingleAsync(u => u.Email == IdentitySqlServerTestEnvironment.BootstrapAdminEmail);

        // 2. Create Veterinarian & Receptionist accounts
        var vetUserId = await userManager.CreateAsync(new CreateManagedUserRequest(
            admin.Id, "BS. Nguyen Hoang", "vet.e2e@example.test", "VetPassword123!", SystemRoleNames.Veterinarian));
        var cashierUserId = await userManager.CreateAsync(new CreateManagedUserRequest(
            admin.Id, "Le Tan Lan", "receptionist.e2e@example.test", "Receptionist123!", SystemRoleNames.Receptionist));

        var vetProfile = await db.VeterinarianProfiles.SingleAsync(p => p.UserId == vetUserId);

        // 3. Create Doctor Shift
        var shiftStart = DateTimeOffset.UtcNow.Date.AddHours(8);
        var shiftEnd = DateTimeOffset.UtcNow.Date.AddHours(17);
        await shiftService.CreateAsync(new CreateShiftRequest(admin.Id, vetProfile.Id, shiftStart, shiftEnd));

        // 4. Create Catalog items (Service & Medicine)
        var serviceCatalog = new ServiceCatalog { Code = "SVC-E2E-1", Name = "Kham lam sang E2E", Category = "Kham", Price = 120000 };
        var medicine = new Medicine { Code = "MED-E2E-1", Name = "Khang sinh E2E", Unit = "vien" };
        var species = new Species { Code = "CANINE", Name = "Cho" };
        db.AddRange(serviceCatalog, medicine, species);
        await db.SaveChangesAsync();

        var breed = new Breed { SpeciesId = species.Id, Name = "Golden Retriever" };
        db.Breeds.Add(breed);
        await db.SaveChangesAsync();

        // 5. Create Owner & Pet
        var ownerCode = await ownerService.CreateAsync(new CreateOwnerRequest(
            cashierUserId, "Pham Van E2E", "0912345678", "123 Pho Hue", "Chu nuoi E2E"));
        var owner = await db.Owners.SingleAsync(o => o.OwnerCode == ownerCode);

        var petCode = await petService.CreateAsync(new CreatePetRequest(
            cashierUserId, owner.Id, "Max", species.Id, breed.Id, PetSex.Male, new DateOnly(2022, 1, 1), "Vang", "Ngoan"));
        var pet = await db.Pets.SingleAsync(p => p.PetCode == petCode);

        // 6. Book Appointment
        var apptStart = DateTimeOffset.UtcNow.Date.AddHours(10);
        var apptEnd = DateTimeOffset.UtcNow.Date.AddHours(10).AddMinutes(30);
        var apptId = await apptService.CreateAsync(new CreateAppointmentRequest(
            cashierUserId, pet.Id, vetProfile.Id, apptStart, apptEnd, "Kham tong quat E2E"));
        Assert.True(apptId > 0);

        // 7. Reception Check-In (Converts Appointment to Waiting Visit)
        var visitId = await visitService.CheckInFromAppointmentAsync(new CheckInFromAppointmentRequest(apptId, cashierUserId));
        Assert.True(visitId > 0);
        var visit = await db.Visits.SingleAsync(v => v.Id == visitId);
        Assert.Equal(VisitStatus.Waiting, visit.Status);
        Assert.Equal(owner.FullName, visit.OwnerNameSnapshot);
        Assert.Equal(pet.Name, visit.PetNameSnapshot);

        // 8. Doctor Starts Consultation (Waiting -> InProgress)
        await visitService.StartAsync(new StartVisitRequest(visitId, vetUserId, visit.RowVersion));
        visit = await db.Visits.SingleAsync(v => v.Id == visitId);
        Assert.Equal(VisitStatus.InProgress, visit.Status);

        // 9. Clinical Documentation: MedicalRecord, Performed Service, Prescription
        db.MedicalRecords.Add(new MedicalRecord
        {
            VisitId = visitId,
            ChiefComplaint = "Sot va bo an",
            Diagnosis = "Viem phoi nhe",
            WeightKg = 24.5m,
            TemperatureC = 39.2m
        });

        db.VisitServices.Add(new VisitServiceLine
        {
            VisitId = visitId,
            ServiceCatalogId = serviceCatalog.Id,
            ServiceNameSnapshot = serviceCatalog.Name,
            Quantity = 1,
            UnitPrice = serviceCatalog.Price,
            Status = VisitServiceStatus.Performed,
            CreatedAt = DateTimeOffset.UtcNow,
            PerformedAt = DateTimeOffset.UtcNow,
            PerformedByVeterinarianId = vetProfile.Id
        });

        var prescription = new Prescription
        {
            VisitId = visitId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Prescriptions.Add(prescription);
        await db.SaveChangesAsync();

        db.PrescriptionItems.Add(new PrescriptionItem
        {
            PrescriptionId = prescription.Id,
            MedicineId = medicine.Id,
            MedicineNameSnapshot = medicine.Name,
            UnitSnapshot = medicine.Unit,
            Dosage = "1 viên",
            Route = "Đường uống",
            Frequency = "2 lần/ngày",
            Duration = "7 ngày",
            Quantity = 14
        });
        await db.SaveChangesAsync();

        // 10. Complete Consultation (InProgress -> Completed & Documents Finalized)
        visit = await db.Visits.SingleAsync(v => v.Id == visitId);
        await visitService.CompleteAsync(new CompleteVisitRequest(visitId, vetUserId, visit.RowVersion));

        visit = await db.Visits.SingleAsync(v => v.Id == visitId);
        Assert.Equal(VisitStatus.Completed, visit.Status);
        var record = await db.MedicalRecords.SingleAsync(m => m.VisitId == visitId);
        Assert.Equal(ClinicalDocumentStatus.Finalized, record.Status);
        var finalizedPrescription = await db.Prescriptions.SingleAsync(p => p.VisitId == visitId);
        Assert.Equal(ClinicalDocumentStatus.Finalized, finalizedPrescription.Status);

        // 11. Cashier Checkout & Payment Confirmation
        var preview = await checkoutService.PreviewAsync(visitId);
        Assert.Equal(120000, preview.TotalAmount);
        Assert.Single(preview.Items);

        var invoiceId = await checkoutService.ConfirmAsync(new ConfirmCheckoutRequest(visitId, cashierUserId, PaymentMethod.BankTransfer));
        Assert.True(invoiceId > 0);

        var invoice = await db.Invoices.Include(i => i.Items).SingleAsync(i => i.Id == invoiceId);
        Assert.Equal(120000, invoice.TotalAmount);
        Assert.Equal(PaymentMethod.BankTransfer, invoice.PaymentMethod);
        Assert.Equal(owner.FullName, invoice.OwnerNameSnapshot);
        Assert.Equal(pet.Name, invoice.PetNameSnapshot);
        Assert.Single(invoice.Items);
        Assert.Equal(120000, invoice.Items.First().LineTotal);

        // 12. Reporting Query: Revenue reflects this invoice
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var report = await revenueReport.GetAsync(today, today);
        Assert.True(report.InvoiceCount >= 1);
        Assert.Contains(report.Rows, r => r.InvoiceNumber == invoice.InvoiceNumber && r.TotalAmount == 120000);
    }

    [IdentitySqlServerFact]
    public async Task E2E_WalkIn_Consultation_And_Cancellation_Alternatives()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<ApplicationDbContext>();
        var userManager = sp.GetRequiredService<IUserManagementService>();
        var ownerService = sp.GetRequiredService<IOwnerService>();
        var petService = sp.GetRequiredService<IPetService>();
        var shiftService = sp.GetRequiredService<IVeterinarianShiftService>();
        var apptService = sp.GetRequiredService<IAppointmentService>();
        var visitService = sp.GetRequiredService<IVisitService>();

        var admin = await db.Users.SingleAsync(u => u.Email == IdentitySqlServerTestEnvironment.BootstrapAdminEmail);

        var vetUserId = await userManager.CreateAsync(new CreateManagedUserRequest(
            admin.Id, "BS. WalkIn Test", "vet.walkin@example.test", "VetPassword123!", SystemRoleNames.Veterinarian));
        var vetProfile = await db.VeterinarianProfiles.SingleAsync(p => p.UserId == vetUserId);

        var shiftStart = DateTimeOffset.UtcNow.Date.AddHours(8);
        var shiftEnd = DateTimeOffset.UtcNow.Date.AddHours(17);
        await shiftService.CreateAsync(new CreateShiftRequest(admin.Id, vetProfile.Id, shiftStart, shiftEnd));

        var species = new Species { Code = "FELINE", Name = "Meo" };
        db.Species.Add(species);
        await db.SaveChangesAsync();

        var breed = new Breed { SpeciesId = species.Id, Name = "Meo Muap" };
        db.Breeds.Add(breed);
        await db.SaveChangesAsync();

        var ownerCode = await ownerService.CreateAsync(new CreateOwnerRequest(
            admin.Id, "Nguyen Thi WalkIn", "0987654321", null, null));
        var owner = await db.Owners.SingleAsync(o => o.OwnerCode == ownerCode);

        var petCode = await petService.CreateAsync(new CreatePetRequest(
            admin.Id, owner.Id, "Bong", species.Id, breed.Id, PetSex.Female, null, "Trang", null));
        var pet = await db.Pets.SingleAsync(p => p.PetCode == petCode);

        // 1. Walk-in Visit without Appointment
        var walkInVisitId = await visitService.WalkInAsync(new WalkInRequest(pet.Id, vetProfile.Id, admin.Id));
        Assert.True(walkInVisitId > 0);
        var walkInVisit = await db.Visits.SingleAsync(v => v.Id == walkInVisitId);
        Assert.Equal(VisitStatus.Waiting, walkInVisit.Status);
        Assert.Null(walkInVisit.AppointmentId);

        // Cancel Walk-In Visit with reason
        await visitService.CancelAsync(new CancelVisitRequest(walkInVisitId, "Chu nuoi doi y", admin.Id, walkInVisit.RowVersion));
        walkInVisit = await db.Visits.SingleAsync(v => v.Id == walkInVisitId);
        Assert.Equal(VisitStatus.Cancelled, walkInVisit.Status);
        Assert.Equal("Chu nuoi doi y", walkInVisit.CancellationReason);

        // 2. Book appointment then cancel it
        var apptStart = DateTimeOffset.UtcNow.Date.AddHours(14);
        var apptEnd = DateTimeOffset.UtcNow.Date.AddHours(14).AddMinutes(30);
        var apptId = await apptService.CreateAsync(new CreateAppointmentRequest(
            admin.Id, pet.Id, vetProfile.Id, apptStart, apptEnd, "Hen tiem phong"));

        var apptDetail = await apptService.GetDetailsAsync(apptId);
        Assert.NotNull(apptDetail);
        Assert.Equal(AppointmentStatus.Scheduled.ToString(), apptDetail.Status);

        await apptService.CancelAsync(apptId, admin.Id, "Ban cong viec dot xuat", apptDetail.RowVersion);
        apptDetail = await apptService.GetDetailsAsync(apptId);
        Assert.NotNull(apptDetail);
        Assert.Equal(AppointmentStatus.Cancelled.ToString(), apptDetail.Status);
        Assert.Equal("Ban cong viec dot xuat", apptDetail.CancellationReason);
    }
}
