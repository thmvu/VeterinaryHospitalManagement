using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Areas.BackOffice.ViewModels.Audit;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Services.Time;

namespace VeterinaryHospitalManagement.Web.Services.Audit;

public sealed class AuditQueryService(
    ApplicationDbContext dbContext,
    IVietnamTimeProvider timeProvider) : IAuditQueryService
{
    public async Task<AuditLogIndexViewModel> QueryAuditLogsAsync(
        AuditLogIndexViewModel filter,
        CancellationToken cancellationToken = default)
    {
        var localOffset = timeProvider.LocalNow.Offset;

        var query = dbContext.AuditLogs
            .AsNoTracking()
            .Include(a => a.User)
            .AsQueryable();

        if (filter.DateFrom.HasValue)
        {
            var fromUtc = new DateTimeOffset(filter.DateFrom.Value.Date, localOffset).ToUniversalTime();
            query = query.Where(a => a.CreatedAt >= fromUtc);
        }

        if (filter.DateTo.HasValue)
        {
            var toUtc = new DateTimeOffset(filter.DateTo.Value.Date.AddDays(1), localOffset).ToUniversalTime();
            query = query.Where(a => a.CreatedAt < toUtc);
        }

        if (!string.IsNullOrWhiteSpace(filter.UserId))
        {
            query = query.Where(a => a.UserId == filter.UserId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            query = query.Where(a => a.Action == filter.Action);
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityName))
        {
            query = query.Where(a => a.EntityName == filter.EntityName);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(a => a.Description.Contains(search) || a.EntityId.Contains(search));
        }

        var totalItems = await query.CountAsync(cancellationToken);

        var pageSize = filter.PageSize < 1 ? 25 : Math.Min(filter.PageSize, 100);
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalItems / pageSize));
        var page = Math.Clamp(filter.Page, 1, totalPages);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogItemViewModel(
                a.Id,
                a.ActorType,
                a.UserId,
                a.User != null ? (!string.IsNullOrEmpty(a.User.FullName) ? a.User.FullName : a.User.UserName ?? a.UserId ?? "N/A") : (a.ActorType == "System" ? "Hệ thống" : "Không xác định"),
                a.Action,
                a.EntityName,
                a.EntityId,
                a.Description,
                a.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        var availableActions = await dbContext.AuditLogs
            .Select(a => a.Action)
            .Distinct()
            .OrderBy(a => a)
            .Take(50)
            .ToListAsync(cancellationToken);

        var availableEntities = await dbContext.AuditLogs
            .Select(a => a.EntityName)
            .Distinct()
            .OrderBy(e => e)
            .Take(50)
            .ToListAsync(cancellationToken);

        var availableUsers = await dbContext.Users
            .AsNoTracking()
            .OrderBy(u => u.FullName)
            .Select(u => new AuditUserOptionViewModel(u.Id, !string.IsNullOrEmpty(u.FullName) ? $"{u.FullName} ({u.Email})" : u.Email ?? u.UserName ?? u.Id))
            .ToListAsync(cancellationToken);

        return new AuditLogIndexViewModel
        {
            DateFrom = filter.DateFrom,
            DateTo = filter.DateTo,
            UserId = filter.UserId,
            Action = filter.Action,
            EntityName = filter.EntityName,
            Search = filter.Search,
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            Items = items,
            AvailableActions = availableActions,
            AvailableEntities = availableEntities,
            AvailableUsers = availableUsers
        };
    }
}
