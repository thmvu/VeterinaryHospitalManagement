using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VeterinaryHospitalManagement.Tests.Infrastructure.Identity;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Services.Identity;

namespace VeterinaryHospitalManagement.Tests.Integration.Visits;

[Collection(IdentitySqlServerTestCollection.Name)]
public sealed class VisitEndpointAuthorizationTests
{
    private const string IndexUrl = "/BackOffice/Visits";
    private const string WalkInUrl = "/BackOffice/Visits/WalkIn";

    [IdentitySqlServerFact]
    public async Task Anonymous_is_challenged_and_redirected_to_login()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var f = new IdentitySqlServerWebApplicationFactory();
        using var c = Client(f);
        using var r = await c.GetAsync(IndexUrl);

        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal("/Account/Login", r.Headers.Location?.AbsolutePath);
    }

    [IdentitySqlServerFact]
    public async Task Role_permissions_matrix_enforced_for_queue_and_walk_in()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var f = new IdentitySqlServerWebApplicationFactory();
        var admin = await Start(f);

        await AddUser(f, admin, "receptionist@visit.test", SystemRoleNames.Receptionist, "Integration.Vet123!");
        await AddUser(f, admin, "veterinarian@visit.test", SystemRoleNames.Veterinarian, "Integration.Vet123!");

        // Receptionist can access queue (Index) and WalkIn
        using (var c = Client(f))
        {
            await Login(c, "receptionist@visit.test", "Integration.Vet123!");
            Assert.Equal(HttpStatusCode.OK, (await c.GetAsync(IndexUrl)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await c.GetAsync(WalkInUrl)).StatusCode);
        }

        // Veterinarian can access queue (Index) but WalkIn is Forbidden (403)
        using (var c = Client(f))
        {
            await Login(c, "veterinarian@visit.test", "Integration.Vet123!");
            Assert.Equal(HttpStatusCode.OK, (await c.GetAsync(IndexUrl)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync(WalkInUrl)).StatusCode);
        }

        // Admin can access both
        using (var ac = Client(f))
        {
            await Login(ac, IdentitySqlServerTestEnvironment.BootstrapAdminEmail, IdentitySqlServerTestEnvironment.BootstrapAdminPassword);
            Assert.Equal(HttpStatusCode.OK, (await ac.GetAsync(IndexUrl)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await ac.GetAsync(WalkInUrl)).StatusCode);
        }
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
