using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VeterinaryHospitalManagement.Tests.Infrastructure.Identity;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Services.Catalogs;
using VeterinaryHospitalManagement.Web.Services.Identity;

namespace VeterinaryHospitalManagement.Tests.Integration.Catalogs;

[Collection(IdentitySqlServerTestCollection.Name)]
public sealed class ServiceCatalogEndpointTests
{
    private const string Index = "/BackOffice/ServiceCatalogs";

    [IdentitySqlServerFact]
    public async Task Anonymous_is_challenged_and_roles_without_catalog_permission_are_forbidden()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        using var anonymous = Client(factory);
        var response = await anonymous.GetAsync(Index);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);

        var admin = await StartAsync(factory);
        await AddUserAsync(factory, admin, "receptionist-catalog@vet.test", SystemRoleNames.Receptionist);
        using var receptionist = Client(factory);
        await LoginAsync(receptionist, "receptionist-catalog@vet.test", "Integration.Vet123!");
        Assert.Equal(HttpStatusCode.Forbidden, (await receptionist.GetAsync(Index)).StatusCode);
    }

    [IdentitySqlServerFact]
    public async Task Manager_can_create_service_through_real_form()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var admin = await StartAsync(factory);
        await AddUserAsync(factory, admin, "manager-catalog@vet.test", SystemRoleNames.Manager);
        using var client = Client(factory);
        await LoginAsync(client, "manager-catalog@vet.test", "Integration.Vet123!");
        var page = await client.GetStringAsync(Index + "/Create");
        using var form = new FormUrlEncodedContent([
            new("Code", " dv-ui "), new("Name", " Khám UI "), new("Category", " Khám "),
            new("Price", "150000.50"), new("Description", " Tạo từ giao diện "),
            new("__RequestVerificationToken", Token(page))]);

        var response = await client.PostAsync(Index + "/Create", form);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(Index, response.Headers.Location?.OriginalString);
        await using var scope = factory.Services.CreateAsyncScope();
        var created = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .ServiceCatalogs.SingleAsync();
        Assert.Equal("DV-UI", created.Code);
        Assert.Equal(150000.50m, created.Price);
    }

    [IdentitySqlServerFact]
    public async Task Invalid_create_and_stale_edit_show_actionable_messages()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var actor = await StartAsync(factory);
        using var client = Client(factory);
        await LoginAsync(client, IdentitySqlServerTestEnvironment.BootstrapAdminEmail, IdentitySqlServerTestEnvironment.BootstrapAdminPassword);
        var createPage = await client.GetStringAsync(Index + "/Create");
        using var invalidForm = new FormUrlEncodedContent([
            new("Code", ""), new("Name", ""), new("Category", ""), new("Price", "-1"),
            new("__RequestVerificationToken", Token(createPage))]);
        var invalidResponse = await client.PostAsync(Index + "/Create", invalidForm);
        var invalidHtml = WebUtility.HtmlDecode(await invalidResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, invalidResponse.StatusCode);
        Assert.Contains("Mã dịch vụ", invalidHtml);

        int id;
        string staleVersion;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IServiceCatalogService>();
            id = await service.CreateAsync(new(actor, "DV-STALE", "Tên cũ", "Khám", 10m, null));
            var original = (await service.FindAsync(id))!;
            staleVersion = Convert.ToBase64String(original.RowVersion);
            await service.UpdateAsync(new(actor, id, original.RowVersion, "Tên mới", "Khám", 20m, null));
        }

        var editPage = await client.GetStringAsync($"{Index}/Edit/{id}");
        using var staleForm = new FormUrlEncodedContent([
            new("Id", id.ToString()), new("RowVersion", staleVersion), new("Name", "Ghi đè"),
            new("Category", "Khám"), new("Price", "30"),
            new("__RequestVerificationToken", Token(editPage))]);
        var staleResponse = await client.PostAsync(Index + "/Edit", staleForm);
        var staleHtml = WebUtility.HtmlDecode(await staleResponse.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, staleResponse.StatusCode);
        Assert.Contains("đã được thay đổi", staleHtml);
        Assert.Contains("DV-STALE", staleHtml);
    }

    private static HttpClient Client(IdentitySqlServerWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

    private static async Task<string> StartAsync(IdentitySqlServerWebApplicationFactory factory)
    {
        using var client = Client(factory);
        await client.GetAsync("/");
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users
            .Where(x => x.NormalizedEmail == IdentitySqlServerTestEnvironment.BootstrapAdminEmail.ToUpperInvariant())
            .Select(x => x.Id).SingleAsync();
    }

    private static async Task AddUserAsync(
        IdentitySqlServerWebApplicationFactory factory, string actor, string email, string role)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IUserManagementService>()
            .CreateAsync(new(actor, "Catalog Endpoint", email, "Integration.Vet123!", role));
    }

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        var page = await client.GetStringAsync("/Account/Login");
        using var form = new FormUrlEncodedContent([
            new("Email", email), new("Password", password),
            new("RememberMe", "false"), new("__RequestVerificationToken", Token(page))]);
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Account/Login", form)).StatusCode);
    }

    private static string Token(string html) =>
        Regex.Match(html, "<input[^>]*name=\"__RequestVerificationToken\"[^>]*value=\"(?<token>[^\"]+)\"")
            .Groups["token"].Value;
}
