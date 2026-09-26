using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VeterinaryHospitalManagement.Tests.Infrastructure.Identity;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Services.Identity;
using VeterinaryHospitalManagement.Web.Services.Scheduling;
using VeterinaryHospitalManagement.Web.Services.Veterinarians;

namespace VeterinaryHospitalManagement.Tests.Integration.Scheduling;

[Collection(IdentitySqlServerTestCollection.Name)]
public sealed class ScheduleEndpointAuthorizationTests
{
    private const string Index = "/BackOffice/Schedules";

    [IdentitySqlServerFact]
    public async Task Anonymous_is_challenged()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var f = new IdentitySqlServerWebApplicationFactory();
        using var c = Client(f);
        using var r = await c.GetAsync(Index);
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal("/Account/Login", r.Headers.Location?.AbsolutePath);
    }

    [IdentitySqlServerFact]
    public async Task Receptionist_and_veterinarian_are_forbidden_while_manager_and_admin_reach_index()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var f = new IdentitySqlServerWebApplicationFactory();
        var admin = await Start(f);

        foreach (var role in new[] { SystemRoleNames.Receptionist, SystemRoleNames.Veterinarian, SystemRoleNames.Manager })
        {
            await AddUser(f, admin, $"{role.ToLowerInvariant()}@sched.test", role, "Integration.Vet123!");
        }

        foreach (var pair in new[]
        {
            (SystemRoleNames.Receptionist, HttpStatusCode.Forbidden),
            (SystemRoleNames.Veterinarian, HttpStatusCode.Forbidden),
            (SystemRoleNames.Manager, HttpStatusCode.OK)
        })
        {
            using var c = Client(f);
            await Login(c, $"{pair.Item1.ToLowerInvariant()}@sched.test", "Integration.Vet123!");
            Assert.Equal(pair.Item2, (await c.GetAsync(Index)).StatusCode);
        }

        using var ac = Client(f);
        await Login(ac, IdentitySqlServerTestEnvironment.BootstrapAdminEmail, IdentitySqlServerTestEnvironment.BootstrapAdminPassword);
        Assert.Equal(HttpStatusCode.OK, (await ac.GetAsync(Index)).StatusCode);
    }

    [IdentitySqlServerFact]
    public async Task Invalid_create_post_redisplays_validation()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var f = new IdentitySqlServerWebApplicationFactory();
        await Start(f);
        using var c = Client(f);
        await Login(c, IdentitySqlServerTestEnvironment.BootstrapAdminEmail, IdentitySqlServerTestEnvironment.BootstrapAdminPassword);

        var page = await c.GetStringAsync(Index + "/Create");
        using var form = new FormUrlEncodedContent([
            new("VeterinarianId", "0"),
            new("StartAtLocal", ""),
            new("EndAtLocal", ""),
            new("__RequestVerificationToken", Token(page))
        ]);

        var r = await c.PostAsync(Index + "/Create", form);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var content = await r.Content.ReadAsStringAsync();
        Assert.Contains("bác sĩ", content);
    }

    [IdentitySqlServerFact]
    public async Task Stale_edit_post_redisplays_conflict()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var f = new IdentitySqlServerWebApplicationFactory();
        var actor = await Start(f);
        var user = await AddUser(f, actor, "stale-shift-end@sched.test", SystemRoleNames.Veterinarian, "Integration.Vet123!");

        int shiftId;
        string stale;
        await using (var s = f.Services.CreateAsyncScope())
        {
            var vetService = s.ServiceProvider.GetRequiredService<IVeterinarianProfileService>();
            var vetId = await vetService.CreateAsync(new(actor, user, null));

            var shiftService = s.ServiceProvider.GetRequiredService<IVeterinarianShiftService>();
            var start = DateTimeOffset.UtcNow.AddDays(1);
            var end = start.AddHours(4);
            shiftId = await shiftService.CreateAsync(new(actor, vetId, start, end));

            var original = (await shiftService.FindAsync(shiftId))!;
            stale = Convert.ToBase64String(original.RowVersion);

            // Update shift once to change rowversion
            await shiftService.UpdateAsync(new(actor, shiftId, original.RowVersion, start, end.AddHours(1)));
        }

        using var c = Client(f);
        await Login(c, IdentitySqlServerTestEnvironment.BootstrapAdminEmail, IdentitySqlServerTestEnvironment.BootstrapAdminPassword);

        var page = await c.GetStringAsync($"{Index}/Edit/{shiftId}");
        using var form = new FormUrlEncodedContent([
            new("Id", shiftId.ToString()),
            new("RowVersion", stale),
            new("StartAtLocal", DateTime.Today.AddDays(1).AddHours(8).ToString("yyyy-MM-ddTHH:mm")),
            new("EndAtLocal", DateTime.Today.AddDays(1).AddHours(12).ToString("yyyy-MM-ddTHH:mm")),
            new("__RequestVerificationToken", Token(page))
        ]);

        var r = await c.PostAsync(Index + "/Edit", form);
        var html = await r.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("đã được thay đổi", WebUtility.HtmlDecode(html));
    }

    private static HttpClient Client(IdentitySqlServerWebApplicationFactory f) =>
        f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });

    private static async Task<string> Start(IdentitySqlServerWebApplicationFactory f)
    {
        using var c = Client(f);
        await c.GetAsync("/");
        await using var s = f.Services.CreateAsyncScope();
        return await s.ServiceProvider.GetRequiredService<Web.Data.ApplicationDbContext>().Users
            .Where(x => x.NormalizedEmail == IdentitySqlServerTestEnvironment.BootstrapAdminEmail.ToUpperInvariant())
            .Select(x => x.Id)
            .SingleAsync();
    }

    private static async Task<string> AddUser(IdentitySqlServerWebApplicationFactory f, string actor, string email, string role, string password)
    {
        await using var s = f.Services.CreateAsyncScope();
        await s.ServiceProvider.GetRequiredService<IUserManagementService>()
            .CreateAsync(new(actor, "Endpoint Test", email, password, role));
        return await s.ServiceProvider.GetRequiredService<Web.Data.ApplicationDbContext>().Users
            .Where(x => x.Email == email)
            .Select(x => x.Id)
            .SingleAsync();
    }

    private static async Task Login(HttpClient c, string email, string password)
    {
        var page = await c.GetStringAsync("/Account/Login");
        using var form = new FormUrlEncodedContent([
            new("Email", email),
            new("Password", password),
            new("RememberMe", "false"),
            new("__RequestVerificationToken", Token(page))
        ]);
        Assert.Equal(HttpStatusCode.Redirect, (await c.PostAsync("/Account/Login", form)).StatusCode);
    }

    private static string Token(string html) =>
        Regex.Match(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"").Groups["token"].Value;
}
