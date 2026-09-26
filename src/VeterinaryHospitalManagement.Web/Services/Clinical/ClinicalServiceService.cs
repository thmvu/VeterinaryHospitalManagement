using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Entities;
using VeterinaryHospitalManagement.Web.Models.Enums;

namespace VeterinaryHospitalManagement.Web.Services.Clinical;

public sealed class ClinicalServiceService(ApplicationDbContext db, TimeProvider clock) : IClinicalServiceService
{
    public async Task<IReadOnlyList<ClinicalServiceLine>> ListAsync(int visitId, CancellationToken ct = default) =>
        await db.VisitServices.AsNoTracking().Where(x => x.VisitId == visitId).OrderBy(x => x.Id)
            .Select(x => new ClinicalServiceLine(x.Id, x.VisitId, x.ServiceNameSnapshot, x.Quantity,
                x.UnitPrice, x.Status, x.CancellationReason, x.PerformedAt, x.RowVersion)).ToListAsync(ct);

    public async Task<int> AddAsync(AddClinicalServiceRequest request, CancellationToken ct = default)
    {
        if (request.Quantity <= 0 || request.Quantity > 99999999m || decimal.Round(request.Quantity, 2) != request.Quantity)
            throw new ClinicalServiceManagementException("Số lượng phải lớn hơn 0 và có tối đa 2 chữ số thập phân.");
        return await ExecuteAsync(async () =>
        {
            var visit = await EditableVisitAsync(request.VisitId, request.ActorUserId, ct);
            var catalog = await db.ServiceCatalogs.SingleOrDefaultAsync(x => x.Id == request.ServiceCatalogId, ct);
            if (catalog is null || !catalog.IsActive)
                throw new ClinicalServiceManagementException("Dịch vụ không tồn tại hoặc đã ngừng sử dụng.");
            if (catalog.Price < 0 || decimal.Truncate(catalog.Price) != catalog.Price)
                throw new ClinicalServiceManagementException("Đơn giá dịch vụ phải là số nguyên không âm theo quy tắc lượt khám.");
            var line = new VisitService { VisitId = visit.Id, ServiceCatalogId = catalog.Id,
                ServiceNameSnapshot = catalog.Name, UnitPrice = catalog.Price, Quantity = request.Quantity,
                Status = VisitServiceStatus.Pending, CreatedAt = clock.GetUtcNow() };
            db.VisitServices.Add(line);
            await db.SaveChangesAsync(ct);
            Audit("VisitService.Added", line.Id, request.ActorUserId, visit.VisitNumber);
            return line.Id;
        }, ct);
    }

    public Task PerformAsync(PerformClinicalServiceRequest request, CancellationToken ct = default) =>
        ChangeAsync(request.LineId, request.ActorUserId, request.ExpectedRowVersion,
            VisitServiceStatus.Performed, null, ct);

    public Task CancelAsync(CancelClinicalServiceRequest request, CancellationToken ct = default)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            throw new ClinicalServiceManagementException("Lý do hủy phải có từ 1 đến 500 ký tự.");
        return ChangeAsync(request.LineId, request.ActorUserId, request.ExpectedRowVersion,
            VisitServiceStatus.Cancelled, reason, ct);
    }

    private async Task ChangeAsync(int id, string actor, byte[] version, VisitServiceStatus status, string? reason, CancellationToken ct)
    {
        await ExecuteAsync(async () =>
        {
            var line = await db.VisitServices.SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new ClinicalServiceManagementException("Không tìm thấy dòng dịch vụ.");
            var visit = await EditableVisitAsync(line.VisitId, actor, ct);
            if (line.Status != VisitServiceStatus.Pending)
                throw new ClinicalServiceManagementException("Chỉ dòng dịch vụ đang chờ mới có thể cập nhật.");
            if (version is null || !line.RowVersion.AsSpan().SequenceEqual(version))
                throw new ClinicalServiceManagementException("Dòng dịch vụ đã thay đổi. Hãy tải lại trang.");
            db.Entry(line).Property(x => x.RowVersion).OriginalValue = version;
            line.Status = status;
            if (status == VisitServiceStatus.Performed)
            {
                line.PerformedAt = clock.GetUtcNow();
                line.PerformedByVeterinarianId = visit.VeterinarianId;
            }
            else line.CancellationReason = reason;
            Audit(status == VisitServiceStatus.Performed ? "VisitService.Performed" : "VisitService.Cancelled",
                line.Id, actor, visit.VisitNumber);
            return 0;
        }, ct);
    }

    private async Task<Visit> EditableVisitAsync(int visitId, string actor, CancellationToken ct)
    {
        var visit = await db.Visits.Include(x => x.Veterinarian).ThenInclude(x => x.User)
            .SingleOrDefaultAsync(x => x.Id == visitId, ct)
            ?? throw new ClinicalServiceManagementException("Không tìm thấy lượt khám.");
        var hasVetRole = await db.UserRoles.AnyAsync(link => link.UserId == actor &&
            db.Roles.Any(role => role.Id == link.RoleId && role.Name == SystemRoleNames.Veterinarian), ct);
        if (visit.Veterinarian.UserId != actor || !visit.Veterinarian.IsActive ||
            !visit.Veterinarian.User.IsActive || !hasVetRole) throw new ClinicalServiceAccessException();
        if (visit.Status != VisitStatus.InProgress)
            throw new ClinicalServiceManagementException("Chỉ có thể đổi dịch vụ khi lượt khám đang diễn ra.");
        return visit;
    }

    private async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                var result = await operation();
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return result;
            });
        }
        catch (DbUpdateConcurrencyException) { throw new ClinicalServiceManagementException("Dòng dịch vụ đã thay đổi. Hãy tải lại trang."); }
        catch (SqlException ex) when (ex.Number == 1205) { throw new ClinicalServiceManagementException("Dịch vụ đang được cập nhật. Hãy thử lại."); }
    }

    private void Audit(string action, int id, string actor, string visitNumber) => db.AuditLogs.Add(new AuditLog
    {
        ActorType = "Internal", UserId = actor, Action = action, EntityName = "VisitService",
        EntityId = id.ToString(CultureInfo.InvariantCulture), Description = $"{action} for visit {visitNumber}.",
        CreatedAt = clock.GetUtcNow()
    });
}
