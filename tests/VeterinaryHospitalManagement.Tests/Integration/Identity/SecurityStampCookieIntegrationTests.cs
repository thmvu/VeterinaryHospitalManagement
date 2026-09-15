using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VeterinaryHospitalManagement.Tests.Infrastructure.Identity;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Entities;
using VeterinaryHospitalManagement.Web.Services.Identity;

namespace VeterinaryHospitalManagement.Tests.Integration.Identity;

[Collection(IdentitySqlServerTestCollection.Name)]
public sealed class SecurityStampCookieIntegrationTests
{
    [IdentitySqlServerFact]
    public async Task Password_reset_rejects_the_preexisting_real_cookie_on_its_next_authorized_request()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        using var client = CreateClient(factory);
        var adminId = await StartAndFindBootstrapAdminAsync(factory, client);
        var userId = await CreateUserAsync(factory, adminId, "reset.cookie@example.test", "Reset.Cookie1!", SystemRoleNames.Receptionist);

        await LoginAsync(client, "reset.cookie@example.test", "Reset.Cookie1!");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Account/ChangePassword")).StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
            var user = Assert.IsType<ManagedUserSummary>(await service.FindAsync(userId));
            await service.ResetPasswordAsync(new ResetPasswordRequest(
                adminId, user.Id, user.ConcurrencyStamp, "Reset.Cookie2!"));
        }

        await AssertCookieRejectedAsync(client);
    }

    [IdentitySqlServerFact]
    public async Task Role_change_rejects_the_preexisting_real_cookie_on_its_next_authorized_request()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        using var client = CreateClient(factory);
        var adminId = await StartAndFindBootstrapAdminAsync(factory, client);
        var userId = await CreateUserAsync(factory, adminId, "role.cookie@example.test", "Role.Cookie1!", SystemRoleNames.Receptionist);

        await LoginAsync(client, "role.cookie@example.test", "Role.Cookie1!");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Account/ChangePassword")).StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IUserManagementService>();
            var user = Assert.IsType<ManagedUserSummary>(await service.FindAsync(userId));
            await service.ChangeRoleAsync(new ChangeUserRoleRequest(
                adminId, user.Id, user.ConcurrencyStamp, SystemRoleNames.Veterinarian));
        }

        await AssertCookieRejectedAsync(client);
    }

    private static HttpClient CreateClient(IdentitySqlServerWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

    private static async Task<string> StartAndFindBootstrapAdminAsync(
        IdentitySqlServerWebApplicationFactory factory,
        HttpClient client)
    {
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await dbContext.Users
            .Where(user => user.NormalizedEmail == IdentitySqlServerTestEnvironment.BootstrapAdminEmail.ToUpperInvariant())
            .Select(user => user.Id)
            .SingleAsync();
    }

    private static async Task<string> CreateUserAsync(
        IdentitySqlServerWebApplicationFactory factory,
        string adminId,
        string email,
        string password,
        string roleName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IUserManagementService>().CreateAsync(
            new CreateManagedUserRequest(adminId, "Cookie Test User", email, password, roleName));
    }

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        var loginPage = await client.GetStringAsync("/Account/Login");
        using var login = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("Email", email),
            new KeyValuePair<string, string>("Password", password),
            new KeyValuePair<string, string>("RememberMe", "false"),
            new KeyValuePair<string, string>("__RequestVerificationToken", ExtractAntiForgeryToken(loginPage))
        ]);
        using var response = await client.PostAsync("/Account/Login", login);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static async Task AssertCookieRejectedAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/Account/ChangePassword");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
        Assert.Equal("?ReturnUrl=%2FAccount%2FChangePassword", response.Headers.Location?.Query);
    }

    private static string ExtractAntiForgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"",
            RegexOptions.CultureInvariant);
        return Assert.IsType<string>(match.Groups["token"].Value);
    }
}
