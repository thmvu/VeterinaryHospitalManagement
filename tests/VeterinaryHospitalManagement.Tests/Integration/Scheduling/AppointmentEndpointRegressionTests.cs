using System.Net;
using System.Text.Json;
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

namespace VeterinaryHospitalManagement.Tests.Integration.Scheduling;

[Collection(IdentitySqlServerTestCollection.Name)]
public sealed class AppointmentEndpointRegressionTests
{
    [IdentitySqlServerFact]
    public async Task Calendar_only_permission_shows_shifts_without_appointment_details()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var adminId = await Start(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var sp = scope.ServiceProvider;
            var db = sp.GetRequiredService<ApplicationDbContext>();
            await sp.GetRequiredService<IUserManagementService>().CreateAsync(new(
                adminId, "Lễ tân", "calendar-only@vet.test", "Integration.Vet123!", SystemRoleNames.Receptionist));
            var roleId = await db.Roles.Where(x => x.Name == SystemRoleNames.Receptionist).Select(x => x.Id).SingleAsync();
            var permissionIds = await db.Permissions
                .Where(x => x.Code == PermissionCodes.AppointmentView || x.Code == PermissionCodes.AppointmentCreate)
                .Select(x => x.Id).ToListAsync();
            db.RolePermissions.RemoveRange(await db.RolePermissions
                .Where(x => x.RoleId == roleId && permissionIds.Contains(x.PermissionId)).ToListAsync());

            var owner = new Owner { OwnerCode = "OWN-000001", FullName = "Chủ bí mật", PhoneNumber = "+84901234567", CreatedAt = DateTimeOffset.UtcNow };
            var species = new Species { Code = "DOG", Name = "Chó" };
            db.AddRange(owner, species);
            await db.SaveChangesAsync();
            var pet = new Pet { PetCode = "PET-000001", OwnerId = owner.Id, Name = "Milo", SpeciesId = species.Id, Sex = PetSex.Male, CreatedAt = DateTimeOffset.UtcNow };
            var vet = new VeterinarianProfile { UserId = adminId, DoctorCode = "VET-CAL", IsActive = true };
            db.AddRange(pet, vet);
            await db.SaveChangesAsync();
            var start = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.FromHours(7));
            db.VeterinarianShifts.Add(new VeterinarianShift { VeterinarianId = vet.Id, StartAt = start.ToUniversalTime(), EndAt = start.AddHours(4).ToUniversalTime(), IsActive = true });
            db.Appointments.Add(new Appointment { AppointmentNumber = "APT-CALENDAR-1", PetId = pet.Id, VeterinarianId = vet.Id,
                StartAt = start.ToUniversalTime(), EndAt = start.AddMinutes(30).ToUniversalTime(), Reason = "Lý do bí mật",
                Status = AppointmentStatus.Scheduled, CreatedByUserId = adminId, CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        using var client = Client(factory);
        await Login(client, "calendar-only@vet.test", "Integration.Vet123!");
        var page = await client.GetStringAsync("/BackOffice/Calendar");
        Assert.DoesNotContain("Đặt lịch hẹn mới", page);
        Assert.DoesNotContain("Danh sách bảng", page);
        using var response = await client.GetAsync("/BackOffice/Calendar/Events?start=2026-11-01T00%3A00%3A00%2B07%3A00&end=2026-11-02T00%3A00%3A00%2B07%3A00");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Single(json.RootElement.EnumerateArray());
        Assert.Equal("shift", json.RootElement[0].GetProperty("extendedProps").GetProperty("type").GetString());
        Assert.DoesNotContain("Milo", json.RootElement.ToString());
        Assert.DoesNotContain("Lý do bí mật", json.RootElement.ToString());

        using var adminClient = Client(factory);
        await Login(adminClient, IdentitySqlServerTestEnvironment.BootstrapAdminEmail, IdentitySqlServerTestEnvironment.BootstrapAdminPassword);
        using var adminResponse = await adminClient.GetAsync("/BackOffice/Calendar/Events?start=2026-11-01T00%3A00%3A00%2B07%3A00&end=2026-11-02T00%3A00%3A00%2B07%3A00");
        using var adminJson = JsonDocument.Parse(await adminResponse.Content.ReadAsStringAsync());
        var appointment = adminJson.RootElement.EnumerateArray()
            .Single(x => x.GetProperty("extendedProps").GetProperty("type").GetString() == "appointment");
        Assert.Equal("Milo", appointment.GetProperty("extendedProps").GetProperty("petName").GetString());
        Assert.Equal("Lý do bí mật", appointment.GetProperty("extendedProps").GetProperty("reason").GetString());
    }

    [IdentitySqlServerFact]
    public async Task Invalid_row_version_on_cancel_and_no_show_returns_redirect_instead_of_server_error()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        await Start(factory);
        using var client = Client(factory);
        await Login(client, IdentitySqlServerTestEnvironment.BootstrapAdminEmail, IdentitySqlServerTestEnvironment.BootstrapAdminPassword);
        var token = Token(await client.GetStringAsync("/BackOffice/Appointments/Create"));
        using var cancel = await client.PostAsync("/BackOffice/Appointments/Cancel", new FormUrlEncodedContent([
            new("Id", "1"), new("Reason", "Đổi lịch"), new("RowVersionBase64", "not-base64"), new("__RequestVerificationToken", token)]));
        Assert.Equal(HttpStatusCode.Redirect, cancel.StatusCode);
        using var noShow = await client.PostAsync("/BackOffice/Appointments/MarkNoShow", new FormUrlEncodedContent([
            new("Id", "1"), new("RowVersionBase64", "not-base64"), new("__RequestVerificationToken", token)]));
        Assert.Equal(HttpStatusCode.Redirect, noShow.StatusCode);
    }

    private static HttpClient Client(IdentitySqlServerWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

    private static async Task<string> Start(IdentitySqlServerWebApplicationFactory factory)
    {
        using (var client = Client(factory)) await client.GetAsync("/");
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users
            .Where(x => x.NormalizedEmail == IdentitySqlServerTestEnvironment.BootstrapAdminEmail.ToUpperInvariant())
            .Select(x => x.Id).SingleAsync();
    }

    private static async Task Login(HttpClient client, string email, string password)
    {
        var token = Token(await client.GetStringAsync("/Account/Login"));
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent([
            new("Email", email), new("Password", password), new("RememberMe", "false"), new("__RequestVerificationToken", token)]));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static string Token(string html) =>
        Regex.Match(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"").Groups["token"].Value;
}
