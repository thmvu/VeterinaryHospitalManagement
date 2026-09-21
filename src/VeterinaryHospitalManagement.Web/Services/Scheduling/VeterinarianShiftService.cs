using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Authorization;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Entities;

namespace VeterinaryHospitalManagement.Web.Services.Scheduling;

public sealed class VeterinarianShiftService(
    ApplicationDbContext db,
    TimeProvider clock) : IVeterinarianShiftService
{
    public async Task<IReadOnlyList<ShiftListItem>> ListAsync(int? veterinarianId, CancellationToken ct = default)
    {
        var query = db.VeterinarianShifts
            .AsNoTracking()
            .OrderBy(x => x.VeterinarianId)
            .ThenBy(x => x.StartAt);

        if (veterinarianId.HasValue)
        {
            query = (IOrderedQueryable<VeterinarianShift>)query.Where(x => x.VeterinarianId == veterinarianId.Value);
        }

        return await query
            .Select(x => new ShiftListItem(
                x.Id,
                x.VeterinarianId,
                x.Veterinarian.DoctorCode,
                x.Veterinarian.User.FullName,
                x.StartAt,
                x.EndAt,
                x.IsActive,
                x.RowVersion))
            .ToListAsync(ct);
    }

    public async Task<ShiftDetails?> FindAsync(int id, CancellationToken ct = default)
        => await db.VeterinarianShifts
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new ShiftDetails(
                x.Id,
                x.VeterinarianId,
                x.Veterinarian.DoctorCode,
                x.Veterinarian.User.FullName,
                x.StartAt,
                x.EndAt,
                x.IsActive,
                x.RowVersion))
            .SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<VeterinarianChoiceItem>> GetActiveVeterinariansAsync(CancellationToken ct = default)
        => await db.VeterinarianProfiles
            .AsNoTracking()
            .Where(v => v.IsActive && v.User.IsActive)
            .OrderBy(v => v.DoctorCode)
            .Select(v => new VeterinarianChoiceItem(v.Id, v.DoctorCode, v.User.FullName))
            .ToListAsync(ct);

    public Task<int> CreateAsync(CreateShiftRequest request, CancellationToken ct = default)
        => InTransactionAsync(async () =>
        {
            VeterinarianShiftRules.ValidateTimeRange(request.StartAt, request.EndAt);

            var profile = await db.VeterinarianProfiles
                .Include(v => v.User)
                .SingleOrDefaultAsync(v => v.Id == request.VeterinarianId, ct)
                ?? throw new ShiftManagementException("Không tìm thấy hồ sơ bác sĩ.");

            if (!profile.IsActive || !profile.User.IsActive)
            {
                throw new ShiftManagementException("Chỉ có thể tạo ca cho bác sĩ đang hoạt động.");
            }

            await EnsureNoOverlapAsync(request.VeterinarianId, request.StartAt, request.EndAt, excludeId: null, ct);

            var shift = new VeterinarianShift
            {
                VeterinarianId = request.VeterinarianId,
                StartAt = request.StartAt.ToUniversalTime(),
                EndAt = request.EndAt.ToUniversalTime(),
                IsActive = true
            };
            db.VeterinarianShifts.Add(shift);
            await db.SaveChangesAsync(ct);

            AddAudit(request.ActorUserId, "VeterinarianShift.Created", shift.Id,
                $"Created shift {shift.StartAt:O} – {shift.EndAt:O} for vet {profile.DoctorCode}.");
            await db.SaveChangesAsync(ct);

            return shift.Id;
        }, ct);

    public Task UpdateAsync(UpdateShiftRequest request, CancellationToken ct = default)
        => GuardAsync(() => InTransactionAsync(async () =>
        {
            VeterinarianShiftRules.ValidateTimeRange(request.StartAt, request.EndAt);

            var shift = await LoadAsync(request.Id, request.ExpectedRowVersion, ct);

            await EnsureNoOverlapAsync(shift.VeterinarianId, request.StartAt, request.EndAt, excludeId: shift.Id, ct);

            shift.StartAt = request.StartAt.ToUniversalTime();
            shift.EndAt = request.EndAt.ToUniversalTime();

            AddAudit(request.ActorUserId, "VeterinarianShift.Updated", shift.Id,
                $"Updated shift to {shift.StartAt:O} – {shift.EndAt:O}.");
            await db.SaveChangesAsync(ct);
            return 0;
        }, ct));

    public Task SetActiveAsync(ShiftActivationRequest request, CancellationToken ct = default)
        => GuardAsync(() => InTransactionAsync(async () =>
        {
            var shift = await LoadAsync(request.Id, request.ExpectedRowVersion, ct);
            shift.IsActive = request.IsActive;

            var action = request.IsActive ? "VeterinarianShift.Activated" : "VeterinarianShift.Deactivated";
            AddAudit(request.ActorUserId, action, shift.Id,
                $"Changed shift active status to {request.IsActive}.");
            await db.SaveChangesAsync(ct);
            return 0;
        }, ct));

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task EnsureNoOverlapAsync(
        int veterinarianId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        int? excludeId,
        CancellationToken ct)
    {
        // Load all active shifts for this vet (excluding the shift being edited)
        var existing = await db.VeterinarianShifts
            .Where(x => x.VeterinarianId == veterinarianId && x.IsActive && (excludeId == null || x.Id != excludeId))
            .Select(x => new { x.StartAt, x.EndAt })
            .ToListAsync(ct);

        foreach (var s in existing)
        {
            if (VeterinarianShiftRules.AreOverlapping(startAt, endAt, s.StartAt, s.EndAt))
            {
                throw new ShiftManagementException(
                    "Ca làm việc mới bị trùng với một ca đang hoạt động của bác sĩ này.");
            }
        }
    }

    private async Task<VeterinarianShift> LoadAsync(int id, byte[] expectedRowVersion, CancellationToken ct)
    {
        var shift = await db.VeterinarianShifts.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new ShiftManagementException("Không tìm thấy ca làm việc.");

        if (!shift.RowVersion.AsSpan().SequenceEqual(expectedRowVersion))
        {
            throw new ShiftConcurrencyException();
        }

        return shift;
    }

    private void AddAudit(string actorUserId, string action, int shiftId, string description)
        => db.AuditLogs.Add(new AuditLog
        {
            ActorType = "Internal",
            UserId = actorUserId,
            Action = action,
            EntityName = "VeterinarianShift",
            EntityId = shiftId.ToString(CultureInfo.InvariantCulture),
            Description = description,
            CreatedAt = clock.GetUtcNow()
        });

    private async Task<T> InTransactionAsync<T>(Func<Task<T>> op, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var result = await op();
                await tx.CommitAsync(ct);
                return result;
            }
            catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
            {
                var isSqlConflict = ex.InnerException is SqlException sql &&
                    sql.Number is 2601 or 2627;
                if (isSqlConflict)
                {
                    throw new ShiftManagementException("Dữ liệu ca làm việc bị xung đột. Hãy thử lại.");
                }
                throw;
            }
        });
    }

    private static async Task GuardAsync(Func<Task> op)
    {
        try
        {
            await op();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ShiftConcurrencyException();
        }
    }
}
