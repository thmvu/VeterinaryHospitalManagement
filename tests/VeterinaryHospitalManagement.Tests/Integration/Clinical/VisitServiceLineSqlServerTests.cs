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
using VeterinaryHospitalManagement.Web.Services.Clinical;
using VeterinaryHospitalManagement.Web.Services.Identity;

namespace VeterinaryHospitalManagement.Tests.Integration.Clinical;

[Collection(IdentitySqlServerTestCollection.Name)]
public sealed class VisitServiceLineSqlServerTests
{
    [IdentitySqlServerFact]
    public async Task Assigned_vet_adds_repeated_lines_with_price_snapshot_and_resolves_each_line()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var (visitId, vetUserId, catalogId) = await SetupAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IClinicalServiceService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var firstId = await service.AddAsync(new(visitId, vetUserId, catalogId, 1));
        var secondId = await service.AddAsync(new(visitId, vetUserId, catalogId, 2));
        (await db.ServiceCatalogs.FindAsync(catalogId))!.Name = "Tên mới";
        (await db.ServiceCatalogs.FindAsync(catalogId))!.Price = 300000;
        await db.SaveChangesAsync();
        var lines = await service.ListAsync(visitId);
        Assert.Equal(2, lines.Count);
        Assert.All(lines, x => { Assert.Equal("Khám tổng quát", x.Name); Assert.Equal(100000, x.UnitPrice); });
        await service.PerformAsync(new(firstId, vetUserId, lines.Single(x => x.Id == firstId).RowVersion));
        await service.CancelAsync(new(secondId, vetUserId, lines.Single(x => x.Id == secondId).RowVersion, "Không cần nữa"));
        lines = await service.ListAsync(visitId);
        Assert.Equal(VisitServiceStatus.Performed, lines.Single(x => x.Id == firstId).Status);
        Assert.Equal(VisitServiceStatus.Cancelled, lines.Single(x => x.Id == secondId).Status);
        Assert.Equal(100000, lines.Where(x => x.Status == VisitServiceStatus.Performed).Sum(x => x.Quantity * x.UnitPrice));
    }

    [IdentitySqlServerFact]
    public async Task Assigned_vet_adds_service_from_visit_page()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var (visitId, _, catalogId) = await SetupAsync(factory);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
            { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        var loginPage = await client.GetStringAsync("/Account/Login");
        using var login = new FormUrlEncodedContent([
            new("Email", "lan@vet.test"), new("Password", "Integration.Vet123!"),
            new("__RequestVerificationToken", Token(loginPage))]);
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Account/Login", login)).StatusCode);
        var page = await client.GetStringAsync($"/BackOffice/Visits/Details/{visitId}");
        Assert.Contains("Dịch vụ trong lượt khám", page);
        using var form = new FormUrlEncodedContent([
            new("visitId", visitId.ToString()), new("serviceCatalogId", catalogId.ToString()),
            new("quantity", "1.5"), new("__RequestVerificationToken", Token(page))]);
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/BackOffice/Visits/AddService", form)).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1.5m, await db.VisitServices.Select(x => x.Quantity).SingleAsync());
    }

    private static string Token(string html) => Regex.Match(html,
        "<input[^>]*name=\\\"__RequestVerificationToken\\\"[^>]*value=\\\"(?<token>[^\\\"]+)\\\"").Groups["token"].Value;

    [IdentitySqlServerFact]
    public async Task Rejects_wrong_actor_stale_version_and_non_pending_transition()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var (visitId, vetUserId, catalogId) = await SetupAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IClinicalServiceService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var adminId = await db.Users.Where(x => x.NormalizedEmail == IdentitySqlServerTestEnvironment.BootstrapAdminEmail.ToUpperInvariant()).Select(x => x.Id).SingleAsync();
        await Assert.ThrowsAsync<ClinicalServiceAccessException>(() => service.AddAsync(new(visitId, adminId, catalogId, 1)));
        await Assert.ThrowsAsync<ClinicalServiceManagementException>(() => service.AddAsync(new(visitId, vetUserId, catalogId, 0)));
        var id = await service.AddAsync(new(visitId, vetUserId, catalogId, 1));
        await Assert.ThrowsAsync<ClinicalServiceManagementException>(() => service.PerformAsync(new(id, vetUserId, new byte[8])));
        var line = (await service.ListAsync(visitId)).Single();
        await service.PerformAsync(new(id, vetUserId, line.RowVersion));
        await Assert.ThrowsAsync<ClinicalServiceManagementException>(() => service.CancelAsync(new(id, vetUserId, line.RowVersion, "Đổi ý")));
    }

    private static async Task<(int VisitId, string VetUserId, int CatalogId)> SetupAsync(IdentitySqlServerWebApplicationFactory factory)
    {
        using var client = factory.CreateClient();
        await client.GetAsync("/");
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var adminId = await db.Users.Where(x => x.NormalizedEmail == IdentitySqlServerTestEnvironment.BootstrapAdminEmail.ToUpperInvariant()).Select(x => x.Id).SingleAsync();
        var vetUserId = await scope.ServiceProvider.GetRequiredService<IUserManagementService>().CreateAsync(new(adminId, "Bác sĩ Lan", "lan@vet.test", "Integration.Vet123!", SystemRoleNames.Veterinarian));
        var vetId = await db.VeterinarianProfiles.Where(x => x.UserId == vetUserId).Select(x => x.Id).SingleAsync();
        var owner = new Owner { OwnerCode = "OWN-000001", FullName = "Nguyễn An", PhoneNumber = "+84901234567", CreatedAt = DateTimeOffset.UtcNow };
        var species = new Species { Code = "DOG", Name = "Chó" };
        var catalog = new ServiceCatalog { Code = "EXAM", Name = "Khám tổng quát", Category = "Khám", Price = 100000 };
        db.AddRange(owner, species, catalog);
        await db.SaveChangesAsync();
        var pet = new Pet { PetCode = "PET-000001", OwnerId = owner.Id, SpeciesId = species.Id, Name = "Milu", CreatedAt = DateTimeOffset.UtcNow };
        db.Pets.Add(pet);
        await db.SaveChangesAsync();
        var visit = new Visit { VisitNumber = "V-20260927-0001", PetId = pet.Id, VeterinarianId = vetId, PetNameSnapshot = pet.Name,
            OwnerNameSnapshot = owner.FullName, OwnerPhoneSnapshot = owner.PhoneNumber, VeterinarianNameSnapshot = "Bác sĩ Lan",
            Status = VisitStatus.InProgress, CheckedInAt = DateTimeOffset.UtcNow.AddMinutes(-5), StartedAt = DateTimeOffset.UtcNow,
            CheckedInByUserId = adminId };
        db.Visits.Add(visit);
        await db.SaveChangesAsync();
        return (visit.Id, vetUserId, catalog.Id);
    }
}
