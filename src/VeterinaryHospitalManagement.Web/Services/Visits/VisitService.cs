using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Entities;
using VeterinaryHospitalManagement.Web.Models.Enums;

namespace VeterinaryHospitalManagement.Web.Services.Visits;

public sealed class VisitService(ApplicationDbContext db, TimeProvider clock) : IVisitService
{
    // ── CheckIn từ Appointment ──────────────────────────────────────────────────

    public async Task<int> CheckInFromAppointmentAsync(CheckInFromAppointmentRequest request, CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

                // Đọc lại Appointment kèm Pet, Owner, Vet để lấy snapshot
                var apt = await db.Appointments
                    .Include(a => a.Pet).ThenInclude(p => p.Owner)
                    .Include(a => a.Veterinarian).ThenInclude(v => v.User)
                    .FirstOrDefaultAsync(a => a.Id == request.AppointmentId, ct);

                if (apt is null)
                    throw new VisitManagementException("Không tìm thấy lịch hẹn.");

                // Idempotent: nếu đã CheckedIn và có Visit thì trả về Visit hiện tại
                if (apt.Status == AppointmentStatus.CheckedIn)
                {
                    var existing = await db.Visits
                        .Where(v => v.AppointmentId == apt.Id)
                        .Select(v => v.Id)
                        .FirstOrDefaultAsync(ct);
                    if (existing != 0)
                    {
                        await tx.RollbackAsync(ct);
                        return existing;
                    }
                }

                if (apt.Status != AppointmentStatus.Scheduled)
                    throw new VisitManagementException(
                        $"Chỉ có thể check-in lịch hẹn ở trạng thái Scheduled. Trạng thái hiện tại: {apt.Status}.");

                // Kiểm tra Pet không có Visit đang mở
                var petHasActive = await db.Visits.AnyAsync(
                    v => v.PetId == apt.PetId &&
                         (v.Status == VisitStatus.Waiting || v.Status == VisitStatus.InProgress), ct);
                if (petHasActive)
                    throw new VisitManagementException("Thú cưng này hiện đang có lượt khám chưa hoàn tất.");

                // Tạo VisitNumber
                var visitNum = await GenerateVisitNumberAsync(ct);

                var visit = new Visit
                {
                    VisitNumber = visitNum,
                    AppointmentId = apt.Id,
                    PetId = apt.PetId,
                    VeterinarianId = apt.VeterinarianId,
                    PetNameSnapshot = apt.Pet.Name,
                    OwnerNameSnapshot = apt.Pet.Owner.FullName,
                    OwnerPhoneSnapshot = apt.Pet.Owner.PhoneNumber,
                    VeterinarianNameSnapshot = apt.Veterinarian.User.FullName,
                    Status = VisitStatus.Waiting,
                    CheckedInAt = clock.GetUtcNow(),
                    CheckedInByUserId = request.PerformedByUserId
                };

                // Cập nhật Appointment → CheckedIn
                apt.Status = AppointmentStatus.CheckedIn;

                db.Visits.Add(visit);
                await db.SaveChangesAsync(ct);

                db.AuditLogs.Add(Audit(request.PerformedByUserId, "Visit.CheckedIn",
                    $"Check-in from appointment {apt.AppointmentNumber}.", visit, visitNum));

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return visit.Id;
            });
        }
        catch (DbUpdateException ex) when (FindSql(ex) is { Number: 2601 or 2627 })
        {
            throw new VisitManagementException("Thú cưng đã được check-in bởi người dùng khác. Hãy tải lại.");
        }
        catch (SqlException ex) when (ex.Number == 1205)
        {
            throw new VisitManagementException("Xung đột dữ liệu. Hãy thử lại.");
        }
    }

    // ── Walk-in ─────────────────────────────────────────────────────────────────

    public async Task<int> WalkInAsync(WalkInRequest request, CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

                // Validate Pet + Vet active
                var pet = await db.Pets.Include(p => p.Owner)
                    .FirstOrDefaultAsync(p => p.Id == request.PetId && p.IsActive && p.Owner.IsActive, ct);
                if (pet is null)
                    throw new VisitManagementException("Thú cưng hoặc chủ nuôi không hoạt động.");

                var vet = await db.VeterinarianProfiles.Include(v => v.User)
                    .FirstOrDefaultAsync(v => v.Id == request.VeterinarianId && v.IsActive && v.User.IsActive, ct);
                if (vet is null)
                    throw new VisitManagementException("Bác sĩ thú y không hoạt động.");

                // Kiểm tra Pet không có Visit đang mở
                var petHasActive = await db.Visits.AnyAsync(
                    v => v.PetId == request.PetId &&
                         (v.Status == VisitStatus.Waiting || v.Status == VisitStatus.InProgress), ct);
                if (petHasActive)
                    throw new VisitManagementException("Thú cưng này hiện đang có lượt khám chưa hoàn tất.");

                var visitNum = await GenerateVisitNumberAsync(ct);

                var visit = new Visit
                {
                    VisitNumber = visitNum,
                    AppointmentId = null,
                    PetId = request.PetId,
                    VeterinarianId = request.VeterinarianId,
                    PetNameSnapshot = pet.Name,
                    OwnerNameSnapshot = pet.Owner.FullName,
                    OwnerPhoneSnapshot = pet.Owner.PhoneNumber,
                    VeterinarianNameSnapshot = vet.User.FullName,
                    Status = VisitStatus.Waiting,
                    CheckedInAt = clock.GetUtcNow(),
                    CheckedInByUserId = request.PerformedByUserId
                };

                db.Visits.Add(visit);
                await db.SaveChangesAsync(ct);

                db.AuditLogs.Add(Audit(request.PerformedByUserId, "Visit.WalkIn",
                    $"Walk-in visit created for pet {pet.Name}.", visit, visitNum));

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return visit.Id;
            });
        }
        catch (DbUpdateException ex) when (FindSql(ex) is { Number: 2601 or 2627 })
        {
            throw new VisitManagementException("Thú cưng đã được tiếp nhận bởi người dùng khác.");
        }
        catch (SqlException ex) when (ex.Number == 1205)
        {
            throw new VisitManagementException("Xung đột dữ liệu. Hãy thử lại.");
        }
    }

    // ── Phân công lại bác sĩ ────────────────────────────────────────────────────

    public async Task AssignVeterinarianAsync(AssignVeterinarianRequest request, CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

                var visit = await db.Visits.FirstOrDefaultAsync(v => v.Id == request.VisitId, ct);
                if (visit is null) throw new VisitManagementException("Không tìm thấy lượt khám.");
                if (visit.Status != VisitStatus.Waiting)
                    throw new VisitManagementException("Chỉ có thể phân công lại bác sĩ khi Visit đang Waiting.");

                var vet = await db.VeterinarianProfiles.Include(v => v.User)
                    .FirstOrDefaultAsync(v => v.Id == request.NewVeterinarianId && v.IsActive && v.User.IsActive, ct);
                if (vet is null) throw new VisitManagementException("Bác sĩ thú y không hoạt động.");

                db.Entry(visit).Property(v => v.RowVersion).OriginalValue = request.RowVersion;

                var oldVetName = visit.VeterinarianNameSnapshot;
                visit.VeterinarianId = request.NewVeterinarianId;
                visit.VeterinarianNameSnapshot = vet.User.FullName;

                db.AuditLogs.Add(Audit(request.PerformedByUserId, "Visit.VeterinarianAssigned",
                    $"Visit {visit.VisitNumber}: reassigned from {oldVetName} to {vet.User.FullName}.", visit, visit.VisitNumber));

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new VisitManagementException("Lượt khám đã bị thay đổi bởi người khác. Vui lòng tải lại.");
        }
        catch (SqlException ex) when (ex.Number == 1205)
        {
            throw new VisitManagementException("Xung đột dữ liệu. Hãy thử lại.");
        }
    }

    // ── Hủy Visit ───────────────────────────────────────────────────────────────

    public async Task CancelAsync(CancelVisitRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.CancellationReason))
            throw new VisitManagementException("Lý do hủy là bắt buộc.");

        var reason = request.CancellationReason.Trim()[..Math.Min(request.CancellationReason.Trim().Length, 500)];

        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

                var visit = await db.Visits.FirstOrDefaultAsync(v => v.Id == request.VisitId, ct);
                if (visit is null) throw new VisitManagementException("Không tìm thấy lượt khám.");
                if (visit.Status != VisitStatus.Waiting)
                    throw new VisitManagementException("Chỉ có thể hủy lượt khám đang Waiting.");

                db.Entry(visit).Property(v => v.RowVersion).OriginalValue = request.RowVersion;

                visit.Status = VisitStatus.Cancelled;
                visit.CancellationReason = reason;

                db.AuditLogs.Add(Audit(request.PerformedByUserId, "Visit.Cancelled",
                    $"Visit {visit.VisitNumber} cancelled. Reason: {reason}", visit, visit.VisitNumber));

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new VisitManagementException("Lượt khám đã bị thay đổi bởi người khác. Vui lòng tải lại.");
        }
        catch (SqlException ex) when (ex.Number == 1205)
        {
            throw new VisitManagementException("Xung đột dữ liệu. Hãy thử lại.");
        }
    }

    // ── Bắt đầu khám ────────────────────────────────────────────────────────────

    public async Task StartAsync(StartVisitRequest request, CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

                var visit = await db.Visits
                    .Include(v => v.Veterinarian).ThenInclude(vet => vet.User)
                    .FirstOrDefaultAsync(v => v.Id == request.VisitId, ct);
                if (visit is null) throw new VisitManagementException("Không tìm thấy lượt khám.");
                if (visit.Status != VisitStatus.Waiting)
                    throw new VisitManagementException("Chỉ có thể bắt đầu khám từ trạng thái Waiting.");

                // Chỉ bác sĩ phụ trách mới được bắt đầu khám
                if (visit.Veterinarian.User.Id != request.PerformedByUserId)
                    throw new VisitManagementException("Chỉ bác sĩ được phân công mới có thể bắt đầu khám.");

                // Kiểm tra bác sĩ không có Visit InProgress khác
                var vetBusy = await db.Visits.AnyAsync(
                    v => v.VeterinarianId == visit.VeterinarianId &&
                         v.Status == VisitStatus.InProgress &&
                         v.Id != visit.Id, ct);
                if (vetBusy)
                    throw new VisitManagementException("Bác sĩ đang có một lượt khám khác chưa hoàn tất.");

                // Kiểm tra profile + user còn active
                if (!visit.Veterinarian.IsActive || !visit.Veterinarian.User.IsActive)
                    throw new VisitManagementException("Bác sĩ không còn hoạt động.");

                db.Entry(visit).Property(v => v.RowVersion).OriginalValue = request.RowVersion;

                visit.Status = VisitStatus.InProgress;
                visit.StartedAt = clock.GetUtcNow();

                db.AuditLogs.Add(Audit(request.PerformedByUserId, "Visit.Started",
                    $"Visit {visit.VisitNumber} started.", visit, visit.VisitNumber));

                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new VisitManagementException("Lượt khám đã bị thay đổi bởi người khác. Vui lòng tải lại.");
        }
        catch (DbUpdateException ex) when (FindSql(ex) is { Number: 2601 or 2627 })
        {
            throw new VisitManagementException("Bác sĩ vừa bắt đầu một lượt khám khác. Hãy tải lại.");
        }
        catch (SqlException ex) when (ex.Number == 1205)
        {
            throw new VisitManagementException("Xung đột dữ liệu. Hãy thử lại.");
        }
    }

    // ── Truy vấn ────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<VisitQueueItem>> GetQueueAsync(int? veterinarianId, CancellationToken ct = default)
    {
        var q = db.Visits.AsNoTracking()
            .Where(v => v.Status == VisitStatus.Waiting || v.Status == VisitStatus.InProgress);
        if (veterinarianId.HasValue)
            q = q.Where(v => v.VeterinarianId == veterinarianId.Value);

        return await q.OrderBy(v => v.CheckedInAt)
            .Select(v => new VisitQueueItem(
                v.Id, v.VisitNumber, v.PetNameSnapshot, v.OwnerNameSnapshot,
                v.OwnerPhoneSnapshot, v.VeterinarianNameSnapshot,
                v.Status.ToString(), v.CheckedInAt, v.AppointmentId))
            .ToListAsync(ct);
    }

    public async Task<VisitDetailDto?> GetDetailsAsync(int visitId, CancellationToken ct = default)
    {
        return await db.Visits.AsNoTracking()
            .Where(v => v.Id == visitId)
            .Select(v => new VisitDetailDto(
                v.Id, v.VisitNumber, v.AppointmentId,
                v.PetId, v.PetNameSnapshot, v.OwnerNameSnapshot, v.OwnerPhoneSnapshot,
                v.VeterinarianId, v.VeterinarianNameSnapshot,
                v.Status.ToString(),
                v.CheckedInAt, v.StartedAt, v.CompletedAt, v.CancellationReason,
                v.CheckedInByUser.FullName,
                v.RowVersion))
            .FirstOrDefaultAsync(ct);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private async Task<string> GenerateVisitNumberAsync(CancellationToken ct)
    {
        var parameter = new SqlParameter
        {
            ParameterName = "@SequenceValue",
            SqlDbType = SqlDbType.BigInt,
            Direction = ParameterDirection.Output
        };

        await db.Database.ExecuteSqlRawAsync(
            "SELECT @SequenceValue = NEXT VALUE FOR [VisitNumberSequence];",
            [parameter],
            ct);

        var seq = Convert.ToInt64(parameter.Value, CultureInfo.InvariantCulture);
        return $"V-{clock.GetUtcNow():yyyyMMdd}-{seq:D4}";
    }

    private AuditLog Audit(string userId, string action, string description, Visit visit, string visitNum) =>
        new()
        {
            ActorType = "Internal",
            UserId = userId,
            Action = action,
            EntityName = "Visit",
            EntityId = visit.Id.ToString(CultureInfo.InvariantCulture),
            Description = description,
            CreatedAt = clock.GetUtcNow()
        };

    private static SqlException? FindSql(Exception? ex)
    {
        while (ex is not null) { if (ex is SqlException s) return s; ex = ex.InnerException; }
        return null;
    }
}
