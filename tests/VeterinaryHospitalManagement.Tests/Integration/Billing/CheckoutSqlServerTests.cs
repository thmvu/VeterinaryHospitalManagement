using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Text.RegularExpressions;
using VeterinaryHospitalManagement.Tests.Infrastructure.Identity;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Entities;
using VeterinaryHospitalManagement.Web.Models.Enums;
using VeterinaryHospitalManagement.Web.Services.Billing;
using VeterinaryHospitalManagement.Web.Services.Identity;
using VisitServiceLine = VeterinaryHospitalManagement.Web.Models.Entities.VisitService;

namespace VeterinaryHospitalManagement.Tests.Integration.Billing;

[Collection(IdentitySqlServerTestCollection.Name)]
public sealed class CheckoutSqlServerTests
{
    [IdentitySqlServerFact]
    public async Task Confirm_uses_performed_snapshots_and_server_totals_then_repeat_returns_same_invoice()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var setup = await SetupAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var checkout = scope.ServiceProvider.GetRequiredService<ICheckoutService>();
        db.VisitServices.AddRange(
            Line(setup.VisitId, setup.CatalogId, "Khám", 1.5m, 100001, VisitServiceStatus.Performed, setup.VetId),
            Line(setup.VisitId, setup.CatalogId, "Khám lại", 1, 50000, VisitServiceStatus.Performed, setup.VetId),
            Line(setup.VisitId, setup.CatalogId, "Không làm", 1, 999999, VisitServiceStatus.Cancelled, setup.VetId));
        await db.SaveChangesAsync();
        var preview = await checkout.PreviewAsync(setup.VisitId);
        Assert.Equal(2, preview.Items.Count);
        Assert.Equal(200002, preview.TotalAmount);
        (await db.ServiceCatalogs.FindAsync(setup.CatalogId))!.Name = "Tên mới";
        (await db.ServiceCatalogs.FindAsync(setup.CatalogId))!.Price = 1;
        await db.SaveChangesAsync();

        var firstId = await checkout.ConfirmAsync(new(setup.VisitId, setup.CashierUserId, PaymentMethod.Cash));
        var secondId = await checkout.ConfirmAsync(new(setup.VisitId, setup.CashierUserId, PaymentMethod.BankTransfer));
        Assert.Equal(firstId, secondId);
        db.ChangeTracker.Clear();
        var invoice = await db.Invoices.Include(x => x.Items).SingleAsync();
        Assert.Equal(200002, invoice.TotalAmount);
        Assert.Equal(PaymentMethod.Cash, invoice.PaymentMethod);
        Assert.Equal("Nguyễn An", invoice.OwnerNameSnapshot);
        Assert.Equal("Milu", invoice.PetNameSnapshot);
        Assert.Equal(2, invoice.Items.Count);
        Assert.Contains(invoice.Items, x => x.DescriptionSnapshot == "Khám" && x.LineTotal == 150002);
        Assert.All(invoice.Items, x => Assert.Equal(setup.VisitId,
            db.VisitServices.Single(s => s.Id == x.VisitServiceId).VisitId));
        Assert.Equal(1, await db.AuditLogs.CountAsync(x => x.Action == "Invoice.Paid"));
        (await db.Users.FindAsync(setup.CashierUserId))!.FullName = "Tên lễ tân mới";
        await db.SaveChangesAsync();
        Assert.Equal("Lễ tân Mai", (await checkout.FindAsync(firstId))!.ProcessedByName);
    }

    [IdentitySqlServerFact]
    public async Task Simultaneous_confirmation_creates_only_one_invoice()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var setup = await SetupAsync(factory);
        async Task<int> ConfirmAsync()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ICheckoutService>()
                .ConfirmAsync(new(setup.VisitId, setup.CashierUserId, PaymentMethod.Cash));
        }
        var results = await Task.WhenAll(ConfirmAsync(), ConfirmAsync());
        Assert.Equal(results[0], results[1]);
        await using var verifyScope = factory.Services.CreateAsyncScope();
        Assert.Equal(1, await verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Invoices.CountAsync());
    }

    [IdentitySqlServerFact]
    public async Task Receptionist_can_preview_and_confirm_checkout_from_web_page()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var setup = await SetupAsync(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.VisitServices.Add(Line(setup.VisitId, setup.CatalogId, "Khám", 1, 100001,
                VisitServiceStatus.Performed, setup.VetId));
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var loginPage = await client.GetStringAsync("/Account/Login");
        using var login = new FormUrlEncodedContent([
            new("Email", "mai@vet.test"), new("Password", "Integration.Rec123!"),
            new("__RequestVerificationToken", Token(loginPage))]);
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Account/Login", login)).StatusCode);
        var list = await client.GetStringAsync("/BackOffice/Invoices");
        Assert.Contains("V-20260927-0001", list);
        var preview = await client.GetStringAsync($"/BackOffice/Invoices/Preview/{setup.VisitId}");
        Assert.Contains("100.001", preview);
        using var form = new FormUrlEncodedContent([
            new("VisitId", setup.VisitId.ToString()), new("PaymentMethod", "BankTransfer"),
            new("__RequestVerificationToken", Token(preview))]);
        var response = await client.PostAsync("/BackOffice/Invoices/Confirm", form);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var invoice = await verifyDb.Invoices.SingleAsync();
        Assert.Equal(PaymentMethod.BankTransfer, invoice.PaymentMethod);
        Assert.Contains(invoice.Id.ToString(), response.Headers.Location!.ToString());
        var detail = await client.GetStringAsync(response.Headers.Location);
        Assert.Contains(invoice.InvoiceNumber, detail);
    }

    private static string Token(string html) => Regex.Match(html,
        "<input[^>]*name=\\\"__RequestVerificationToken\\\"[^>]*value=\\\"(?<token>[^\\\"]+)\\\"").Groups["token"].Value;

    [IdentitySqlServerFact]
    public async Task Zero_service_visit_can_be_paid_once_and_invalid_state_or_actor_is_rejected()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var setup = await SetupAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var checkout = scope.ServiceProvider.GetRequiredService<ICheckoutService>();
        await Assert.ThrowsAsync<CheckoutManagementException>(() => checkout.ConfirmAsync(
            new(setup.VisitId, setup.VetUserId, PaymentMethod.Cash)));
        var visit = (await db.Visits.FindAsync(setup.VisitId))!;
        visit.Status = VisitStatus.InProgress;
        visit.CompletedAt = null;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<CheckoutManagementException>(() => checkout.ConfirmAsync(
            new(setup.VisitId, setup.CashierUserId, PaymentMethod.Cash)));
        visit.Status = VisitStatus.Completed;
        visit.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        var id = await checkout.ConfirmAsync(new(setup.VisitId, setup.CashierUserId, PaymentMethod.BankTransfer));
        var invoice = await db.Invoices.Include(x => x.Items).SingleAsync(x => x.Id == id);
        Assert.Empty(invoice.Items);
        Assert.Equal(0, invoice.TotalAmount);
    }

    private static VisitServiceLine Line(int visitId, int catalogId, string name, decimal quantity,
        decimal unitPrice, VisitServiceStatus status, int vetId) => new()
    {
        VisitId = visitId, ServiceCatalogId = catalogId, ServiceNameSnapshot = name,
        Quantity = quantity, UnitPrice = unitPrice, Status = status, CreatedAt = DateTimeOffset.UtcNow,
        PerformedAt = status == VisitServiceStatus.Performed ? DateTimeOffset.UtcNow : null,
        PerformedByVeterinarianId = status == VisitServiceStatus.Performed ? vetId : null,
        CancellationReason = status == VisitServiceStatus.Cancelled ? "Không cần" : null
    };

    private static async Task<(int VisitId, int CatalogId, int VetId, string VetUserId, string CashierUserId)> SetupAsync(
        IdentitySqlServerWebApplicationFactory factory)
    {
        using var client = factory.CreateClient();
        await client.GetAsync("/");
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
        var adminId = await db.Users.Where(x => x.NormalizedEmail == IdentitySqlServerTestEnvironment.BootstrapAdminEmail.ToUpperInvariant())
            .Select(x => x.Id).SingleAsync();
        var vetUserId = await users.CreateAsync(new(adminId, "Bác sĩ Lan", "lan@vet.test", "Integration.Vet123!", SystemRoleNames.Veterinarian));
        var cashierUserId = await users.CreateAsync(new(adminId, "Lễ tân Mai", "mai@vet.test", "Integration.Rec123!", SystemRoleNames.Receptionist));
        var vetId = await db.VeterinarianProfiles.Where(x => x.UserId == vetUserId).Select(x => x.Id).SingleAsync();
        var owner = new Owner { OwnerCode = "OWN-000001", FullName = "Nguyễn An", PhoneNumber = "+84901234567", CreatedAt = DateTimeOffset.UtcNow };
        var species = new Species { Code = "DOG", Name = "Chó" };
        var catalog = new ServiceCatalog { Code = "EXAM", Name = "Khám", Category = "Khám", Price = 100001 };
        db.AddRange(owner, species, catalog);
        await db.SaveChangesAsync();
        var pet = new Pet { PetCode = "PET-000001", OwnerId = owner.Id, SpeciesId = species.Id, Name = "Milu", CreatedAt = DateTimeOffset.UtcNow };
        db.Pets.Add(pet);
        await db.SaveChangesAsync();
        var visit = new Visit { VisitNumber = "V-20260927-0001", PetId = pet.Id, VeterinarianId = vetId,
            PetNameSnapshot = pet.Name, OwnerNameSnapshot = owner.FullName, OwnerPhoneSnapshot = owner.PhoneNumber,
            VeterinarianNameSnapshot = "Bác sĩ Lan", Status = VisitStatus.Completed,
            CheckedInAt = DateTimeOffset.UtcNow.AddMinutes(-10), StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-1), CheckedInByUserId = adminId };
        db.Visits.Add(visit);
        await db.SaveChangesAsync();
        return (visit.Id, catalog.Id, vetId, vetUserId, cashierUserId);
    }
}
