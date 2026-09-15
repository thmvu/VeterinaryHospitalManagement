using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
public sealed class IdentityWorkflowIntegrationTests
{
    [IdentitySqlServerFact]
    public async Task Exact_test_database_migrates_up_down_and_up()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();

        await IdentitySqlServerTestEnvironment.MigrateDownAndUpAsync();
    }

    [IdentitySqlServerFact]
    public async Task Cross_process_database_coordinator_holds_an_exclusive_sql_session_lock()
    {
        var masterConnectionString = new SqlConnectionStringBuilder(
            IdentitySqlServerTestEnvironment.ConnectionString)
        {
            InitialCatalog = "master"
        }.ConnectionString;
        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sp_getapplock
                @Resource = N'VeterinaryHospitalManagement.TestDatabaseFixture',
                @LockMode = N'Exclusive',
                @LockOwner = N'Session',
                @LockTimeout = 0;
            SELECT @result;
            """;

        Assert.True(Convert.ToInt32(await command.ExecuteScalarAsync()) < 0);
    }

    [IdentitySqlServerFact]
    public async Task Seed_completes_a_partial_catalog_and_a_second_run_preserves_changed_grants()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();

        await using var partialCatalogContext = IdentitySqlServerTestEnvironment.CreateContext();
        partialCatalogContext.Permissions.Add(new Permission
        {
            Code = PermissionCodes.OwnerView,
            Name = "Permission inserted before baseline seed"
        });
        await partialCatalogContext.SaveChangesAsync();

        using var factory = new IdentitySqlServerWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);

        await using var firstScope = factory.Services.CreateAsyncScope();
        var firstDb = firstScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(PermissionCatalog.All.Count, await firstDb.Permissions.CountAsync());
        Assert.Equal(SystemRoleNames.All.Count, await firstDb.Roles.CountAsync());
        Assert.True(await firstDb.RolePermissions.AnyAsync(rolePermission =>
            rolePermission.Permission.Code == PermissionCodes.OwnerManage &&
            rolePermission.Role!.Name == SystemRoleNames.Receptionist));

        var receptionistRoleId = await firstDb.Roles
            .Where(role => role.Name == SystemRoleNames.Receptionist)
            .Select(role => role.Id)
            .SingleAsync();
        var ownerManagePermissionId = await firstDb.Permissions
            .Where(permission => permission.Code == PermissionCodes.OwnerManage)
            .Select(permission => permission.Id)
            .SingleAsync();
        var changedGrant = await firstDb.RolePermissions.SingleAsync(grant =>
            grant.RoleId == receptionistRoleId && grant.PermissionId == ownerManagePermissionId);
        firstDb.RolePermissions.Remove(changedGrant);
        await firstDb.SaveChangesAsync();

        await using var secondScope = factory.Services.CreateAsyncScope();
        await secondScope.ServiceProvider.GetRequiredService<SeedRunner>().RunAsync();
        var secondDb = secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Equal(PermissionCatalog.All.Count, await secondDb.Permissions.CountAsync());
        Assert.False(await secondDb.RolePermissions.AnyAsync(grant =>
            grant.RoleId == receptionistRoleId && grant.PermissionId == ownerManagePermissionId));
        Assert.Equal(
            1,
            await secondDb.Users.CountAsync(user => user.NormalizedEmail == IdentitySqlServerTestEnvironment.BootstrapAdminEmail.ToUpperInvariant()));
    }

    [IdentitySqlServerFact]
    public async Task Login_cookie_is_rejected_on_the_next_request_after_the_user_is_deactivated()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        var loginPage = await client.GetStringAsync("/Account/Login");
        var antiForgeryToken = ExtractAntiForgeryToken(loginPage);
        using var login = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("Email", IdentitySqlServerTestEnvironment.BootstrapAdminEmail),
            new KeyValuePair<string, string>("Password", IdentitySqlServerTestEnvironment.BootstrapAdminPassword),
            new KeyValuePair<string, string>("RememberMe", "false"),
            new KeyValuePair<string, string>("__RequestVerificationToken", antiForgeryToken)
        ]);

        using var loginResponse = await client.PostAsync("/Account/Login", login);
        Assert.Equal(HttpStatusCode.Redirect, loginResponse.StatusCode);
        Assert.Equal("/", loginResponse.Headers.Location?.OriginalString);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Account/ChangePassword")).StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(IdentitySqlServerTestEnvironment.BootstrapAdminEmail);
            Assert.NotNull(user);
            user.IsActive = false;
            Assert.True((await userManager.UpdateAsync(user)).Succeeded);
        }

        using var rejectedResponse = await client.GetAsync("/Account/ChangePassword");
        Assert.Equal(HttpStatusCode.Redirect, rejectedResponse.StatusCode);
        Assert.Equal("/Account/Login", rejectedResponse.Headers.Location?.AbsolutePath);
        Assert.Equal("?ReturnUrl=%2FAccount%2FChangePassword", rejectedResponse.Headers.Location?.Query);
    }

    [IdentitySqlServerFact]
    public async Task Dynamic_permission_policy_allows_the_current_grant_and_denies_a_missing_grant()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);

        string receptionistId;
        await using (var setupScope = factory.Services.CreateAsyncScope())
        {
            var users = setupScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var receptionist = new ApplicationUser
            {
                UserName = "integration.receptionist@example.test",
                Email = "integration.receptionist@example.test",
                EmailConfirmed = true,
                FullName = "Integration Receptionist",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            Assert.True((await users.CreateAsync(receptionist, "Integration.Receptionist1!")).Succeeded);
            Assert.True((await users.AddToRoleAsync(receptionist, SystemRoleNames.Receptionist)).Succeeded);
            receptionistId = receptionist.Id;
        }

        await using var authorizationScope = factory.Services.CreateAsyncScope();
        var authorization = authorizationScope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, receptionistId)],
            IdentityConstants.ApplicationScheme));

        Assert.True((await authorization.AuthorizeAsync(
            principal,
            resource: null,
            PermissionPolicyName.For(PermissionCodes.OwnerView))).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(
            principal,
            resource: null,
            PermissionPolicyName.For(PermissionCodes.ReportExport))).Succeeded);
    }

    [IdentitySqlServerFact]
    public async Task Concurrent_deactivation_of_two_admins_keeps_one_active_admin()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);

        var firstAdmin = await FindBootstrapAdminAsync(factory);
        await using (var createScope = factory.Services.CreateAsyncScope())
        {
            var service = CreateUserManagementService(createScope.ServiceProvider);
            await service.CreateAsync(new CreateManagedUserRequest(
                firstAdmin.Id,
                "Second Integration Admin",
                "integration.admin.second@example.test",
                "Integration.Admin2!",
                SystemRoleNames.Admin));
        }

        var admins = await FindActiveAdminsAsync(factory);
        Assert.Equal(2, admins.Count);

        var results = await Task.WhenAll(admins.Select(admin => DeactivateAdminAsync(factory, admin)));

        Assert.Equal(1, results.Count(result => result is null));
        Assert.Equal(1, results.Count(result => result is UserManagementException));

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var activeAdminCount = await (
            from user in verifyDb.Users
            join userRole in verifyDb.UserRoles on user.Id equals userRole.UserId
            join role in verifyDb.Roles on userRole.RoleId equals role.Id
            where user.IsActive && role.Name == SystemRoleNames.Admin
            select user.Id).CountAsync();
        Assert.Equal(1, activeAdminCount);
    }

    [IdentitySqlServerFact]
    public async Task Failed_role_change_keeps_the_existing_single_role()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);

        var admin = await FindBootstrapAdminAsync(factory);
        string receptionistId;
        await using (var createScope = factory.Services.CreateAsyncScope())
        {
            var service = CreateUserManagementService(createScope.ServiceProvider);
            receptionistId = await service.CreateAsync(new CreateManagedUserRequest(
                admin.Id,
                "Rollback Receptionist",
                "integration.rollback@example.test",
                "Integration.Rollback1!",
                SystemRoleNames.Receptionist));
        }

        var receptionist = await FindUserAsync(factory, receptionistId);
        await using (var changeScope = factory.Services.CreateAsyncScope())
        {
            var service = CreateUserManagementService(changeScope.ServiceProvider);
            await Assert.ThrowsAsync<UserManagementException>(() => service.ChangeRoleAsync(
                new ChangeUserRoleRequest(
                    admin.Id,
                    receptionist.Id,
                    receptionist.ConcurrencyStamp,
                    "NotASystemRole")));
        }

        var afterFailure = await FindUserAsync(factory, receptionistId);
        Assert.Equal(SystemRoleNames.Receptionist, afterFailure.RoleName);
    }

    [IdentitySqlServerFact]
    public async Task Concurrent_role_changes_for_the_same_user_leave_exactly_one_role_and_one_audit()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);

        var admin = await FindBootstrapAdminAsync(factory);
        var targetId = await CreateManagedUserAsync(
            factory,
            admin.Id,
            "Concurrent Role User",
            "integration.concurrent-role@example.test",
            SystemRoleNames.Receptionist);
        var target = await FindUserAsync(factory, targetId);

        var outcomes = await Task.WhenAll(
            ChangeRoleAsync(factory, admin.Id, target, SystemRoleNames.Manager),
            ChangeRoleAsync(factory, admin.Id, target, SystemRoleNames.Veterinarian));

        Assert.Equal(1, outcomes.Count(outcome => outcome is null));
        var failure = Assert.Single(outcomes, outcome => outcome is not null);
        Assert.True(
            failure is UserManagementException,
            $"The concurrent role-change loser must return a domain concurrency error, not {failure!.GetType().Name}: {failure.Message}");

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roles = await (from userRole in verifyDb.UserRoles
                           join role in verifyDb.Roles on userRole.RoleId equals role.Id
                           where userRole.UserId == targetId
                           select role.Name).ToListAsync();
        Assert.Single(roles);
        Assert.True(
            roles[0] is SystemRoleNames.Manager or SystemRoleNames.Veterinarian,
            $"Unexpected final role: {roles[0]}");
        Assert.Equal(
            1,
            await verifyDb.AuditLogs.CountAsync(audit =>
                audit.Action == "Identity.UserRoleChanged" && audit.EntityId == targetId));
    }

    [IdentitySqlServerFact]
    public async Task Role_change_audit_failure_rolls_back_the_old_role_removal_and_security_stamp_update()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);

        var admin = await FindBootstrapAdminAsync(factory);
        var targetId = await CreateManagedUserAsync(
            factory,
            admin.Id,
            "Role Rollback User",
            "integration.role-rollback@example.test",
            SystemRoleNames.Receptionist);
        var before = await FindUserAsync(factory, targetId);

        await using (var mutationScope = factory.Services.CreateAsyncScope())
        {
            var service = CreateUserManagementService(mutationScope.ServiceProvider);
            await Assert.ThrowsAsync<DbUpdateException>(() => service.ChangeRoleAsync(
                new ChangeUserRoleRequest(
                    "missing-actor-user-id",
                    before.Id,
                    before.ConcurrencyStamp,
                    SystemRoleNames.Manager)));
        }

        var after = await FindUserAsync(factory, targetId);
        Assert.Equal(SystemRoleNames.Receptionist, after.RoleName);
        Assert.Equal(before.ConcurrencyStamp, after.ConcurrencyStamp);

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await verifyDb.UserRoles.CountAsync(role => role.UserId == targetId));
        Assert.Equal(
            0,
            await verifyDb.AuditLogs.CountAsync(audit =>
                audit.Action == "Identity.UserRoleChanged" && audit.EntityId == targetId));
    }

    [IdentitySqlServerFact]
    public async Task Create_user_audit_failure_rolls_back_the_user_role_and_audit()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);

        const string email = "integration.create-rollback@example.test";
        await using (var mutationScope = factory.Services.CreateAsyncScope())
        {
            var service = CreateUserManagementService(mutationScope.ServiceProvider);
            await Assert.ThrowsAsync<DbUpdateException>(() => service.CreateAsync(
                new CreateManagedUserRequest(
                    "missing-actor-user-id",
                    "Create Rollback User",
                    email,
                    "Integration.CreateRollback1!",
                    SystemRoleNames.Receptionist)));
        }

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0, await verifyDb.Users.CountAsync(user => user.NormalizedEmail == email.ToUpperInvariant()));
        Assert.Equal(
            0,
            await verifyDb.AuditLogs.CountAsync(audit =>
                audit.Action == "Identity.UserCreated" && audit.Description.Contains(email)));
    }

    private static IUserManagementService CreateUserManagementService(IServiceProvider services) =>
        services.GetRequiredService<IUserManagementService>();

    private static async Task<string> CreateManagedUserAsync(
        IdentitySqlServerWebApplicationFactory factory,
        string actorUserId,
        string fullName,
        string email,
        string roleName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await CreateUserManagementService(scope.ServiceProvider).CreateAsync(
            new CreateManagedUserRequest(
                actorUserId,
                fullName,
                email,
                "Integration.ManagedUser1!",
                roleName));
    }

    private static async Task<Exception?> ChangeRoleAsync(
        IdentitySqlServerWebApplicationFactory factory,
        string actorUserId,
        ManagedUserSummary target,
        string roleName)
    {
        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            await CreateUserManagementService(scope.ServiceProvider).ChangeRoleAsync(
                new ChangeUserRoleRequest(
                    actorUserId,
                    target.Id,
                    target.ConcurrencyStamp,
                    roleName));
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static async Task<ManagedUserSummary> FindBootstrapAdminAsync(IdentitySqlServerWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var service = CreateUserManagementService(scope.ServiceProvider);
        return Assert.Single(
            await service.ListAsync(),
            user => user.Email == IdentitySqlServerTestEnvironment.BootstrapAdminEmail);
    }

    private static async Task<List<ManagedUserSummary>> FindActiveAdminsAsync(IdentitySqlServerWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var service = CreateUserManagementService(scope.ServiceProvider);
        return (await service.ListAsync())
            .Where(user => user.IsActive && user.RoleName == SystemRoleNames.Admin)
            .ToList();
    }

    private static async Task<ManagedUserSummary> FindUserAsync(
        IdentitySqlServerWebApplicationFactory factory,
        string userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var service = CreateUserManagementService(scope.ServiceProvider);
        return Assert.IsType<ManagedUserSummary>(await service.FindAsync(userId));
    }

    private static async Task<Exception?> DeactivateAdminAsync(
        IdentitySqlServerWebApplicationFactory factory,
        ManagedUserSummary admin)
    {
        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var service = CreateUserManagementService(scope.ServiceProvider);
            await service.SetActiveAsync(new UserActivationRequest(
                admin.Id,
                admin.Id,
                admin.ConcurrencyStamp,
                IsActive: false));
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
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
