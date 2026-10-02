using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VeterinaryHospitalManagement.Tests.Infrastructure.Identity;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Entities;
using VeterinaryHospitalManagement.Web.Models.Enums;
using VeterinaryHospitalManagement.Web.Services.Billing;
using VeterinaryHospitalManagement.Web.Services.Clinical;
using VeterinaryHospitalManagement.Web.Services.Identity;
using VeterinaryHospitalManagement.Web.Services.Owners;
using VeterinaryHospitalManagement.Web.Services.Pets;
using VeterinaryHospitalManagement.Web.Services.Reports;
using VeterinaryHospitalManagement.Web.Services.Scheduling;
using VeterinaryHospitalManagement.Web.Services.Visits;

namespace VeterinaryHospitalManagement.Tests.Integration.EndToEnd;

[Collection(IdentitySqlServerTestCollection.Name)]
public sealed class EndToEndSqlServerTests
{
    [IdentitySqlServerFact]
    public async Task Full_E2E_Happy_Path_From_Owner_Booking_To_Clinical_Checkout_And_Reporting()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        var clock = new ScenarioClock(new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.FromHours(7)));
        using var baseFactory = new IdentitySqlServerWebApplicationFactory();
        using var factory = WithClock(baseFactory, clock);
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
        var visitReport = sp.GetRequiredService<IVisitReportService>();
        var serviceReport = sp.GetRequiredService<IServiceRevenueReportService>();
        var medicalRecords = sp.GetRequiredService<IMedicalRecordService>();
        var prescriptions = sp.GetRequiredService<IPrescriptionService>();
        var clinicalServices = sp.GetRequiredService<IClinicalServiceService>();

        // 1. Identify Admin actor
        var admin = await db.Users.SingleAsync(u => u.Email == IdentitySqlServerTestEnvironment.BootstrapAdminEmail);

        // 2. Create Veterinarian & Receptionist accounts
        var vetUserId = await userManager.CreateAsync(new CreateManagedUserRequest(
            admin.Id, "BS. Nguyen Hoang", "vet.e2e@example.test", "VetPassword123!", SystemRoleNames.Veterinarian));
        var cashierUserId = await userManager.CreateAsync(new CreateManagedUserRequest(
            admin.Id, "Le Tan Lan", "receptionist.e2e@example.test", "Receptionist123!", SystemRoleNames.Receptionist));

        var vetProfile = await db.VeterinarianProfiles.SingleAsync(p => p.UserId == vetUserId);

        // 3. Create Doctor Shift
        var shiftStart = clock.Current.AddHours(-2);
        var shiftEnd = clock.Current.AddHours(7);
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
        var apptStart = clock.Current;
        var apptEnd = apptStart.AddMinutes(30);
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
        Assert.Equal(visitId, await visitService.CheckInFromAppointmentAsync(new(apptId, cashierUserId)));
        Assert.Equal(1, await db.Visits.CountAsync(v => v.AppointmentId == apptId));

        // 8. Doctor Starts Consultation (Waiting -> InProgress)
        await visitService.StartAsync(new StartVisitRequest(visitId, vetUserId, visit.RowVersion));
        visit = await db.Visits.SingleAsync(v => v.Id == visitId);
        Assert.Equal(VisitStatus.InProgress, visit.Status);

        // 9. Clinical Documentation: MedicalRecord, Performed Service, Prescription
        await medicalRecords.SaveDraftAsync(new(visitId, vetUserId, null, "Sot va bo an", null,
            24.5m, 39.2m, "Viem phoi nhe", null, null));
        await prescriptions.SaveDraftAsync(new(visitId, vetUserId, null, "Theo dõi tại nhà",
            [new(null, medicine.Id, "1 viên", "Đường uống", "2 lần/ngày", "7 ngày", 14, null)]));
        var performedLineId = await clinicalServices.AddAsync(new(visitId, vetUserId, serviceCatalog.Id, 1));
        var cancelledLineId = await clinicalServices.AddAsync(new(visitId, vetUserId, serviceCatalog.Id, 2));
        var serviceLines = await clinicalServices.ListAsync(visitId);
        await clinicalServices.PerformAsync(new(performedLineId, vetUserId,
            serviceLines.Single(x => x.Id == performedLineId).RowVersion));
        await clinicalServices.CancelAsync(new(cancelledLineId, vetUserId,
            serviceLines.Single(x => x.Id == cancelledLineId).RowVersion, "Không thực hiện dịch vụ mẫu này"));

        // Changing the current catalog must not change the saved treatment price or name.
        serviceCatalog.Name = "Tên danh mục đã đổi";
        serviceCatalog.Price = 900000;
        await db.SaveChangesAsync();

        // 10. Complete Consultation (InProgress -> Completed & Documents Finalized)
        clock.Current = apptEnd;
        visit = await db.Visits.SingleAsync(v => v.Id == visitId);
        await visitService.CompleteAsync(new CompleteVisitRequest(visitId, vetUserId, visit.RowVersion));

        visit = await db.Visits.SingleAsync(v => v.Id == visitId);
        Assert.Equal(VisitStatus.Completed, visit.Status);
        var record = await db.MedicalRecords.AsNoTracking().SingleAsync(m => m.VisitId == visitId);
        Assert.Equal(ClinicalDocumentStatus.Finalized, record.Status);
        Assert.Equal("Viem phoi nhe", record.Diagnosis);
        Assert.Equal(24.5m, record.WeightKg);
        Assert.Equal(39.2m, record.TemperatureC);
        var finalizedPrescription = await db.Prescriptions.AsNoTracking().Include(p => p.Items)
            .SingleAsync(p => p.VisitId == visitId);
        Assert.Equal(ClinicalDocumentStatus.Finalized, finalizedPrescription.Status);
        var prescriptionItem = Assert.Single(finalizedPrescription.Items);
        Assert.Equal("Khang sinh E2E", prescriptionItem.MedicineNameSnapshot);
        Assert.Equal(14, prescriptionItem.Quantity);

        // 11. Cashier Checkout & Payment Confirmation
        // Payment on the next Vietnam date must appear in that date's revenue, not the intake date.
        clock.Current = apptStart.AddDays(1);
        var preview = await checkoutService.PreviewAsync(visitId);
        Assert.Equal(120000, preview.TotalAmount);
        Assert.Single(preview.Items);

        var invoiceId = await checkoutService.ConfirmAsync(new ConfirmCheckoutRequest(visitId, cashierUserId, PaymentMethod.BankTransfer));
        Assert.True(invoiceId > 0);
        Assert.Equal(invoiceId, await checkoutService.ConfirmAsync(new(visitId, cashierUserId, PaymentMethod.Cash)));
        Assert.Equal(1, await db.Invoices.CountAsync(i => i.VisitId == visitId));

        var invoice = await db.Invoices.AsNoTracking().Include(i => i.Items).SingleAsync(i => i.Id == invoiceId);
        Assert.Equal(120000, invoice.TotalAmount);
        Assert.Equal(PaymentMethod.BankTransfer, invoice.PaymentMethod);
        Assert.Equal(clock.GetUtcNow(), invoice.PaidAt);
        Assert.Equal(owner.FullName, invoice.OwnerNameSnapshot);
        Assert.Equal(pet.Name, invoice.PetNameSnapshot);
        Assert.Single(invoice.Items);
        Assert.Equal(120000, invoice.Items.First().LineTotal);
        Assert.Equal("Kham lam sang E2E", invoice.Items.First().DescriptionSnapshot);
        Assert.Equal(performedLineId, invoice.Items.First().VisitServiceId);
        var invoiceEntityId = invoiceId.ToString(CultureInfo.InvariantCulture);
        var visitEntityId = visitId.ToString(CultureInfo.InvariantCulture);
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.Action == "Invoice.Paid" && a.EntityId == invoiceEntityId));
        Assert.Equal(1, await db.AuditLogs.CountAsync(a => a.Action == "Visit.CheckedIn" && a.EntityId == visitEntityId));

        // 12. Reporting Query: Revenue reflects this invoice
        var intakeDay = DateOnly.FromDateTime(apptStart.DateTime);
        var paidDay = intakeDay.AddDays(1);
        var report = await revenueReport.GetAsync(paidDay, paidDay);
        var revenueRow = Assert.Single(report.Rows);
        Assert.Equal(invoiceId, revenueRow.InvoiceId);
        Assert.Equal(invoice.InvoiceNumber, revenueRow.InvoiceNumber);
        Assert.Equal(120000, report.TotalAmount);
        Assert.Empty((await revenueReport.GetAsync(intakeDay, intakeDay)).Rows);
        var servicesReport = await serviceReport.GetAsync(paidDay, paidDay);
        var serviceRow = Assert.Single(servicesReport.Rows);
        Assert.Equal("Kham lam sang E2E", serviceRow.ServiceName);
        Assert.Equal(1, serviceRow.Quantity);
        Assert.Equal(120000, servicesReport.TotalRevenue);
        Assert.Equal(0, (await serviceReport.GetAsync(intakeDay, intakeDay)).TotalLineCount);
        var visitsReport = await visitReport.GetAsync(intakeDay, intakeDay);
        Assert.Equal(1, visitsReport.TotalCount);
        Assert.Equal(1, visitsReport.Statuses.Single(x => x.Status == VisitStatus.Completed).Count);
        Assert.Equal(0, (await visitReport.GetAsync(paidDay, paidDay)).TotalCount);
    }

    [IdentitySqlServerFact]
    public async Task E2E_WalkIn_Consultation_And_Cancellation_Alternatives()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        var clock = new ScenarioClock(new DateTimeOffset(2026, 10, 2, 10, 0, 0, TimeSpan.FromHours(7)));
        using var baseFactory = new IdentitySqlServerWebApplicationFactory();
        using var factory = WithClock(baseFactory, clock);
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

        var shiftStart = clock.Current.AddHours(-2);
        var shiftEnd = clock.Current.AddHours(7);
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
        var apptStart = clock.Current.AddHours(4);
        var apptEnd = apptStart.AddMinutes(30);
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

        // A cancelled appointment releases its slot; a later appointment can be marked absent after EndAt.
        var noShowId = await apptService.CreateAsync(new(admin.Id, pet.Id, vetProfile.Id,
            apptStart, apptEnd, "Hen lai sau khi huy"));
        var noShowDetail = await apptService.GetDetailsAsync(noShowId);
        Assert.NotNull(noShowDetail);
        clock.Current = apptEnd.AddMinutes(1);
        await apptService.MarkNoShowAsync(noShowId, admin.Id, noShowDetail.RowVersion);
        Assert.Equal(AppointmentStatus.NoShow.ToString(), (await apptService.GetDetailsAsync(noShowId))!.Status);
        Assert.Equal(1, await db.Visits.CountAsync());
        Assert.False(await db.Invoices.AnyAsync());
        var visitsReport = await sp.GetRequiredService<IVisitReportService>()
            .GetAsync(new DateOnly(2026, 10, 2), new DateOnly(2026, 10, 2));
        Assert.Equal(1, visitsReport.TotalCount);
        Assert.Equal(1, visitsReport.Statuses.Single(x => x.Status == VisitStatus.Cancelled).Count);
    }

    private static WebApplicationFactory<Program> WithClock(
        IdentitySqlServerWebApplicationFactory factory, TimeProvider clock) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton(clock);
        }));

    private sealed class ScenarioClock(DateTimeOffset current) : TimeProvider
    {
        public DateTimeOffset Current { get; set; } = current;
        public override DateTimeOffset GetUtcNow() => Current.ToUniversalTime();
    }
}
