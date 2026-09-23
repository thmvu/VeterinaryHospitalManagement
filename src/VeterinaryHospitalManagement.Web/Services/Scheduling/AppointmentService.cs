using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VeterinaryHospitalManagement.Web.Data;
using VeterinaryHospitalManagement.Web.Models.Entities;
using VeterinaryHospitalManagement.Web.Models.Enums;

namespace VeterinaryHospitalManagement.Web.Services.Scheduling;

public sealed class AppointmentService(ApplicationDbContext db, TimeProvider clock) : IAppointmentService
{
    public async Task<IReadOnlyList<AppointmentListItem>> ListAsync(DateTimeOffset from, DateTimeOffset to,
        int? veterinarianId = null, CancellationToken ct = default)
    {
        AppointmentRules.ValidateTimeRange(from, to);
        var query = db.Appointments.AsNoTracking()
            .Where(x => x.StartAt < to && from < x.EndAt);
        if (veterinarianId.HasValue) query = query.Where(x => x.VeterinarianId == veterinarianId.Value);
        return await query.OrderBy(x => x.StartAt).Select(x => new AppointmentListItem(
            x.Id, x.AppointmentNumber, x.PetId, x.Pet.Name, x.VeterinarianId,
            x.Veterinarian.User.FullName, x.StartAt, x.EndAt, x.Reason, x.Status.ToString())).ToListAsync(ct);
    }

    public async Task<AppointmentAvailability> CheckAvailabilityAsync(int petId, int veterinarianId,
        DateTimeOffset startAt, DateTimeOffset endAt, CancellationToken ct = default)
    {
        AppointmentRules.ValidateTimeRange(startAt, endAt);
        startAt = startAt.ToUniversalTime(); endAt = endAt.ToUniversalTime();
        var eligibility = await ValidateParticipantsAndShiftAsync(petId, veterinarianId, startAt, endAt, ct);
        if (eligibility is not null) return new(false, eligibility);
        var conflict = await HasConflictAsync(petId, veterinarianId, startAt, endAt, ct);
        return conflict ? new(false, "Bác sĩ hoặc thú cưng đã có lịch trong khoảng thời gian này.") : new(true, null);
    }

    public async Task<int> CreateAsync(CreateAppointmentRequest request, CancellationToken ct = default)
    {
        AppointmentRules.ValidateTimeRange(request.StartAt, request.EndAt);
        var reason = AppointmentRules.NormalizeReason(request.Reason);
        var startAt = request.StartAt.ToUniversalTime(); var endAt = request.EndAt.ToUniversalTime();
        var strategy = db.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                await AcquireLocksAsync(request.PetId, request.VeterinarianId, ct);
                var error = await ValidateParticipantsAndShiftAsync(request.PetId, request.VeterinarianId, startAt, endAt, ct);
                if (error is not null) throw new AppointmentManagementException(error);
                if (await HasConflictAsync(request.PetId, request.VeterinarianId, startAt, endAt, ct))
                    throw new AppointmentManagementException("Bác sĩ hoặc thú cưng đã có lịch trong khoảng thời gian này.");

                var appointment = new Appointment
                {
                    AppointmentNumber = $"APT-{clock.GetUtcNow():yyyyMMdd}-{Guid.NewGuid():N}"[..25].ToUpperInvariant(),
                    PetId = request.PetId, VeterinarianId = request.VeterinarianId,
                    StartAt = startAt, EndAt = endAt, Reason = reason,
                    Status = AppointmentStatus.Scheduled, CreatedByUserId = request.ActorUserId,
                    CreatedAt = clock.GetUtcNow()
                };
                db.Appointments.Add(appointment);
                await db.SaveChangesAsync(ct);
                db.AuditLogs.Add(new AuditLog { ActorType = "Internal", UserId = request.ActorUserId,
                    Action = "Appointment.Created", EntityName = "Appointment",
                    EntityId = appointment.Id.ToString(CultureInfo.InvariantCulture),
                    Description = $"Created appointment {appointment.AppointmentNumber}.", CreatedAt = clock.GetUtcNow() });
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return appointment.Id;
            });
        }
        catch (DbUpdateException ex) when (FindSqlException(ex) is { Number: 2601 or 2627 })
        { throw new AppointmentManagementException("Dữ liệu lịch hẹn bị xung đột. Hãy thử lại."); }
        catch (SqlException ex) when (ex.Number == 1205)
        { throw new AppointmentManagementException("Lịch hẹn vừa được thay đổi bởi người khác. Hãy thử lại."); }
    }

    private async Task<string?> ValidateParticipantsAndShiftAsync(int petId, int veterinarianId,
        DateTimeOffset startAt, DateTimeOffset endAt, CancellationToken ct)
    {
        var petActive = await db.Pets.AnyAsync(x => x.Id == petId && x.IsActive && x.Owner.IsActive, ct);
        if (!petActive) return "Chỉ có thể đặt lịch cho thú cưng và chủ nuôi đang hoạt động.";
        var vetActive = await db.VeterinarianProfiles.AnyAsync(x => x.Id == veterinarianId && x.IsActive && x.User.IsActive, ct);
        if (!vetActive) return "Chỉ có thể đặt lịch với bác sĩ đang hoạt động.";
        var inShift = await db.VeterinarianShifts.AnyAsync(x => x.VeterinarianId == veterinarianId && x.IsActive
            && x.StartAt <= startAt && x.EndAt >= endAt, ct);
        return inShift ? null : "Thời gian khám phải nằm trọn trong ca làm việc đang hoạt động của bác sĩ.";
    }

    private Task<bool> HasConflictAsync(int petId, int veterinarianId, DateTimeOffset startAt,
        DateTimeOffset endAt, CancellationToken ct) => db.Appointments.AnyAsync(x =>
            (x.Status == AppointmentStatus.Scheduled || x.Status == AppointmentStatus.CheckedIn)
            && (x.PetId == petId || x.VeterinarianId == veterinarianId)
            && x.StartAt < endAt && startAt < x.EndAt, ct);

    private async Task AcquireLocksAsync(int petId, int veterinarianId, CancellationToken ct)
    {
        foreach (var resource in new[] { $"Appointment.Pet.{petId}", $"Appointment.Vet.{veterinarianId}" }.Order())
        {
            var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
            var resourceParameter = new SqlParameter("@resource", SqlDbType.NVarChar, 255) { Value = resource };
            await db.Database.ExecuteSqlRawAsync(
                "EXEC @result = sp_getapplock @Resource=@resource, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000",
                [result, resourceParameter], ct);
            if ((int)result.Value < 0) throw new AppointmentManagementException("Không thể khóa lịch để kiểm tra. Hãy thử lại.");
        }
    }

    private static SqlException? FindSqlException(Exception? ex)
    { while (ex is not null) { if (ex is SqlException sql) return sql; ex = ex.InnerException; } return null; }
}
