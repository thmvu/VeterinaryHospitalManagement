using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VeterinaryHospitalManagement.Tests.Infrastructure.Identity;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Data.Seed;
using VeterinaryHospitalManagement.Web.Models.Entities;
using VeterinaryHospitalManagement.Web.Services.Identity;

namespace VeterinaryHospitalManagement.Tests.Integration.Identity;

[Collection(IdentitySqlServerTestCollection.Name)]
public sealed class UsersEndpointAndSeedIntegrationTests
{
    private const string UsersIndexPath = "/BackOffice/Users";

    [IdentitySqlServerFact]
    public async Task Anonymous_request_to_users_index_is_challenged_to_login()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(UsersIndexPath);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
        Assert.Equal("?ReturnUrl=%2FBackOffice%2FUsers", response.Headers.Location?.Query);
    }

    [IdentitySqlServerFact]
    public async Task Receptionist_cookie_is_forbidden_from_the_actual_users_index()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var adminId = await StartAndFindBootstrapAdminAsync(factory);
        await CreateUserAsync(factory, adminId, "users.receptionist@example.test", SystemRoleNames.Receptionist);
        using var client = CreateClient(factory);
        await LoginAsync(client, "users.receptionist@example.test", "Integration.UsersRole1!");

        using var response = await client.GetAsync(UsersIndexPath);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [IdentitySqlServerFact]
    public async Task Manager_cookie_is_forbidden_from_the_actual_users_index()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var adminId = await StartAndFindBootstrapAdminAsync(factory);
        await CreateUserAsync(factory, adminId, "users.manager@example.test", SystemRoleNames.Manager);
        using var client = CreateClient(factory);
        await LoginAsync(client, "users.manager@example.test", "Integration.UsersRole1!");

        using var response = await client.GetAsync(UsersIndexPath);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [IdentitySqlServerFact]
    public async Task Admin_cookie_with_user_manage_reaches_the_actual_users_index()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        using var client = CreateClient(factory);
        await EnsureFactoryStartedAsync(factory);
        await LoginAsync(
            client,
            IdentitySqlServerTestEnvironment.BootstrapAdminEmail,
            IdentitySqlServerTestEnvironment.BootstrapAdminPassword);

        using var response = await client.GetAsync(UsersIndexPath);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Quản lý tài khoản", html);
    }

    [IdentitySqlServerFact]
    public async Task Concurrent_seed_runners_complete_once_and_preserve_manual_grants()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using (var initialFactory = new IdentitySqlServerWebApplicationFactory())
        {
            await EnsureFactoryStartedAsync(initialFactory);
            await using var scope = initialFactory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var receptionistRoleId = await db.Roles
                .Where(role => role.Name == SystemRoleNames.Receptionist)
                .Select(role => role.Id)
                .SingleAsync();
            var ownerManagePermissionId = await db.Permissions
                .Where(permission => permission.Code == PermissionCodes.OwnerManage)
                .Select(permission => permission.Id)
                .SingleAsync();
            var manualRemoval = await db.RolePermissions.SingleAsync(grant =>
                grant.RoleId == receptionistRoleId && grant.PermissionId == ownerManagePermissionId);
            db.RolePermissions.Remove(manualRemoval);

            var reportViewPermissionId = await db.Permissions
                .Where(permission => permission.Code == PermissionCodes.ReportView)
                .Select(permission => permission.Id)
                .SingleAsync();
            db.RolePermissions.Add(new RolePermission
            {
                RoleId = receptionistRoleId,
                PermissionId = reportViewPermissionId
            });

            var ownerView = await db.Permissions.SingleAsync(permission =>
                permission.Code == PermissionCodes.OwnerView);
            var ownerViewGrants = await db.RolePermissions
                .Where(grant => grant.PermissionId == ownerView.Id)
                .ToListAsync();
            db.RolePermissions.RemoveRange(ownerViewGrants);
            db.Permissions.Remove(ownerView);
            await db.SaveChangesAsync();
        }

        using var firstInstance = new IdentitySqlServerWebApplicationFactory();
        using var secondInstance = new IdentitySqlServerWebApplicationFactory();
        var startupResults = await Task.WhenAll(
            StartIndependentInstanceAsync(firstInstance),
            StartIndependentInstanceAsync(secondInstance));
        Assert.All(startupResults, status => Assert.Equal(HttpStatusCode.OK, status));

        await using var verify = IdentitySqlServerTestEnvironment.CreateContext();
        var roles = await verify.Roles.ToListAsync();
        var permissions = await verify.Permissions.ToListAsync();
        var ownerViewId = Assert.Single(permissions, permission => permission.Code == PermissionCodes.OwnerView).Id;
        var receptionistId = Assert.Single(roles, role => role.Name == SystemRoleNames.Receptionist).Id;
        var ownerManageId = Assert.Single(permissions, permission => permission.Code == PermissionCodes.OwnerManage).Id;
        var reportViewId = Assert.Single(permissions, permission => permission.Code == PermissionCodes.ReportView).Id;

        Assert.Equal(SystemRoleNames.All.Count, roles.Count);
        Assert.Equal(PermissionCatalog.All.Count, permissions.Count);
        Assert.Equal(
            SystemRoleNames.All.Sum(role => PermissionCatalog.DefaultPermissionsFor(role).Count),
            await verify.RolePermissions.CountAsync());
        Assert.Equal(
            SystemRoleNames.All.Count,
            await verify.RolePermissions.CountAsync(grant => grant.PermissionId == ownerViewId));
        Assert.False(await verify.RolePermissions.AnyAsync(grant =>
            grant.RoleId == receptionistId && grant.PermissionId == ownerManageId));
        Assert.True(await verify.RolePermissions.AnyAsync(grant =>
            grant.RoleId == receptionistId && grant.PermissionId == reportViewId));
        Assert.Equal(
            1,
            await verify.AuditLogs.CountAsync(audit =>
                audit.ActorType == "System" &&
                audit.Action == "Identity.PermissionBaselineSeeded" &&
                audit.EntityName == "PermissionCatalog" &&
                audit.EntityId == "v1"));
    }

    private static HttpClient CreateClient(IdentitySqlServerWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

    private static async Task<string> StartAndFindBootstrapAdminAsync(IdentitySqlServerWebApplicationFactory factory)
    {
        await EnsureFactoryStartedAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Users
            .Where(user => user.NormalizedEmail == IdentitySqlServerTestEnvironment.BootstrapAdminEmail.ToUpperInvariant())
            .Select(user => user.Id)
            .SingleAsync();
    }

    private static async Task EnsureFactoryStartedAsync(IdentitySqlServerWebApplicationFactory factory)
    {
        using var client = CreateClient(factory);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);
    }

    private static async Task<HttpStatusCode> StartIndependentInstanceAsync(IdentitySqlServerWebApplicationFactory factory)
    {
        using var client = CreateClient(factory);
        return (await client.GetAsync("/")).StatusCode;
    }

    private static async Task CreateUserAsync(
        IdentitySqlServerWebApplicationFactory factory,
        string adminId,
        string email,
        string roleName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IUserManagementService>().CreateAsync(
            new CreateManagedUserRequest(
                adminId,
                "Users Endpoint Test",
                email,
                "Integration.UsersRole1!",
                roleName));
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
