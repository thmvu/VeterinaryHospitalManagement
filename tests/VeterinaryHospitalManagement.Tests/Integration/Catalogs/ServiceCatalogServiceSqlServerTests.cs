using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VeterinaryHospitalManagement.Tests.Infrastructure.Identity;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Services.Catalogs;

namespace VeterinaryHospitalManagement.Tests.Integration.Catalogs;

[Collection(IdentitySqlServerTestCollection.Name)]
public sealed class ServiceCatalogServiceSqlServerTests
{
    [IdentitySqlServerFact]
    public async Task Create_normalizes_fields_and_writes_audit_atomically()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var actor = await StartAsync(factory);

        var id = await WithAsync(factory, service => service.CreateAsync(
            new(actor, " dv-kham ", " Khám tổng quát ", " Khám ", 125000.50m, " Dịch vụ cơ bản ")));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await db.ServiceCatalogs.SingleAsync();
        Assert.Equal(id, item.Id);
        Assert.Equal("DV-KHAM", item.Code);
        Assert.Equal("Khám tổng quát", item.Name);
        Assert.Equal("Khám", item.Category);
        Assert.Equal(125000.50m, item.Price);
        Assert.Equal("Dịch vụ cơ bản", item.Description);
        Assert.Single(await db.AuditLogs.Where(x => x.Action == "ServiceCatalog.Created").ToListAsync());
    }

    [IdentitySqlServerFact]
    public async Task Duplicate_code_is_rejected_and_audit_failure_rolls_back_creation()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var actor = await StartAsync(factory);
        await WithAsync(factory, service => service.CreateAsync(new(actor, "DV-01", "Khám", "Khám", 1m, null)));

        await Assert.ThrowsAsync<ServiceCatalogManagementException>(() => WithAsync(factory,
            service => service.CreateAsync(new(actor, " dv-01 ", "Trùng", "Khám", 2m, null))));
        await Assert.ThrowsAsync<ServiceCatalogManagementException>(() => WithAsync(factory,
            service => service.CreateAsync(new("missing-actor", "DV-02", "Rollback", "Khám", 3m, null))));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.ServiceCatalogs.CountAsync());
        Assert.Equal(1, await db.AuditLogs.CountAsync(x => x.Action == "ServiceCatalog.Created"));
    }

    [IdentitySqlServerFact]
    public async Task Concurrent_duplicate_code_creates_only_one_service()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var actor = await StartAsync(factory);

        var results = await Task.WhenAll(
            AttemptAsync(() => WithAsync(factory, service => service.CreateAsync(
                new(actor, "DV-RACE", "Dịch vụ A", "Khám", 1m, null)))),
            AttemptAsync(() => WithAsync(factory, service => service.CreateAsync(
                new(actor, " dv-race ", "Dịch vụ B", "Khám", 2m, null)))));

        Assert.Equal(1, results.Count(x => x));
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .ServiceCatalogs.CountAsync(x => x.Code == "DV-RACE"));
    }

    [IdentitySqlServerFact]
    public async Task Update_keeps_code_detects_stale_version_and_activation_is_audited()
    {
        await IdentitySqlServerTestEnvironment.RecreateAndMigrateAsync();
        using var factory = new IdentitySqlServerWebApplicationFactory();
        var actor = await StartAsync(factory);
        var id = await WithAsync(factory, service => service.CreateAsync(
            new(actor, "DV-IMMUTABLE", "Tên cũ", "Nhóm cũ", 10m, null)));
        var original = (await WithAsync(factory, service => service.FindAsync(id)))!;

        await WithAsync(factory, service => service.UpdateAsync(
            new(actor, id, original.RowVersion, "Tên mới", "Nhóm mới", 20.25m, "Mô tả")));
        await Assert.ThrowsAsync<ServiceCatalogConcurrencyException>(() => WithAsync(factory,
            service => service.UpdateAsync(new(actor, id, original.RowVersion, "Sai", "Sai", 1m, null))));
        var updated = (await WithAsync(factory, service => service.FindAsync(id)))!;
        await WithAsync(factory, service => service.SetActiveAsync(new(actor, id, updated.RowVersion, false)));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await db.ServiceCatalogs.SingleAsync();
        Assert.Equal("DV-IMMUTABLE", item.Code);
        Assert.Equal("Tên mới", item.Name);
        Assert.False(item.IsActive);
        Assert.Equal(1, await db.AuditLogs.CountAsync(x => x.Action == "ServiceCatalog.Updated"));
        Assert.Equal(1, await db.AuditLogs.CountAsync(x => x.Action == "ServiceCatalog.Deactivated"));
    }

    private static async Task<string> StartAsync(IdentitySqlServerWebApplicationFactory factory)
    {
        using var client = factory.CreateClient();
        await client.GetAsync("/");
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users
            .Where(x => x.NormalizedEmail == IdentitySqlServerTestEnvironment.BootstrapAdminEmail.ToUpperInvariant())
            .Select(x => x.Id).SingleAsync();
    }

    private static async Task<T> WithAsync<T>(IdentitySqlServerWebApplicationFactory factory, Func<IServiceCatalogService, Task<T>> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IServiceCatalogService>());
    }

    private static async Task WithAsync(IdentitySqlServerWebApplicationFactory factory, Func<IServiceCatalogService, Task> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<IServiceCatalogService>());
    }

    private static async Task<bool> AttemptAsync(Func<Task> action)
    {
        try
        {
            await action();
            return true;
        }
        catch (ServiceCatalogManagementException)
        {
            return false;
        }
    }
}
