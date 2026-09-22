using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VeterinaryHospitalManagement.Tests.Infrastructure.Identity;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Services.Identity;

namespace VeterinaryHospitalManagement.Tests.Integration.OwnersPets;

[Collection(IdentitySqlServerTestCollection.Name)]
public sealed class OwnersEndpointAuthorizationTests
{
    private const string OwnersIndexPath = "/BackOffice/Owners";
    private const string OwnersCreatePath = "/BackOffice/Owners/Create";

    [IdentitySqlServerFact]
    public async Task Anonymous_request_to_owners_index_is_challenged_to_login()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(OwnersIndexPath);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    [IdentitySqlServerFact]
    public async Task Veterinarian_with_owner_view_cannot_access_create_or_post()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var adminId = await StartAndFindBootstrapAdminAsync(factory);
        await CreateUserAsync(factory, adminId, "owners.vet@example.test", SystemRoleNames.Veterinarian, "Integration.OwnersVet1!");
        using var client = CreateClient(factory);
        await LoginAsync(client, "owners.vet@example.test", "Integration.OwnersVet1!");

        using var indexResponse = await client.GetAsync(OwnersIndexPath);
        Assert.Equal(HttpStatusCode.OK, indexResponse.StatusCode);
        var indexHtml = await indexResponse.Content.ReadAsStringAsync();
        Assert.Contains("data-ui=\"botanical-owner-registry\"", indexHtml);
        Assert.DoesNotContain("href=\"/BackOffice/Owners/Create\"", indexHtml);

        // GET /BackOffice/Owners/Create requires OwnerManage; Veterinarian only has OwnerView -> 403
        using var getCreateResponse = await client.GetAsync(OwnersCreatePath);
        Assert.Equal(HttpStatusCode.Forbidden, getCreateResponse.StatusCode);

        // POST /BackOffice/Owners/Create requires OwnerManage -> 403
        using var form = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("FullName", "Khách Hàng Test"),
            new KeyValuePair<string, string>("PhoneNumber", "0912345678")
        ]);
        using var response = await client.PostAsync(OwnersCreatePath, form);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [IdentitySqlServerFact]
    public async Task Receptionist_with_owner_manage_creates_owner_via_post()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var adminId = await StartAndFindBootstrapAdminAsync(factory);
        await CreateUserAsync(factory, adminId, "owners.receptionist@example.test", SystemRoleNames.Receptionist, "Integration.OwnersRec1!");
        using var client = CreateClient(factory);
        await LoginAsync(client, "owners.receptionist@example.test", "Integration.OwnersRec1!");

        var createPage = await client.GetStringAsync(OwnersCreatePath);
        using var form = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("FullName", "Lê Văn Tạo"),
            new KeyValuePair<string, string>("PhoneNumber", "0912 345 678"),
            new KeyValuePair<string, string>("__RequestVerificationToken", ExtractAntiForgeryToken(createPage))
        ]);
        using var response = await client.PostAsync(OwnersCreatePath, form);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<Web.Data.ApplicationDbContext>();
        var owner = Assert.Single(dbContext.Owners.ToList());
        Assert.Equal("OWN-000001", owner.OwnerCode);
        Assert.Equal("+84912345678", owner.PhoneNumber);
    }

    private static HttpClient CreateClient(IdentitySqlServerWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

    private static async Task<string> StartAndFindBootstrapAdminAsync(IdentitySqlServerWebApplicationFactory factory)
    {
        using var client = CreateClient(factory);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Web.Data.ApplicationDbContext>();
        return await db.Users
            .Where(user => user.NormalizedEmail == IdentitySqlServerTestEnvironment.BootstrapAdminEmail.ToUpperInvariant())
            .Select(user => user.Id)
            .SingleAsync();
    }

    private static async Task CreateUserAsync(
        IdentitySqlServerWebApplicationFactory factory,
        string adminId,
        string email,
        string roleName,
        string password)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IUserManagementService>().CreateAsync(
            new CreateManagedUserRequest(adminId, "Owners Endpoint Test", email, password, roleName));
    }

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        var loginPage = await client.GetStringAsync("/Account/Login");
        using var form = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("Email", email),
            new KeyValuePair<string, string>("Password", password),
            new KeyValuePair<string, string>("RememberMe", "false"),
            new KeyValuePair<string, string>("__RequestVerificationToken", ExtractAntiForgeryToken(loginPage))
        ]);
        using var response = await client.PostAsync("/Account/Login", form);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
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
