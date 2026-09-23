using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Entities;

namespace VeterinaryHospitalManagement.Web.Services.Catalogs;

public sealed class ServiceCatalogService(ApplicationDbContext db, TimeProvider clock) : IServiceCatalogService
{
    public async Task<IReadOnlyList<ServiceCatalogListItem>> ListAsync(CancellationToken cancellationToken = default) =>
        await db.ServiceCatalogs.AsNoTracking().OrderBy(x => x.Code)
            .Select(x => new ServiceCatalogListItem(x.Id, x.Code, x.Name, x.Category, x.Price, x.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<ServiceCatalogDetails?> FindAsync(int id, CancellationToken cancellationToken = default) =>
        await db.ServiceCatalogs.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new ServiceCatalogDetails(
                x.Id, x.Code, x.Name, x.Category, x.Price, x.Description, x.IsActive, x.RowVersion))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<int> CreateAsync(CreateServiceCatalogRequest request, CancellationToken cancellationToken = default) =>
        InSerializableTransactionAsync(async () =>
        {
            var code = ServiceCatalogRules.NormalizeCode(request.Code);
            if (await db.ServiceCatalogs.AnyAsync(x => x.Code == code, cancellationToken))
            {
                throw new ServiceCatalogManagementException("Mã dịch vụ đã tồn tại.");
            }

            var item = new ServiceCatalog
            {
                Code = code,
                Name = ServiceCatalogRules.NormalizeName(request.Name),
                Category = ServiceCatalogRules.NormalizeCategory(request.Category),
                Price = ServiceCatalogRules.NormalizePrice(request.Price),
                Description = ServiceCatalogRules.NormalizeDescription(request.Description),
                IsActive = true
            };
            db.ServiceCatalogs.Add(item);
            await db.SaveChangesAsync(cancellationToken);
            AddAudit(request.ActorUserId, "ServiceCatalog.Created", item.Id, $"Created service catalog {item.Code}.");
            await db.SaveChangesAsync(cancellationToken);
            return item.Id;
        }, cancellationToken);

    public Task UpdateAsync(UpdateServiceCatalogRequest request, CancellationToken cancellationToken = default) =>
        GuardConcurrentMutationAsync(() => InSerializableTransactionAsync(async () =>
        {
            var item = await LoadAsync(request.Id, request.ExpectedRowVersion, cancellationToken);
            item.Name = ServiceCatalogRules.NormalizeName(request.Name);
            item.Category = ServiceCatalogRules.NormalizeCategory(request.Category);
            item.Price = ServiceCatalogRules.NormalizePrice(request.Price);
            item.Description = ServiceCatalogRules.NormalizeDescription(request.Description);
            AddAudit(request.ActorUserId, "ServiceCatalog.Updated", item.Id, $"Updated service catalog {item.Code}.");
            await db.SaveChangesAsync(cancellationToken);
            return 0;
        }, cancellationToken));

    public Task SetActiveAsync(ServiceCatalogActivationRequest request, CancellationToken cancellationToken = default) =>
        GuardConcurrentMutationAsync(() => InSerializableTransactionAsync(async () =>
        {
            var item = await LoadAsync(request.Id, request.ExpectedRowVersion, cancellationToken);
            item.IsActive = request.IsActive;
            AddAudit(
                request.ActorUserId,
                request.IsActive ? "ServiceCatalog.Activated" : "ServiceCatalog.Deactivated",
                item.Id,
                $"Changed service catalog {item.Code} active status to {request.IsActive}.");
            await db.SaveChangesAsync(cancellationToken);
            return 0;
        }, cancellationToken));

    private async Task<ServiceCatalog> LoadAsync(int id, byte[] expectedRowVersion, CancellationToken cancellationToken)
    {
        var item = await db.ServiceCatalogs.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ServiceCatalogManagementException("Không tìm thấy dịch vụ.");
        if (!item.RowVersion.AsSpan().SequenceEqual(expectedRowVersion))
        {
            throw new ServiceCatalogConcurrencyException();
        }

        return item;
    }

    private void AddAudit(string actorUserId, string action, int id, string description) =>
        db.AuditLogs.Add(new AuditLog
        {
            ActorType = "Internal",
            UserId = actorUserId,
            Action = action,
            EntityName = "ServiceCatalog",
            EntityId = id.ToString(CultureInfo.InvariantCulture),
            Description = description,
            CreatedAt = clock.GetUtcNow()
        });

    private async Task<T> InSerializableTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                try
                {
                    var result = await operation();
                    await transaction.CommitAsync(cancellationToken);
                    return result;
                }
                catch (DbUpdateException exception) when (exception is not DbUpdateConcurrencyException)
                {
                    throw new ServiceCatalogManagementException("Không thể lưu dịch vụ; mã có thể đã tồn tại.");
                }
            });
        }
        catch (Exception exception) when (ContainsSqlDeadlock(exception))
        {
            throw new ServiceCatalogManagementException("Không thể lưu dịch vụ; mã có thể đã tồn tại.");
        }
    }

    private static bool ContainsSqlDeadlock(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 1205 })
            {
                return true;
            }
        }

        return false;
    }

    private static async Task GuardConcurrentMutationAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ServiceCatalogConcurrencyException();
        }
    }
}
